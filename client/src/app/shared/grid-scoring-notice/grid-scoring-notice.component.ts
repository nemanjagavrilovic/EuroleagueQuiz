import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map, timer } from 'rxjs';
import { environment } from '../../../environments/environment';

interface GridScoringNotice {
  id: string;
  expiresAtUtc: string;
}

@Component({
  selector: 'app-grid-scoring-notice',
  standalone: true,
  imports: [NgIf],
  templateUrl: './grid-scoring-notice.component.html',
  styleUrl: './grid-scoring-notice.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GridScoringNoticeComponent {
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dismissed = signal(false);
  private readonly now = signal(Date.now());
  readonly notice = signal<GridScoringNotice | null>(null);
  private readonly path = toSignal(this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    map(event => event.urlAfterRedirects.split(/[?#]/)[0])
  ), { initialValue: this.router.url.split(/[?#]/)[0] });
  readonly visible = computed(() => {
    const notice = this.notice();
    return !!notice && !this.dismissed() && ['/', '/leaderboard'].includes(this.path()) &&
      this.now() < Date.parse(notice.expiresAtUtc);
  });

  constructor() {
    const url = environment.apiUrl.replace(/\/+$/, '') + '/config/public';
    this.http.get<{ gridScoringNotice?: GridScoringNotice | null }>(url).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: configuration => {
        const notice = configuration.gridScoringNotice;
        if (!notice || !Number.isFinite(Date.parse(notice.expiresAtUtc))) return;
        this.notice.set(notice);
        this.now.set(Date.now());
        try {
          this.dismissed.set(localStorage.getItem(this.storageKey(notice)) === 'dismissed');
        } catch {
          // The close button also works when browser storage is unavailable.
        }
        timer(Math.max(0, Date.parse(notice.expiresAtUtc) - Date.now()))
          .pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.now.set(Date.now()));
      },
      error: () => this.notice.set(null)
    });
  }

  dismiss(): void {
    this.dismissed.set(true);
    const notice = this.notice();
    if (!notice) return;
    try {
      localStorage.setItem(this.storageKey(notice), 'dismissed');
    } catch {
      // Dismissal lasts for this visit if storage is unavailable.
    }
  }

  private storageKey(notice: GridScoringNotice): string {
    return 'pickandroll.notice.' + notice.id;
  }
}
