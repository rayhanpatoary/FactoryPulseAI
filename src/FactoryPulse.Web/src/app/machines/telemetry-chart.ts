import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CategoryScale,
  Chart,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
} from 'chart.js';
import { Language } from '../i18n/language';
import { Reading } from '../monitoring/models';

Chart.register(CategoryScale, LineController, LineElement, LinearScale, PointElement, Tooltip);

@Component({
  selector: 'fp-telemetry-chart',
  imports: [TranslatePipe],
  template: `
    <section class="surface panel telemetry-chart">
      <h3>{{ 'measure.' + metric() | translate }} ({{ unit() }})</h3>
      <div>
        <canvas #canvas role="img" [attr.aria-label]="'measure.' + metric() | translate"></canvas>
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TelemetryChart {
  readonly readings = input.required<readonly Reading[]>();
  readonly metric = input.required<'temperature' | 'vibration' | 'rpm'>();
  readonly unit = input.required<string>();
  readonly color = input.required<string>();
  private readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly locale = inject(Language).locale;
  private chart?: Chart<'line'>;

  constructor() {
    effect((cleanup) => {
      const canvas = this.canvas()?.nativeElement;
      if (!canvas) return;
      this.chart = new Chart(canvas, {
        type: 'line',
        data: { labels: [], datasets: [{ data: [], borderWidth: 2, pointRadius: 0 }] },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          animation: false,
          scales: { x: { ticks: { maxTicksLimit: 5 } }, y: { ticks: { maxTicksLimit: 5 } } },
        },
      });
      cleanup(() => {
        this.chart?.destroy();
        this.chart = undefined;
      });
    });
    effect(() => {
      const readings = this.readings();
      const metric = this.metric();
      const color = this.color();
      const locale = this.locale();
      if (!this.canvas() || !this.chart) return;
      const time = new Intl.DateTimeFormat(locale, {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      });
      this.chart.data.labels = readings.map((reading) => time.format(new Date(reading.timestampUtc)));
      this.chart.data.datasets[0].data = readings.map((reading) => reading[metric]);
      this.chart.data.datasets[0].borderColor = color;
      this.chart.options.locale = locale;
      this.chart.update('none');
    });
  }
}
