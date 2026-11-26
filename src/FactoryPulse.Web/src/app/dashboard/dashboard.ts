import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../i18n/language';
import { MonitoringStore } from '../monitoring/monitoring-store';

@Component({
  selector: 'fp-dashboard',
  imports: [RouterLink, DatePipe, DecimalPipe, MatButtonModule, TranslatePipe],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dashboard {
  protected readonly store = inject(MonitoringStore);
  protected readonly locale = inject(Language).locale;
  protected readonly catalogOnly = inject(ActivatedRoute).snapshot.data['catalogOnly'] === true;
}
