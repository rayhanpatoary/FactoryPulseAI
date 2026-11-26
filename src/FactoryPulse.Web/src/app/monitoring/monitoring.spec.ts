import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HubConnection } from '@microsoft/signalr';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LiveReading, Machine } from './models';
import { MonitoringStore } from './monitoring-store';
import { compareUtc, machineCounts, mergeReadings } from './reconcile';
import { MONITORING_CONNECTION, MonitoringRealtime } from './realtime';

const machine: Machine = { id: 'machine', machineCode: 'CNC-001', name: 'Mill', status: 'Running' };
const reading: LiveReading = {
  machineId: machine.id,
  machineCode: machine.machineCode,
  status: 'Idle',
  timestampUtc: '2026-11-25T12:30:00.0000001Z',
  temperature: 50,
  vibration: 1,
  rpm: 2000,
  torque: 12,
  toolWear: 0.2,
};

function configure() {
  const handlers = new Map<string, (reading: LiveReading) => void>();
  const reconnecting = vi.fn();
  const reconnected = vi.fn();
  const closed = vi.fn();
  const hub = {
    on: vi.fn((event: string, handler: (reading: LiveReading) => void) => handlers.set(event, handler)),
    off: vi.fn(),
    start: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    stop: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    onreconnecting: reconnecting,
    onreconnected: reconnected,
    onclose: closed,
  };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: MONITORING_CONNECTION,
        useValue: () => hub as unknown as HubConnection,
      },
    ],
  });
  return {
    hub,
    reconnecting,
    reconnected,
    closed,
    handlers,
    http: TestBed.inject(HttpTestingController),
  };
}

function flushSnapshot(http: HttpTestingController, catalog: Machine[], latest: LiveReading | null) {
  http.expectOne('/api/status').flush({ operatingMode: 'simulation' });
  http.expectOne('/api/machines').flush(catalog);
  if (catalog.length) http.expectOne('/api/machines/machine/readings/latest').flush(latest);
  http.expectOne('/api/alarms/active?page=1&pageSize=100').flush({ page: 1, hasMore: false, items: [] });
}

describe('monitoring', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('uses the real connection state, registers each event once and cleans up', async () => {
    const { hub, reconnecting, reconnected, closed, handlers } = configure();
    const realtime = TestBed.inject(MonitoringRealtime);
    const observer = vi.fn();
    realtime.readings.subscribe(observer);
    hub.start.mockRejectedValueOnce(new Error('Unavailable'));
    await realtime.connect();
    expect(realtime.state()).toBe('unavailable');
    await realtime.connect();
    expect(realtime.state()).toBe('live');
    await realtime.connect();
    expect(hub.start).toHaveBeenCalledTimes(2);
    expect([...handlers.keys()]).toEqual([
      'MachineReadingUpdated',
      'AlarmOpened',
      'AlarmResolved',
      'AlarmAcknowledged',
    ]);
    handlers.get('MachineReadingUpdated')?.(reading);
    expect(observer).toHaveBeenCalledExactlyOnceWith(reading);
    reconnecting.mock.calls[0][0]();
    expect(realtime.state()).toBe('reconnecting');
    reconnected.mock.calls[0][0]();
    expect(realtime.state()).toBe('live');
    closed.mock.calls[0][0]();
    expect(realtime.state()).toBe('disconnected');
    TestBed.resetTestingModule();
    expect(hub.off).toHaveBeenCalledTimes(4);
    expect(hub.stop).toHaveBeenCalledOnce();
  });

  it('merges newer live readings while initial and reconnect snapshots load', () => {
    const { http, handlers, reconnected } = configure();
    const store = TestBed.inject(MonitoringStore);
    expect(store.loading()).toBe(true);
    handlers.get('MachineReadingUpdated')?.(reading);
    flushSnapshot(http, [machine], {
      ...reading,
      timestampUtc: '2026-11-25T12:30:00Z',
      temperature: 30,
    });
    expect(store.loading()).toBe(false);
    expect(store.machines()[0]).toMatchObject({ status: 'Idle', reading: { temperature: 50 } });
    handlers.get('MachineReadingUpdated')?.({ ...reading, temperature: 99 });
    expect(store.machines()[0].reading?.temperature).toBe(50);
    reconnected.mock.calls[0][0]();
    flushSnapshot(http, [machine], {
      ...reading,
      timestampUtc: '2026-11-25T12:30:01Z',
      temperature: 60,
    });
    expect(store.machines()[0].reading?.temperature).toBe(60);
    http.verify();
  });

  it('reports an unreachable backend and supports retry to an empty catalogue', () => {
    const { http } = configure();
    const store = TestBed.inject(MonitoringStore);
    http.expectOne('/api/status').flush(null, { status: 503, statusText: 'Unavailable' });
    expect(http.expectOne('/api/machines').cancelled).toBe(true);
    http
      .expectOne('/api/alarms/active?page=1&pageSize=100')
      .flush({ page: 1, hasMore: false, items: [] });
    expect(store.error()).toBe('errors.offline');
    store.refresh();
    flushSnapshot(http, [], null);
    expect(store.error()).toBe(null);
    expect(store.machines()).toEqual([]);
    http.verify();
  });

  it('counts operational states and bounds ordered unique sample history', () => {
    expect(machineCounts([machine, { ...machine, id: '2', status: 'Faulted' }])).toEqual({
      total: 2,
      running: 1,
      stopped: 1,
    });
    expect(compareUtc('2026-11-25T12:30:00Z', reading.timestampUtc)).toBeLessThan(0);
    const samples = Array.from({ length: 140 }, (_, index) => ({
      ...reading,
      timestampUtc: new Date(Date.UTC(2026, 10, 25, 12, 30, index)).toISOString(),
    }));
    const merged = mergeReadings([], [...samples].reverse());
    expect(merged).toEqual(samples.slice(-120));
    expect(mergeReadings(merged, [samples[139]])).toEqual(merged);
  });
});
