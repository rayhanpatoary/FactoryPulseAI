import { TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, describe, expect, it } from 'vitest';
import { Language } from './language';

describe('dashboard language', () => {
  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('updates translations, locale and the document language at runtime', () => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ fallbackLang: 'en' })] });
    const language = TestBed.inject(Language);
    const translate = TestBed.inject(TranslateService);
    expect(translate.instant('nav.dashboard')).toBe('Dashboard');
    language.set('de');
    expect(translate.instant('nav.dashboard')).toBe('Übersicht');
    expect(language.locale()).toBe('de-DE');
    expect(document.documentElement.lang).toBe('de');
    expect(localStorage.getItem('factorypulse.language')).toBe('de');
  });
});
