import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, forkJoin, map, merge, of, startWith, Subject, switchMap } from 'rxjs';
import { AlarmTable } from '../monitoring/alarm-table';
import { MachineDetails, Reading, Alarm } from '../monitoring/models';
import { errorKey, MonitoringApi } from '../monitoring/monitoring-api';
import { MonitoringStore } from '../monitoring/monitoring-store';
import { ReadingsTable } from '../monitoring/readings-table';
import { MonitoringRealtime } from '../monitoring/realtime';
import { mergeReadings } from '../monitoring/reconcile';
import { TelemetryChart } from './telemetry-chart';

interface MachineView {
  machine: MachineDetails;
  readings: Reading[];
  alarms: Alarm[];
}

@Component({
  selector: 'fp-machine-detail',
  imports: [RouterLink, MatButtonModule, TranslatePipe, AlarmTable, ReadingsTable, TelemetryChart],
  templateUrl: './machine-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MachineDetail {
  private readonly api = inject(MonitoringApi);
  private readonly realtime = inject(MonitoringRealtime);
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(MonitoringStore);
  protected readonly retry = new Subject<void>();
  private readonly id = toSignal(this.route.paramMap.pipe(map((params) => params.get('id') ?? '')), {
    initialValue: this.route.snapshot.paramMap.get('id') ?? '',
  });
  private readonly live = signal<{ id: string; samples: Reading[] }>({ id: '', samples: [] });

  protected readonly view = toSignal(
    this.route.paramMap.pipe(
      map((params) => params.get('id') ?? ''),
      switchMap((id) =>
        merge(of(null), this.retry, this.realtime.recovered, this.realtime.alarms).pipe(
          switchMap(() =>
            forkJoin({
              machine: this.api.machine(id),
              history: this.api.readings(id, 1, 100),
              alarms: this.api.machineAlarms(id),
            }).pipe(
              map(({ machine, history, alarms }): MachineView => ({
                machine,
                readings: history.items,
                alarms: alarms.items,
              })),
              catchError((error: unknown) => of(errorKey(error))),
            ),
          ),
          startWith(null),
        ),
      ),
    ),
    { initialValue: null },
  );
  protected readonly samples = computed(() => {
    const view = this.view();
    const live = this.live();
    const snapshot = typeof view === 'object' && view?.machine.id === this.id() ? view.readings : [];
    return mergeReadings(mergeReadings([], snapshot), live.id === this.id() ? live.samples : []);
  });
  protected readonly currentStatus = computed(
    () => this.store.machines().find((item) => item.id === this.id())?.status,
  );
  protected readonly latest = computed(() => {
    const snapshot = this.store.machines().find((item) => item.id === this.id())?.reading;
    return mergeReadings(this.samples(), snapshot ? [snapshot] : [], 1);
  });

  constructor() {
    this.realtime.readings.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((sample) => {
      if (sample.machineId !== this.id()) return;
      this.live.update((live) => ({
        id: sample.machineId,
        samples: mergeReadings(live.id === sample.machineId ? live.samples : [], [sample]),
      }));
    });
  }
}
