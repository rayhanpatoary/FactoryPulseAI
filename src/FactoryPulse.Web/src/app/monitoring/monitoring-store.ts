import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  catchError,
  EMPTY,
  filter,
  forkJoin,
  from,
  map,
  merge,
  mergeMap,
  of,
  Subject,
  switchMap,
  takeUntil,
  toArray,
} from 'rxjs';
import { LiveReading, Machine, Reading } from './models';
import { errorKey, MonitoringApi } from './monitoring-api';
import { compareUtc, machineCounts } from './reconcile';
import { MonitoringRealtime } from './realtime';

@Injectable({ providedIn: 'root' })
export class MonitoringStore {
  private readonly api = inject(MonitoringApi);
  private readonly realtime = inject(MonitoringRealtime);
  private readonly reload = new Subject<void>();
  private readonly reloadAlarms = new Subject<void>();
  private readonly leaveSession = new Subject<void>();
  private readonly catalog = signal<Machine[]>([]);
  private readonly known = computed(() => new Set(this.catalog().map((machine) => machine.id)));
  private readonly live = signal<ReadonlyMap<string, LiveReading>>(new Map());
  private readonly persisted = signal<ReadonlyMap<string, Reading>>(new Map());
  private loaded = false;
  private active = false;

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly alarmError = signal<string | null>(null);
  readonly mode = signal<string | null>(null);
  readonly lastUpdate = signal<number | null>(null);
  readonly alarmCounts = signal<{ active: number; warnings: number } | null>(null);
  readonly machines = computed(() =>
    this.catalog().map((machine) => {
      const live = this.live().get(machine.id);
      const stored = this.persisted().get(machine.id);
      const useLive = live && (!stored || compareUtc(live.timestampUtc, stored.timestampUtc) >= 0);
      return {
        ...machine,
        status: (useLive && live.status) || machine.status,
        reading: useLive ? live : stored,
      };
    }),
  );
  readonly counts = computed(() => machineCounts(this.machines()));

  constructor() {
    const destroy = inject(DestroyRef);
    this.realtime.readings.pipe(takeUntilDestroyed(destroy)).subscribe((sample) => this.accept(sample));
    const refresh = merge(this.reload, this.realtime.recovered);
    refresh
      .pipe(
        filter(() => this.active),
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);
          return forkJoin({
            mode: this.api.mode(),
            snapshot: this.api.machines().pipe(
              switchMap((machines) =>
                from(machines).pipe(
                  mergeMap((machine) => this.api.latest(machine.id), 6),
                  toArray(),
                  map((readings) => ({ machines, readings })),
                ),
              ),
            ),
          }).pipe(
            takeUntil(this.leaveSession),
            catchError((error: unknown) => {
              this.error.set(errorKey(error));
              this.loading.set(false);
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(destroy),
      )
      .subscribe(({ mode, snapshot: { machines, readings } }) => {
        this.catalog.set(machines);
        this.persisted.set(
          new Map(
            readings
              .filter((reading) => reading !== null)
              .map((reading) => [reading.machineId, reading]),
          ),
        );
        this.live.update((samples) => new Map([...samples].filter(([id]) => this.known().has(id))));
        this.mode.set(mode);
        this.lastUpdate.set(Date.now());
        this.loaded = true;
        this.loading.set(false);
      });

    merge(refresh, this.reloadAlarms, this.realtime.alarms)
      .pipe(
        filter(() => this.active),
        switchMap(() =>
          this.api.activeSummary().pipe(
            takeUntil(this.leaveSession),
            catchError((error: unknown) => {
              this.alarmError.set(errorKey(error));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(destroy),
      )
      .subscribe((counts) => {
        if (counts) this.alarmError.set(null);
        this.alarmCounts.set(counts);
      });
  }

  start() {
    if (this.active) return;
    this.active = true;
    void this.realtime.connect();
    this.refresh();
  }

  stop() {
    this.active = false;
    this.loaded = false;
    this.leaveSession.next();
    void this.realtime.disconnect();
    this.catalog.set([]);
    this.live.set(new Map());
    this.persisted.set(new Map());
    this.mode.set(null);
    this.lastUpdate.set(null);
    this.alarmCounts.set(null);
    this.alarmError.set(null);
    this.error.set(null);
    this.loading.set(false);
  }

  refresh() {
    this.reload.next();
  }

  refreshAlarms() {
    this.reloadAlarms.next();
  }

  private accept(sample: LiveReading) {
    if (!this.active) return;
    if (this.loaded && !this.known().has(sample.machineId)) return;
    const previous = this.live().get(sample.machineId);
    if (previous && compareUtc(sample.timestampUtc, previous.timestampUtc) <= 0) return;
    this.live.update((samples) => {
      const next = new Map(samples);
      next.set(sample.machineId, sample);
      // Keep an upper bound on readings received before the machine snapshot arrives.
      if (!this.loaded && next.size > 128) {
        const oldest = next.keys().next();
        if (!oldest.done) next.delete(oldest.value);
      }
      return next;
    });
    this.lastUpdate.set(Date.now());
  }
}
