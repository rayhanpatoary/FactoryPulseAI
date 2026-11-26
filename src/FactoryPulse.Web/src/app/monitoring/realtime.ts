import { DestroyRef, inject, Injectable, InjectionToken, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { LiveReading, MonitoringEvents } from './models';

type AlarmEventName = Exclude<keyof MonitoringEvents, 'MachineReadingUpdated'>;
type AlarmEvent = {
  [Name in AlarmEventName]: { name: Name; payload: MonitoringEvents[Name] };
}[AlarmEventName];
export type ConnectionState = 'connecting' | 'live' | 'reconnecting' | 'unavailable' | 'disconnected';

export const MONITORING_CONNECTION = new InjectionToken<() => HubConnection>('Monitoring connection', {
  providedIn: 'root',
  factory: () => () =>
    new HubConnectionBuilder()
      .withUrl('/hubs/monitoring')
      .withAutomaticReconnect([0, 2000, 5000, 15000])
      .configureLogging(LogLevel.None)
      .build(),
});

@Injectable({ providedIn: 'root' })
export class MonitoringRealtime {
  private readonly hub = inject(MONITORING_CONNECTION)();
  private readonly connectionState = signal<ConnectionState>('disconnected');
  private readonly readingEvents = new Subject<LiveReading>();
  private readonly alarmEvents = new Subject<AlarmEvent>();
  private readonly recoveries = new Subject<void>();
  private attempted = false;
  private destroyed = false;
  private revision = 0;
  private stopping?: Promise<void>;

  readonly state = this.connectionState.asReadonly();
  readonly readings = this.readingEvents.asObservable();
  readonly alarms = this.alarmEvents.asObservable();
  readonly recovered = this.recoveries.asObservable();

  constructor() {
    this.hub.on('MachineReadingUpdated', (payload: LiveReading) => this.readingEvents.next(payload));
    this.hub.on('AlarmOpened', (payload: MonitoringEvents['AlarmOpened']) =>
      this.alarmEvents.next({ name: 'AlarmOpened', payload }),
    );
    this.hub.on('AlarmResolved', (payload: MonitoringEvents['AlarmResolved']) =>
      this.alarmEvents.next({ name: 'AlarmResolved', payload }),
    );
    this.hub.on('AlarmAcknowledged', (payload: MonitoringEvents['AlarmAcknowledged']) =>
      this.alarmEvents.next({ name: 'AlarmAcknowledged', payload }),
    );
    this.hub.onreconnecting(() => {
      if (this.attempted) this.connectionState.set('reconnecting');
    });
    this.hub.onreconnected(() => this.connected(true));
    this.hub.onclose(() => this.connectionState.set('disconnected'));

    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      for (const name of [
        'MachineReadingUpdated',
        'AlarmOpened',
        'AlarmResolved',
        'AlarmAcknowledged',
      ]) {
        this.hub.off(name);
      }
      this.readingEvents.complete();
      this.alarmEvents.complete();
      this.recoveries.complete();
      void this.disconnect();
    });
  }

  async connect() {
    const revision = this.revision;
    if (this.stopping) await this.stopping;
    if (
      this.destroyed ||
      revision !== this.revision ||
      !['disconnected', 'unavailable'].includes(this.state())
    )
      return;
    const retry = this.attempted;
    this.attempted = true;
    this.connectionState.set('connecting');
    try {
      await this.hub.start();
      if (revision === this.revision) this.connected(retry);
    } catch {
      if (!this.destroyed && revision === this.revision) this.connectionState.set('unavailable');
    }
  }

  disconnect() {
    this.revision++;
    this.attempted = false;
    this.connectionState.set('disconnected');
    return (this.stopping ??= this.hub
      .stop()
      .catch(() => undefined)
      .finally(() => (this.stopping = undefined)));
  }

  private connected(recovered: boolean) {
    if (this.destroyed || !this.attempted) return;
    this.connectionState.set('live');
    if (recovered) this.recoveries.next();
  }
}
