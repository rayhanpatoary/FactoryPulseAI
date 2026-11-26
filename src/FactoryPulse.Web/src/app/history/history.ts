import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, of, startWith, switchMap } from 'rxjs';
import { errorKey, MonitoringApi } from '../monitoring/monitoring-api';
import { MonitoringStore } from '../monitoring/monitoring-store';
import { Pagination } from '../monitoring/pagination';
import { ReadingsTable } from '../monitoring/readings-table';

export function dateRange(start: string, end: string) {
  const from = start ? new Date(start) : null;
  const to = end ? new Date(end) : null;
  if (
    (from && !Number.isFinite(from.getTime())) ||
    (to && !Number.isFinite(to.getTime())) ||
    (from && to && from >= to)
  ) {
    return null;
  }
  return { fromUtc: from?.toISOString(), toUtc: to?.toISOString() };
}

@Component({
  selector: 'fp-history',
  imports: [MatButtonModule, TranslatePipe, Pagination, ReadingsTable],
  templateUrl: './history.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class History {
  private readonly api = inject(MonitoringApi);
  protected readonly store = inject(MonitoringStore);
  protected readonly invalidRange = signal(false);
  protected readonly query = signal<{
    id: string;
    page: number;
    fromUtc?: string;
    toUtc?: string;
  } | null>(null);
  protected readonly result = toSignal(
    toObservable(this.query).pipe(
      switchMap((query) =>
        query
          ? this.api.readings(query.id, query.page, 25, query.fromUtc, query.toUtc).pipe(
              catchError((error: unknown) => of(errorKey(error))),
              startWith(null),
            )
          : of(null),
      ),
    ),
    { initialValue: null },
  );

  protected apply(id: string, start: string, end: string) {
    const range = dateRange(start, end);
    this.invalidRange.set(!range);
    if (range && id) this.query.set({ id, page: 1, ...range });
  }

  protected move(page: number) {
    this.query.update((query) => query && { ...query, page });
  }
}
