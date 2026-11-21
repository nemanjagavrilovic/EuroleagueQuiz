import { DOCUMENT, NgIf } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Input, afterNextRender, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AnalyticsService, QuizGame } from '../../analytics.service';
import { AuthService } from '../../auth.service';

@Component({
  selector: 'app-guest-game-invitation',
  standalone: true,
  imports: [NgIf, RouterLink],
  templateUrl: './guest-game-invitation.component.html',
  styleUrl: './guest-game-invitation.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GuestGameInvitationComponent {
  @Input({ required: true }) game!: QuizGame;
  @Input() difficulty = '';
  readonly auth = inject(AuthService);
  private readonly analytics = inject(AnalyticsService);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private viewed = false;

  get returnUrl(): string {
    const level = this.difficulty.toLowerCase();
    return `/play/${this.game}` + (this.game === 'grid' && ['easy', 'normal', 'hard', 'legend'].includes(level)
      ? `?difficulty=${level}` : '');
  }

  constructor() {
    afterNextRender(() => {
      if (this.auth.user()) return;
      const browser = this.document.defaultView;
      if (browser) {
        const bounds = this.element.nativeElement.getBoundingClientRect();
        // Scroll anchoring can keep the player down at their last answer when
        // the banner is inserted above the finished game. Reveal it once.
        if (bounds.top < 110 || bounds.bottom > browser.innerHeight - 12) {
          this.element.nativeElement.scrollIntoView({
            block: 'start',
            behavior: browser.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
          });
        }
      }
      if (!browser || !('IntersectionObserver' in browser)) { this.trackView(); return; }
      // Count a view when the invitation reaches the viewport, not merely when
      // a completed attempt is restored below the fold.
      const observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting && entry.intersectionRatio >= 0.25)) { this.trackView(); observer.disconnect(); }
      }, { threshold: 0.25 });
      observer.observe(this.element.nativeElement);
      this.destroyRef.onDestroy(() => observer.disconnect());
    });
  }

  register(): void {
    if (this.auth.user()) return;
    this.trackView();
    this.analytics.quiz('signup_prompt_click', this.game, 'daily', { difficulty: this.difficulty });
  }

  private trackView(): void {
    if (this.viewed || this.auth.user()) return;
    this.viewed = true;
    this.analytics.quiz('signup_prompt_view', this.game, 'daily', { difficulty: this.difficulty });
  }
}
