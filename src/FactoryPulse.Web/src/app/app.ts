import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from './i18n/language';
import { MonitoringRealtime } from './monitoring/realtime';
import { MonitoringStore } from './monitoring/monitoring-store';

@Component({
  selector: 'fp-root',
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
  protected readonly navigation = [
    { path: '/', label: 'nav.dashboard', icon: '▦' },
    { path: '/machines', label: 'nav.machines', icon: '▤' },
    { path: '/settings', label: 'nav.settings', icon: '⚙' },
  ];
}
