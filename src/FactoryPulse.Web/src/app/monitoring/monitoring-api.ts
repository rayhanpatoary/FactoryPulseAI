import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { EMPTY, expand, map, reduce } from 'rxjs';
import { Alarm, AlarmFilters, Machine, MachineDetails, Page, Reading } from './models';

@Injectable({ providedIn: 'root' })
export class MonitoringApi {
  private readonly http = inject(HttpClient);

  machines() {
    return this.http.get<Machine[]>('/api/machines');
  }

  machine(id: string) {
    return this.http.get<MachineDetails>(this.machinePath(id));
  }

  latest(id: string) {
    return this.http.get<Reading | null>(`${this.machinePath(id)}/readings/latest`);
  }

  readings(id: string, page: number, pageSize = 25, fromUtc?: string, toUtc?: string) {
    return this.http.get<Page<Reading>>(`${this.machinePath(id)}/readings`, {
      params: { page, pageSize, ...(fromUtc && { fromUtc }), ...(toUtc && { toUtc }) },
    });
  }

  alarms(page: number, filters: AlarmFilters, pageSize = 25) {
    return this.http.get<Page<Alarm>>('/api/alarms', { params: { page, pageSize, ...filters } });
  }

  machineAlarms(id: string) {
    return this.http.get<Page<Alarm>>(`${this.machinePath(id)}/alarms`);
  }

  activeSummary() {
    const load = (page: number) =>
      this.http.get<Page<Alarm>>('/api/alarms/active', { params: { page, pageSize: 100 } });
    return load(1).pipe(
      expand((page) => (page.hasMore ? load(page.page + 1) : EMPTY)),
      reduce(
        (summary, page) => {
          for (const alarm of page.items) {
            if (alarm.severity !== 'Information') summary.warnings.add(alarm.machineId);
            summary.alarms.add(alarm.id);
          }
          return summary;
        },
        { alarms: new Set<number>(), warnings: new Set<string>() },
      ),
      map(({ alarms, warnings }) => ({ active: alarms.size, warnings: warnings.size })),
    );
  }

  mode() {
    return this.http
      .get<{ operatingMode: string }>('/api/status')
      .pipe(map((status) => status.operatingMode));
  }

  acknowledge(id: number) {
    return this.http.post<Alarm>(`/api/alarms/${id}/acknowledge`, null);
  }

  private machinePath(id: string) {
    return `/api/machines/${encodeURIComponent(id)}`;
  }
}

export function errorKey(error: unknown) {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 403) return 'errors.forbidden';
    if (error.status === 404) return 'errors.missing';
    if (error.status === 0 || error.status === 502 || error.status === 503) return 'errors.offline';
  }
  return 'errors.load';
}
