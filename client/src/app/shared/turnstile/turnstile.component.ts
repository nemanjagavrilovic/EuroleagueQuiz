import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface PublicConfiguration { turnstileSiteKey: string; }
interface TurnstileApi {
  render(container: HTMLElement, options: Record<string, unknown>): string;
  reset(widgetId: string): void;
  remove(widgetId: string): void;
}

declare global { interface Window { turnstile?: TurnstileApi; } }

@Component({
  selector: 'app-turnstile',
  standalone: true,
  imports: [NgIf],
  template: `<div class="turnstile"><div #container></div><p *ngIf="error()" role="alert">{{ error() }}</p></div>`,
  styles: [`
    :host { display: block; min-width: 0; }
    .turnstile { min-height: 68px; }
    .turnstile > div { max-width: 100%; }
    p { margin: 7px 0 0; color: var(--error-text); font-size: 12px; line-height: 1.5; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TurnstileComponent implements AfterViewInit, OnDestroy {
  @Input({ required: true }) action = '';
  @Output() readonly tokenChange = new EventEmitter<string>();
  @ViewChild('container', { static: true }) private readonly container!: ElementRef<HTMLDivElement>;
  private readonly http = inject(HttpClient);
  private widgetId?: string;
  readonly error = signal('');
  private static scriptPromise?: Promise<void>;

  ngAfterViewInit(): void { void this.initialize(); }

  reset(): void {
    this.tokenChange.emit('');
    if (this.widgetId && window.turnstile) window.turnstile.reset(this.widgetId);
  }

  ngOnDestroy(): void {
    if (this.widgetId && window.turnstile) window.turnstile.remove(this.widgetId);
  }

  private async initialize(): Promise<void> {
    try {
      const baseUrl = environment.apiUrl.replace(/\/+$/, '');
      const configuration = await firstValueFrom(this.http.get<PublicConfiguration>(`${baseUrl}/config/public`));
      await TurnstileComponent.loadScript();
      if (!window.turnstile || !configuration.turnstileSiteKey) throw new Error('Turnstile is not configured.');
      this.widgetId = window.turnstile.render(this.container.nativeElement, {
        sitekey: configuration.turnstileSiteKey,
        action: this.action,
        theme: 'auto',
        size: 'flexible',
        callback: (token: string) => { this.error.set(''); this.tokenChange.emit(token); },
        'expired-callback': () => { this.tokenChange.emit(''); },
        'error-callback': () => { this.tokenChange.emit(''); this.error.set('Verification could not load. Please try again.'); }
      });
    } catch {
      this.error.set('Verification is temporarily unavailable. Please refresh the page.');
      this.tokenChange.emit('');
    }
  }

  private static loadScript(): Promise<void> {
    if (window.turnstile) return Promise.resolve();
    if (this.scriptPromise) return this.scriptPromise;
    this.scriptPromise = new Promise<void>((resolve, reject) => {
      const existing = document.querySelector<HTMLScriptElement>('script[data-pickandroll-turnstile]');
      if (existing) {
        existing.addEventListener('load', () => resolve(), { once: true });
        existing.addEventListener('error', () => reject(new Error('Turnstile failed to load.')), { once: true });
        return;
      }
      const script = document.createElement('script');
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
      script.async = true;
      script.defer = true;
      script.dataset['pickandrollTurnstile'] = 'true';
      script.addEventListener('load', () => resolve(), { once: true });
      script.addEventListener('error', () => reject(new Error('Turnstile failed to load.')), { once: true });
      document.head.appendChild(script);
    });
    return this.scriptPromise;
  }
}
