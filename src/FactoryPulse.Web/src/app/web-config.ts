import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class WebConfig {
  readonly acknowledgementEnabled = toSignal(
    inject(HttpClient)
      .get<{ allowAlarmAcknowledgement?: boolean }>('/web-config.json')
      .pipe(
        map((config) => config?.allowAlarmAcknowledgement === true),
        catchError(() => of(false)),
      ),
    { initialValue: false },
  );
}
