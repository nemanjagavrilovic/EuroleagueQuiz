import { Injectable, signal } from '@angular/core';

export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  hasPassword: boolean;
  roles: string[];
  subscriptionPlan: string;
}

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  readonly user = signal<AuthUser | null>(null);
  private token = '';

  get accessToken(): string { return this.token; }
  setSession(accessToken: string, user: AuthUser): void { this.token = accessToken; this.user.set(user); }
  updateUser(user: AuthUser): void { if (this.user()?.id === user.id) this.user.set(user); }
  clear(): void { this.token = ''; this.user.set(null); }
}
