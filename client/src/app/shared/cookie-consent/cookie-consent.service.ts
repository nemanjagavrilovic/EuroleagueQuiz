import { Injectable, computed, signal } from '@angular/core';

export interface CookieConsentPreferences {
  version: 1;
  essential: true;
  analytics: boolean;
  updatedAt: string;
}

const storageKey = 'pickandroll.cookie-consent.v1';

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private readonly preferencesState = signal<CookieConsentPreferences | null>(this.load());
  readonly preferences = this.preferencesState.asReadonly();
  readonly analyticsAllowed = computed(() => this.preferencesState()?.analytics === true);
  readonly visible = signal(this.preferencesState() === null);

  open(): void { this.visible.set(true); }

  close(): void {
    if (this.preferencesState()) this.visible.set(false);
  }

  acceptAll(): void { this.save(true); }
  essentialOnly(): void { this.save(false); }

  save(analytics: boolean): void {
    const preferences: CookieConsentPreferences = {
      version: 1,
      essential: true,
      analytics,
      updatedAt: new Date().toISOString()
    };

    this.preferencesState.set(preferences);
    this.visible.set(false);

    try {
      localStorage.setItem(storageKey, JSON.stringify(preferences));
    } catch {
      // The in-memory choice still applies when storage is unavailable.
    }

    window.dispatchEvent(new CustomEvent('pickandroll:consent-changed', { detail: preferences }));
  }

  private load(): CookieConsentPreferences | null {
    try {
      const stored = localStorage.getItem(storageKey);
      if (!stored) return null;

      const value = JSON.parse(stored) as Partial<CookieConsentPreferences>;
      if (value.version !== 1 || typeof value.analytics !== 'boolean') return null;

      return {
        version: 1,
        essential: true,
        analytics: value.analytics,
        updatedAt: typeof value.updatedAt === 'string' ? value.updatedAt : new Date().toISOString()
      };
    } catch {
      return null;
    }
  }
}
