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

const quizEvents = {
  start: ['quiz_start', 'QuizStart'],
  complete: ['quiz_complete', 'QuizComplete'],
  reveal: ['quiz_reveal', 'QuizReveal'],
  answer: ['quiz_answer', 'QuizAnswer'],
  clue: ['quiz_clue_reveal', 'QuizClueReveal'],
  round_start: ['quiz_round_start', 'QuizRoundStart'],
  round_complete: ['quiz_round_complete', 'QuizRoundComplete']
} as const;
export type QuizGame = 'grid' | 'team-history' | 'missing-club' | 'career-order' | 'mystery-player' | 'five-player';
export type QuizMode = 'daily' | 'ranked' | 'practice';
export interface QuizEventDetails {
  difficulty?: string;
  result?: 'correct' | 'incorrect' | 'partial';
  reason?: 'skip' | 'incorrect' | 'reveal' | 'timeout' | 'answered';
  score?: number;
  clues_revealed?: number;
  round_number?: number;
  correct_players?: number;
}
type QuizEvent = {
  action: keyof typeof quizEvents;
  data: { game: QuizGame; mode: QuizMode } & QuizEventDetails;
};
const gamePaths: Record<QuizGame, readonly string[]> = {
  grid: ['/grid/easy', '/grid/normal', '/grid/hard', '/grid/legend'],
  'team-history': ['/team-history', '/game/history'],
  'missing-club': ['/game/missing-club', '/game/missing'],
  'career-order': ['/game/career-order', '/game/order'],
  'mystery-player': ['/game/mystery-player', '/game/mystery'],
  'five-player': ['/game/five-player']
};

const publicPages = new Set([
  '/', '/contact', '/scoring', '/leaderboard', ...Object.values(gamePaths).flat()
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
  private googleQueue: QuizEvent[] = [];
  private metaQueue: QuizEvent[] = [];

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

  grid(action: 'start' | 'complete' | 'reveal', mode: QuizMode, difficulty: string): void {
    this.quiz(action, 'grid', mode, { difficulty });
  }

  quiz(action: QuizEvent['action'], game: QuizGame, mode: QuizMode, details: QuizEventDetails = {}): void {
    const page = this.page();
    if (!page || !gamePaths[game]?.includes(page.path) || !supportedModes.has(mode)) return;
    // Explicitly copy supported fields; never pass through a round, guess or account object.
    const data: QuizEvent['data'] = { game, mode };
    const level = details.difficulty?.toLowerCase();
    if (game === 'grid') {
      if (!level || !gamePaths.grid.includes(`/grid/${level}`)) return;
      data.difficulty = level;
    }
    if (details.result && ['correct', 'incorrect', 'partial'].includes(details.result)) data.result = details.result;
    if (details.reason && ['skip', 'incorrect', 'reveal', 'timeout', 'answered'].includes(details.reason)) data.reason = details.reason;
    for (const [field, maximum] of [['score', 100], ['clues_revealed', 5], ['round_number', 5], ['correct_players', 25]] as const) {
      const value = details[field];
      if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= maximum) data[field] = value;
    }
    const event: QuizEvent = { action, data };
    const config = this.config();
    if (this.consent.analyticsAllowed() && (!config || config.googleMeasurementId)) {
      if (this.googleActive && this.googlePage === page) this.sendGoogleQuiz(event, page);
      else if (this.googleQueue.length < 64) this.googleQueue.push(event);
    }
    if (page.metaSafe && this.consent.marketingAllowed() && (!config || config.metaPixelId)) {
      if (this.metaActive && this.metaPage === page && config) this.sendMetaQuiz(event, config.metaPixelId);
      else if (this.metaQueue.length < 64) this.metaQueue.push(event);
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
    }
    if (googleActive && page && this.googlePage === page) {
      for (const event of this.googleQueue.splice(0)) this.sendGoogleQuiz(event, page);
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
    }
    if (metaActive && page && this.metaPage === page) {
      for (const event of this.metaQueue.splice(0)) this.sendMetaQuiz(event, metaId);
    }
  }

  private sendGoogleQuiz(event: QuizEvent, page: TrackedPage): void {
    try {
      this.browser?.gtag?.('event', quizEvents[event.action][0], { ...event.data, page_location: page.location });
    } catch {
      // Analytics must not interrupt a guess when a third-party tag fails.
    }
  }

  private sendMetaQuiz(event: QuizEvent, pixelId: string): void {
    try {
      this.browser?.fbq?.('trackSingleCustom', pixelId, quizEvents[event.action][1], event.data);
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
