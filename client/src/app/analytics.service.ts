import { DOCUMENT } from '@angular/common';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { environment } from '../environments/environment';
import { CookieConsentService } from './shared/cookie-consent/cookie-consent.service';

interface TrackingConfig { googleMeasurementId: string; metaPixelId: string; }
interface TrackedPage { path: string; location: string; title: string; metaSafe: boolean; }
type TagCommand = (...args: unknown[]) => void;
interface MetaPixel extends TagCommand {
  callMethod?: TagCommand;
  queue: unknown[][];
  push: TagCommand;
  loaded: boolean;
  version: string;
  disablePushState: boolean;
}
type TrackingWindow = Window & {
  dataLayer?: unknown[];
  gtag?: TagCommand;
  fbq?: MetaPixel;
  _fbq?: MetaPixel;
} & Partial<Record<`ga-disable-${string}`, boolean>>;

const gridEvents = {
  start: ['quiz_start', 'QuizStart'],
  complete: ['quiz_complete', 'QuizComplete'],
  reveal: ['quiz_reveal', 'QuizReveal']
} as const;
type GridEvent = {
  action: keyof typeof gridEvents;
  data: { game: 'grid'; mode: 'daily' | 'ranked' | 'practice'; difficulty: string };
};

const publicPages = new Set([
  '/', '/contact', '/leaderboard', '/team-history', '/game/history', '/game/missing-club',
  '/game/career-order', '/game/mystery-player', '/game/five-player',
  '/grid/easy', '/grid/normal', '/grid/hard', '/grid/legend'
]);
const campaignKeys = new Set(['utm_source', 'utm_medium', 'utm_campaign', 'utm_id', 'utm_term', 'utm_content']);
const campaignValue = /^[a-z\d_.~-]{1,100}$/i;
const supportedModes = new Set(['daily', 'ranked', 'practice']);

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly document = inject(DOCUMENT);
  private readonly browser = this.document.defaultView as TrackingWindow | null;
  private readonly router = inject(Router);
  private readonly consent = inject(CookieConsentService);
  private readonly destroyRef = inject(DestroyRef);
  // The configuration is public and needs no session token.
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly config = signal<TrackingConfig | null>(null);
  private readonly page = signal<TrackedPage | null>(null);
  private readonly googleReady = signal(false);
  private readonly metaReady = signal(false);
  private googleRequested = false;
  private metaRequested = false;
  private googleActive = false;
  private metaActive = false;
  private googleConfigured = false;
  private metaConfigured = false;
  private googlePage: TrackedPage | null = null;
  private metaPage: TrackedPage | null = null;
  private googleQueue: GridEvent[] = [];
  private metaQueue: GridEvent[] = [];

  constructor() {
    if (!this.browser) return;
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => this.updatePage(event.urlAfterRedirects));
    if (this.router.navigated) this.updatePage(this.router.url);
    this.http.get<TrackingConfig>(`${environment.apiUrl.replace(/\/+$/, '')}/config/public`).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: value => {
        const googleId = value?.googleMeasurementId ?? '';
        const metaId = value?.metaPixelId ?? '';
        this.config.set({
          googleMeasurementId: /^G-[A-Z0-9]+$/.test(googleId) ? googleId : '',
          metaPixelId: /^\d{5,25}$/.test(metaId) ? metaId : ''
        });
      },
      error: () => this.config.set({ googleMeasurementId: '', metaPixelId: '' })
    });
    effect(() => this.syncTags());
  }

  grid(action: GridEvent['action'], mode: GridEvent['data']['mode'], difficulty: string): void {
    const page = this.page();
    if (!page?.path.startsWith('/grid/')) return;
    const level = difficulty.toLowerCase();
    if (!publicPages.has(`/grid/${level}`)) return;
    const event: GridEvent = { action, data: { game: 'grid', mode, difficulty: level } };
    const config = this.config();
    if (this.consent.analyticsAllowed() && (!config || config.googleMeasurementId)) {
      if (this.googleActive && this.googlePage === page) this.sendGoogleGrid(event, page);
      else if (this.googleQueue.length < 8) this.googleQueue.push(event);
    }
    if (page.metaSafe && this.consent.marketingAllowed() && (!config || config.metaPixelId)) {
      if (this.metaActive && this.metaPage === page && config) this.sendMetaGrid(event, config.metaPixelId);
      else if (this.metaQueue.length < 8) this.metaQueue.push(event);
    }
  }

  private updatePage(routerUrl: string): void {
    const browser = this.browser!;
    const url = new URL(routerUrl, browser.location.origin);
    const path = url.pathname.replace(/\/$/, '') || '/';
    let next: TrackedPage | null = null;
    if (url.origin === browser.location.origin && publicPages.has(path)) {
      const campaign = new URLSearchParams();
      let metaSafe = !url.hash || url.hash === '#games-title' || url.hash === '#how-to-play';
      url.searchParams.forEach((value, key) => {
        if (campaignKeys.has(key) && campaignValue.test(value)) campaign.set(key, value);
        else if (key === 'mode' && supportedModes.has(value)) campaign.set(key, value);
        else if (!(key === 'fbclid' && /^[a-z\d_-]{1,512}$/i.test(value))) metaSafe = false;
      });
      const safeUrl = new URL(path, url.origin);
      safeUrl.search = campaign.toString();
      next = { path, location: safeUrl.href, title: this.document.title, metaSafe };
    }
    const previous = this.page();
    if (previous?.location === next?.location && previous?.metaSafe === next?.metaSafe) return;
    this.googleQueue = [];
    this.metaQueue = [];
    this.page.set(next);
  }

  private syncTags(): void {
    const browser = this.browser!;
    const page = this.page();
    const config = this.config();
    const googleAllowed = this.consent.analyticsAllowed();
    const marketingAllowed = this.consent.marketingAllowed();
    if (!googleAllowed || !page || config?.googleMeasurementId === '') this.googleQueue = [];
    if (!marketingAllowed || !page?.metaSafe || config?.metaPixelId === '') this.metaQueue = [];
    if (!config) return;

    const googleId = config.googleMeasurementId;
    const metaId = config.metaPixelId;
    const useGoogle = Boolean(googleId && page && googleAllowed);
    const useMeta = Boolean(metaId && page?.metaSafe && marketingAllowed);
    if (useGoogle && !this.googleRequested) this.loadGoogle(googleId);
    if (useMeta && !this.metaRequested) this.loadMeta();

    const googleActive = useGoogle && this.googleReady();
    if (this.googleRequested) browser[`ga-disable-${googleId}`] = !googleActive;
    if (googleActive !== this.googleActive) {
      browser.gtag?.('consent', 'update', { analytics_storage: googleActive ? 'granted' : 'denied' });
      this.googleActive = googleActive;
    }
    if (googleActive && page && this.googlePage !== page) {
      const referrer = this.googlePage?.location ?? this.referrerOrigin();
      const fields = { page_location: page.location, page_title: page.title, page_referrer: referrer };
      browser.gtag?.('set', fields);
      if (!this.googleConfigured) {
        browser.gtag?.('config', googleId, {
          send_page_view: false, allow_google_signals: false, allow_ad_personalization_signals: false
        });
        this.googleConfigured = true;
      }
      browser.gtag?.('event', 'page_view', fields);
      this.googlePage = page;
      for (const event of this.googleQueue.splice(0)) this.sendGoogleGrid(event, page);
    }

    const metaActive = useMeta && this.metaReady();
    if (metaActive !== this.metaActive) {
      browser.fbq?.('consent', metaActive ? 'grant' : 'revoke');
      this.metaActive = metaActive;
    }
    if (metaActive && page && this.metaPage !== page) {
      if (!this.metaConfigured) {
        browser.fbq?.('set', 'autoConfig', false, metaId);
        browser.fbq?.('init', metaId);
        this.metaConfigured = true;
      }
      browser.fbq?.('trackSingle', metaId, 'PageView');
      this.metaPage = page;
      for (const event of this.metaQueue.splice(0)) this.sendMetaGrid(event, metaId);
    }
  }

  private sendGoogleGrid(event: GridEvent, page: TrackedPage): void {
    try {
      this.browser?.gtag?.('event', gridEvents[event.action][0], { ...event.data, page_location: page.location });
    } catch {
      // Analytics must not interrupt a guess when a third-party tag fails.
    }
  }

  private sendMetaGrid(event: GridEvent, pixelId: string): void {
    try {
      this.browser?.fbq?.('trackSingleCustom', pixelId, gridEvents[event.action][1], event.data);
    } catch {
      // Analytics must not interrupt a guess when a third-party tag fails.
    }
  }

  private loadGoogle(id: string): void {
    const browser = this.browser!;
    this.googleRequested = true;
    browser[`ga-disable-${id}`] = true;
    browser.dataLayer ??= [];
    browser.gtag ??= function () { browser.dataLayer!.push(arguments); };
    browser.gtag('consent', 'default', {
      analytics_storage: 'denied', ad_storage: 'denied', ad_user_data: 'denied', ad_personalization: 'denied'
    });
    browser.gtag('js', new Date());
    this.appendTag(`https://www.googletagmanager.com/gtag/js?id=${id}`, () => this.googleReady.set(true));
  }

  private loadMeta(): void {
    const browser = this.browser!;
    this.metaRequested = true;
    const alreadyPresent = Boolean(browser.fbq?.callMethod);
    if (!browser.fbq) {
      const pixel: MetaPixel = Object.assign((...args: unknown[]) => {
        if (pixel.callMethod) pixel.callMethod.apply(pixel, args);
        else pixel.queue.push(args);
      }, { queue: [] as unknown[][], push: (...args: unknown[]) => pixel(...args),
        loaded: true, version: '2.0', disablePushState: true });
      browser.fbq = pixel;
      browser._fbq ??= pixel;
    }
    browser.fbq.disablePushState = true;
    browser.fbq('consent', 'revoke');
    if (alreadyPresent) queueMicrotask(() => this.metaReady.set(true));
    else this.appendTag('https://connect.facebook.net/en_US/fbevents.js', () => this.metaReady.set(true));
  }

  private appendTag(src: string, ready: () => void): void {
    const script = this.document.createElement('script');
    script.async = true;
    script.referrerPolicy = 'origin';
    script.src = src;
    script.addEventListener('load', ready, { once: true });
    script.addEventListener('error', () => script.remove(), { once: true });
    this.document.head.append(script);
  }

  private referrerOrigin(): string {
    try { return new URL(this.document.referrer).origin; } catch { return ''; }
  }
}
