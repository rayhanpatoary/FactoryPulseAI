import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'fp-pagination',
  imports: [MatButtonModule, TranslatePipe],
  template: `
    <div class="pagination">
      <button
        matButton="outlined"
        type="button"
        [disabled]="page() <= 1"
        (click)="navigate.emit(page() - 1)"
      >
        {{ 'actions.previous' | translate }}
      </button>
      <span>{{ 'common.page' | translate: { page: page() } }}</span>
      <button
        matButton="outlined"
        type="button"
        [disabled]="!hasMore() || page() >= 10000"
        (click)="navigate.emit(page() + 1)"
      >
        {{ 'actions.next' | translate }}
      </button>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Pagination {
  readonly page = input.required<number>();
  readonly hasMore = input.required<boolean>();
  readonly navigate = output<number>();
}
