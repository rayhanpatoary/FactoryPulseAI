import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import de from './de.json';
import en from './en.json';

const storageKey = 'factorypulse.language';
export type LanguageCode = 'en' | 'de';

@Injectable({ providedIn: 'root' })
export class Language {
  private readonly translator = inject(TranslateService);
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<LanguageCode>('en');
  readonly code = this.current.asReadonly();
  readonly locale = computed(() => (this.code() === 'de' ? 'de-DE' : 'en-GB'));

  constructor() {
    this.translator.setTranslation('en', en);
    this.translator.setTranslation('de', de);
    let stored: string | null = null;
    try {
      stored = this.document.defaultView?.localStorage.getItem(storageKey) ?? null;
    } catch {
      // Language switching still works when browser storage is unavailable.
    }
    this.set(stored === 'de' ? 'de' : 'en');
  }

  set(code: LanguageCode) {
    this.current.set(code);
    this.document.documentElement.lang = code;
    this.translator.use(code).subscribe();
    try {
      this.document.defaultView?.localStorage.setItem(storageKey, code);
    } catch {
      // Keep the in-memory preference for this session.
    }
  }
}
