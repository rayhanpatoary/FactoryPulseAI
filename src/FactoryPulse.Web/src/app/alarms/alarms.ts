import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, combineLatest, finalize, merge, of, startWith, Subject, switchMap } from 'rxjs';
import { AlarmTable } from '../monitoring/alarm-table';
import { AlarmFilters, Severity } from '../monitoring/models';
import { errorKey, MonitoringApi } from '../monitoring/monitoring-api';
import { MonitoringStore } from '../monitoring/monitoring-store';
import { Pagination } from '../monitoring/pagination';
import { MonitoringRealtime } from '../monitoring/realtime';
import { WebConfig } from '../web-config';

@Component({
  selector: 'fp-alarms',
  imports: [MatButtonModule, TranslatePipe, AlarmTable, Pagination],
  templateUrl: './alarms.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Alarms {
  private readonly api = inject(MonitoringApi);
  private readonly realtime = inject(MonitoringRealtime);
  private readonly destroy = inject(DestroyRef);
  private readonly refresh = new Subject<void>();
  protected readonly config = inject(WebConfig);
  protected readonly store = inject(MonitoringStore);
  protected readonly pending = signal<number | null>(null);
  protected readonly writeError = signal<string | null>(null);
  protected readonly query = signal<{ page: number; filters: AlarmFilters }>({ page: 1, filters: {} });
  protected readonly severities: Severity[] = ['Information', 'Warning', 'Critical'];

  protected readonly result = toSignal(
    combineLatest([
      toObservable(this.query),
      merge(of(null), this.refresh, this.realtime.recovered, this.realtime.alarms),
    ]).pipe(
      switchMap(([query]) =>
        this.api.alarms(query.page, query.filters).pipe(
          catchError((error: unknown) => of(errorKey(error))),
          startWith(null),
        ),
      ),
    ),
    { initialValue: null },
  );

  protected apply(machineId: string, severity: string, active: string) {
    const selectedSeverity = this.severities.find((value) => value === severity);
    this.query.set({
      page: 1,
      filters: {
        ...(machineId && { machineId }),
        ...(selectedSeverity && { severity: selectedSeverity }),
        ...(active && { active: active === 'true' }),
      },
    });
  }

  protected move(page: number) {
    this.query.update((query) => ({ ...query, page }));
  }

  protected reload() {
    this.refresh.next();
  }

  protected acknowledge(id: number) {
    if (!this.config.acknowledgementEnabled() || this.pending() !== null) return;
    this.pending.set(id);
    this.writeError.set(null);
    this.api
      .acknowledge(id)
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.pending.set(null)),
      )
      .subscribe({
        next: () => {
          this.reload();
          this.store.refreshAlarms();
        },
        error: (error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 403)
            this.config.disableAcknowledgement();
          this.writeError.set(errorKey(error));
        },
      });
  }
}
