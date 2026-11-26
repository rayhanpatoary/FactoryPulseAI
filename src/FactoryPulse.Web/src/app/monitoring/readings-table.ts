import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../i18n/language';
import { Reading } from './models';

@Component({
  selector: 'fp-readings-table',
  imports: [DatePipe, DecimalPipe, PercentPipe, TranslatePipe],
  template: `
    <div class="table-wrap">
      <table [attr.aria-label]="'machine.recent' | translate">
        <thead>
          <tr>
            <th scope="col">{{ 'measure.time' | translate }}</th>
            <th scope="col">{{ 'measure.temperature' | translate }} (°C)</th>
            <th scope="col">{{ 'measure.vibration' | translate }} (mm/s)</th>
            <th scope="col">{{ 'measure.rpm' | translate }} (rpm)</th>
            <th scope="col">{{ 'measure.torque' | translate }} (N·m)</th>
            <th scope="col">{{ 'measure.wear' | translate }}</th>
          </tr>
        </thead>
        <tbody>
          @for (reading of readings(); track $index) {
            <tr>
              <td>
                <time [attr.datetime]="reading.timestampUtc">{{
                  reading.timestampUtc | date: 'medium' : undefined : locale()
                }}</time>
              </td>
              <td>{{ reading.temperature | number: '1.0-2' : locale() }}</td>
              <td>{{ reading.vibration | number: '1.0-2' : locale() }}</td>
              <td>{{ reading.rpm | number: '1.0-0' : locale() }}</td>
              <td>{{ reading.torque | number: '1.0-2' : locale() }}</td>
              <td>{{ reading.toolWear | percent: '1.0-1' : locale() }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadingsTable {
  readonly readings = input.required<readonly Reading[]>();
  protected readonly locale = inject(Language).locale;
}
