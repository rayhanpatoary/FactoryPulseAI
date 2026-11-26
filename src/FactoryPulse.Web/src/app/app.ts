import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthSession } from './auth/auth-session';
import { Language } from './i18n/language';
import { MonitoringRealtime } from './monitoring/realtime';
import { MonitoringStore } from './monitoring/monitoring-store';

@Component({
  selector: 'fp-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly store = inject(MonitoringStore);
  protected readonly realtime = inject(MonitoringRealtime);
  protected readonly language = inject(Language);
  protected readonly session = inject(AuthSession);
  protected readonly logoutPending = signal(false);
  protected readonly logoutError = signal(false);
  protected readonly navigation = [
    { path: '/', label: 'nav.dashboard', icon: '▦' },
    { path: '/machines', label: 'nav.machines', icon: '▤' },
    { path: '/alarms', label: 'nav.alarms', icon: '△' },
    { path: '/history', label: 'nav.history', icon: '◷' },
    { path: '/settings', label: 'nav.settings', icon: '⚙' },
  ];

  constructor() {
    this.store.start();
    inject(DestroyRef).onDestroy(() => this.store.stop());
    effect(() => {
      const state = this.realtime.state();
      if (state === 'disconnected' || state === 'unavailable') {
        untracked(() => void this.session.verify());
      }
    });
  }

  protected async logout() {
    if (this.logoutPending()) return;
    this.logoutPending.set(true);
    this.logoutError.set(false);
    try {
      await this.session.logout();
    } catch {
      this.logoutError.set(true);
    } finally {
      this.logoutPending.set(false);
    }
  }
}
