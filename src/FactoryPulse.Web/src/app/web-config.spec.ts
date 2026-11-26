import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { WebConfig } from './web-config';

describe('alarm acknowledgement capability', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('requires explicit opt-in and can be blocked for the session', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const config = TestBed.inject(WebConfig);
    expect(config.acknowledgementEnabled()).toBe(false);
    TestBed.inject(HttpTestingController)
      .expectOne('/web-config.json')
      .flush({ allowAlarmAcknowledgement: true });
    expect(config.acknowledgementEnabled()).toBe(true);
    config.disableAcknowledgement();
    expect(config.acknowledgementEnabled()).toBe(false);
  });
});
