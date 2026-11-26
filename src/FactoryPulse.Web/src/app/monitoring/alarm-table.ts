import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../i18n/language';
import { Alarm, Machine } from './models';

const codes = new Set(['TEMPERATURE_WARNING', 'TEMPERATURE_CRITICAL', 'VIBRATION_WARNING']);

@Component({
  selector: 'fp-alarm-table',
  imports: [DatePipe, TranslatePipe, RouterLink, MatButtonModule],
  templateUrl: './alarm-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlarmTable {
  readonly alarms = input.required<readonly Alarm[]>();
  readonly machines = input<readonly Machine[]>([]);
  readonly showMachine = input(true);
  readonly allowAcknowledgement = input(false);
  readonly pending = input<number | null>(null);
  readonly acknowledged = output<number>();
  protected readonly locale = inject(Language).locale;

  protected alarmLabel(code: string) {
    return codes.has(code) ? `alarmCode.${code}` : null;
  }

  protected machineCode(id: string) {
    return this.machines().find((machine) => machine.id === id)?.machineCode ?? id;
  }
}
