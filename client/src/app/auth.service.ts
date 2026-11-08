import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, firstValueFrom, of, tap, throwError } from 'rxjs';
import { environment } from '../environments/environment';
import { AuthSessionStore, AuthUser } from './auth-session.store';

interface AuthResponse { accessToken: string; accessTokenExpiresAtUtc: string; user: AuthUser; }
interface MessageResponse { message: string; errors?: { code: string; description: string }[]; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly store = inject(AuthSessionStore);
  private readonly baseUrl = environment.apiUrl.replace(/\/+$/, '');
  private refreshTimer?: ReturnType<typeof setTimeout>;
  readonly user = this.store.user;

  async restoreSession(): Promise<void> {
    await firstValueFrom(this.refresh().pipe(catchError(() => { this.clearSession(); return of(null); })));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/auth/login`, { email, password }, { withCredentials: true })
      .pipe(tap(response => this.accept(response)));
  }

  register(displayName: string, email: string, password: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/register`, { displayName, email, password });
  }

  confirmEmail(userId: string, token: string): Observable<MessageResponse> {
    return this.http.get<MessageResponse>(`${this.baseUrl}/auth/confirm-email`, { params: { userId, token } });
  }

  resendConfirmation(email: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/resend-confirmation`, { email });
  }

  forgotPassword(email: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/forgot-password`, { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/reset-password`, { email, token, newPassword })
      .pipe(tap(() => this.clearSession()));
  }

  setPassword(email: string, token: string, newPassword: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/set-password`, { email, token, newPassword })
      .pipe(tap(() => this.clearSession()));
  }

  requestPasswordSetupLink(): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/password-setup-link`, {});
  }

  refreshUser(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${this.baseUrl}/auth/me`).pipe(
      tap(user => this.store.updateUser(user)),
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) this.clearSession();
        return throwError(() => error);
      })
    );
  }

  updateProfile(firstName: string, lastName: string): Observable<AuthUser> {
    return this.http.put<AuthUser>(`${this.baseUrl}/auth/profile`, { firstName, lastName })
      .pipe(tap(user => this.store.updateUser(user)));
  }

  requestPasswordResetLink(): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/auth/password-reset-link`, {});
  }

  beginGoogleLogin(returnUrl = '/'): void {
    window.location.assign(`${this.baseUrl}/auth/google?returnUrl=${encodeURIComponent(this.safeReturnUrl(returnUrl))}`);
  }

  async completeGoogleLogin(returnUrl = '/'): Promise<void> {
    await this.restoreSession();
    await this.router.navigateByUrl(this.store.user() ? this.safeReturnUrl(returnUrl) : '/login?error=google');
  }

  logout(): void {
    this.http.post<void>(`${this.baseUrl}/auth/logout`, {}, { withCredentials: true })
      .pipe(catchError(() => of(undefined))).subscribe(() => { this.clearSession(); void this.router.navigateByUrl('/'); });
  }

  private refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/auth/refresh`, {}, { withCredentials: true })
      .pipe(tap(response => this.accept(response)));
  }

  private accept(response: AuthResponse): void {
    this.store.setSession(response.accessToken, response.user);
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    const delay = Math.max(30_000, new Date(response.accessTokenExpiresAtUtc).getTime() - Date.now() - 60_000);
    this.refreshTimer = setTimeout(() => this.refresh().pipe(catchError(() => { this.clearSession(); return of(null); })).subscribe(), delay);
  }

  private clearSession(): void {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = undefined;
    this.store.clear();
  }

  private safeReturnUrl(value: string): string { return value.startsWith('/') && !value.startsWith('//') ? value : '/'; }
}
