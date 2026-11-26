import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthSession, expireOnUnauthorized } from './auth-session';

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([expireOnUnauthorized])),
      provideHttpClientTesting(),
    ],
  });
  const router = TestBed.inject(Router);
  return {
    session: TestBed.inject(AuthSession),
    http: TestBed.inject(HttpTestingController),
    navigate: vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true),
  };
}

describe('browser session', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  it('loads a server session once and clears it when a monitoring request returns 401', async () => {
    const { session, http, navigate } = setup();
    const request = session.verify();
    expect(session.verify()).toBe(request);
    http.expectOne('/api/auth/me').flush({
      userName: 'operator',
      roles: ['Operator'],
      canAcknowledge: true,
      expiresAtUtc: null,
    });
    expect(await request).toBe(true);
    expect(session.canAcknowledge()).toBe(true);

    TestBed.inject(HttpClient)
      .get('/api/machines')
      .subscribe({ error: () => undefined });
    http.expectOne('/api/machines').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(session.user()).toBeNull();
    expect(session.expired()).toBe(true);
    expect(session.canAcknowledge()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
    http.verify();
  });

  it('expires an idle session at the server ticket deadline', async () => {
    vi.useFakeTimers();
    const { session, http, navigate } = setup();
    const request = session.verify();
    http.expectOne('/api/auth/me').flush({
      userName: 'viewer',
      roles: ['Viewer'],
      canAcknowledge: false,
      expiresAtUtc: new Date(Date.now() + 5000).toISOString(),
    });
    await request;
    expect(session.canAcknowledge()).toBe(false);
    vi.advanceTimersByTime(5000);
    expect(session.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
