import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from './i18n/language';
import { WebConfig } from './web-config';

@Component({
  selector: 'fp-settings',
  imports: [TranslatePipe],
  template: `
    <header class="page-heading">
      <div>
        <h1>{{ 'nav.settings' | translate }}</h1>
        <p>{{ 'settings.subtitle' | translate }}</p>
      </div>
    </header>
    <section class="surface panel">
      <h2>{{ 'settings.language' | translate }}</h2>
      <label class="field">
        <span>{{ 'settings.language' | translate }}</span>
        <select
          [value]="language.code()"
          (change)="language.set($any($event.target).value === 'de' ? 'de' : 'en')"
        >
          <option value="en" lang="en">English</option>
          <option value="de" lang="de">Deutsch</option>
        </select>
      </label>
      <p>{{ 'settings.languageHelp' | translate }}</p>
    </section>
    <section class="surface panel">
      <h2>{{ 'settings.mode' | translate }}</h2>
      <p>{{ 'common.simulation' | translate }}</p>
      <p>
        {{ (config.acknowledgementEnabled() ? 'settings.ackEnabled' : 'settings.readOnly') | translate }}
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Settings {
  protected readonly language = inject(Language);
  protected readonly config = inject(WebConfig);
}
