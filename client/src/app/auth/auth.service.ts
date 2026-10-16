import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthenticatedUser {
  id: number;
  email: string;
  displayName: string;
}

interface AuthResponse {
  accessToken: string;
  user: AuthenticatedUser;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storageKey = 'pick-and-roll-session';
  readonly currentUser = signal<AuthenticatedUser | null>(this.readStoredUser());

  login(email: string, password: string) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap(response => this.saveSession(response)));
  }

  loginWithGoogle(credential: string) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/google`, { credential })
      .pipe(tap(response => this.saveSession(response)));
  }

  register(displayName: string, email: string, password: string) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, { displayName, email, password })
      .pipe(tap(response => this.saveSession(response)));
  }

  logout(): void {
    if (typeof localStorage !== 'undefined') localStorage.removeItem(this.storageKey);
    this.currentUser.set(null);
  }

  get accessToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    try {
      return JSON.parse(localStorage.getItem(this.storageKey) ?? 'null')?.accessToken ?? null;
    } catch {
      return null;
    }
  }

  private saveSession(response: AuthResponse): void {
    if (typeof localStorage !== 'undefined') localStorage.setItem(this.storageKey, JSON.stringify(response));
    this.currentUser.set(response.user);
  }

  private readStoredUser(): AuthenticatedUser | null {
    if (typeof localStorage === 'undefined') return null;
    try {
      return JSON.parse(localStorage.getItem(this.storageKey) ?? 'null')?.user ?? null;
    } catch {
      return null;
    }
  }
}
