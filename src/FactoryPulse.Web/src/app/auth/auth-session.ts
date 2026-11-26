import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, throwError } from 'rxjs';

export interface SessionUser {
  userName: string;
  roles: string[];
  canAcknowledge: boolean;
  expiresAtUtc: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthSession {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly current = signal<SessionUser | null>(null);
  private readonly blocked = signal(false);
  private expiry?: ReturnType<typeof setTimeout>;
  private checking?: Promise<boolean>;
  private revision = 0;

  readonly user = this.current.asReadonly();
  readonly expired = signal(false);
  readonly canAcknowledge = computed(() => this.user()?.canAcknowledge === true && !this.blocked());

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.expiry));
  }

  verify() {
    return (this.checking ??= this.restore().finally(() => (this.checking = undefined)));
  }

  async login(userName: string, password: string) {
    await this.token();
    const user = await firstValueFrom(
      this.http.post<SessionUser>('/api/auth/login', { userName, password }),
    );
    this.revision++;
    this.expired.set(false);
    this.blocked.set(false);
    this.set(user);
    await this.router.navigateByUrl('/');
  }

  async logout() {
    try {
      await this.token();
      await firstValueFrom(this.http.post('/api/auth/logout', null));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) throw error;
    }
    this.end(false);
  }

  end(expired: boolean) {
    if (expired && !this.user()) return;
    this.revision++;
    this.expired.set(expired);
    this.set(null);
    void this.router.navigateByUrl('/login');
  }

  disableAcknowledgement() {
    this.blocked.set(true);
  }

  private async restore() {
    const revision = this.revision;
    try {
      const user = await firstValueFrom(this.http.get<SessionUser>('/api/auth/me'));
      if (revision === this.revision) this.set(user);
    } catch (error) {
      if (revision === this.revision && error instanceof HttpErrorResponse && error.status === 401) {
        this.end(true);
      }
    }
    return this.user() !== null;
  }

  private set(user: SessionUser | null) {
    clearTimeout(this.expiry);
    this.current.set(user);
    if (user?.expiresAtUtc) {
      this.expiry = setTimeout(
        () => this.end(true),
        Math.max(0, Date.parse(user.expiresAtUtc) - Date.now()),
      );
    }
  }

  private token() {
    return firstValueFrom(this.http.get<void>('/api/auth/csrf'));
  }
}

export const requireSession: CanActivateFn = async () => {
  const session = inject(AuthSession);
  const router = inject(Router);
  return session.user() || (await session.verify()) ? true : router.createUrlTree(['/login']);
};

export const expireOnUnauthorized: HttpInterceptorFn = (request, next) => {
  const session = inject(AuthSession);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url.startsWith('/api/') &&
        !request.url.startsWith('/api/auth/')
      ) {
        session.end(true);
      }
      return throwError(() => error);
    }),
  );
};
