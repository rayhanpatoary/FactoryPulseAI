import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class WebConfig {
  private readonly forbidden = signal(false);
  private readonly enabled = toSignal(
    inject(HttpClient)
      .get<{ allowAlarmAcknowledgement?: boolean }>('/web-config.json')
      .pipe(
        map((config) => config?.allowAlarmAcknowledgement === true),
        catchError(() => of(false)),
      ),
    { initialValue: false },
  );
  readonly acknowledgementEnabled = computed(() => this.enabled() && !this.forbidden());

  disableAcknowledgement() {
    this.forbidden.set(true);
  }
}
