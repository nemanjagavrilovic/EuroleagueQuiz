import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { DataApiService, LeaderboardEntry, LeaderboardResponse } from './data-api.service';
import { BasketballLoaderComponent } from './basketball-loader.component';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, BasketballLoaderComponent],
  templateUrl: './leaderboard.component.html',
  styleUrl: './leaderboard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LeaderboardComponent implements OnInit {
  private readonly api = inject(DataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly detector = inject(ChangeDetectorRef);

  readonly periods = [
    { id: 'today', label: 'Today' },
    { id: 'week', label: 'This week' },
    { id: 'month', label: 'This month' },
    { id: 'all-time', label: 'All time' }
  ];
  readonly games = [
    { id: 'all', label: 'All games' },
    { id: 'grid', label: 'Player Grid' },
    { id: 'team-history', label: 'Team History' },
    { id: 'missing-club', label: 'Missing Club' },
    { id: 'career-order', label: 'Career Order' },
    { id: 'mystery-player', label: 'Mystery Player' },
    { id: 'five-player', label: 'Starting Five' }
  ];
  period = 'today';
  game = 'all';
  board: LeaderboardResponse | null = null;
  page = 1;
  readonly pageSize = 20;
  loading = false;
  errorMessage = '';

  get currentUserOutsideTop(): LeaderboardEntry | null {
    const current = this.board?.currentUser ?? null;
    return current && !this.board?.entries.some(entry => entry.isCurrentUser) ? current : null;
  }
  get dateLabel(): string {
    if (!this.board) return '';
    if (this.period === 'today') return this.board.toDate;
    if (this.period === 'all-time') return 'All ranked results';
    return `${this.board.fromDate} — ${this.board.toDate}`;
  }

  ngOnInit(): void {
    const requestedPeriod = this.route.snapshot.queryParamMap.get('period');
    const requestedGame = this.route.snapshot.queryParamMap.get('game');
    const requestedPage = Number(this.route.snapshot.queryParamMap.get('page'));
    if (this.periods.some(item => item.id === requestedPeriod)) this.period = requestedPeriod!;
    if (this.games.some(item => item.id === requestedGame)) this.game = requestedGame!;
    if (Number.isInteger(requestedPage) && requestedPage > 1) this.page = requestedPage;
    this.load();
  }

  selectPeriod(period: string): void { if (this.period !== period) { this.period = period; this.page = 1; this.updateAndLoad(); } }
  selectGame(event: Event): void {
    const game = (event.target as HTMLSelectElement).value;
    if (this.game !== game) { this.game = game; this.page = 1; this.updateAndLoad(); }
  }

  goToPage(page: number): void {
    if (!this.board || page < 1 || page > this.board.totalPages || page === this.page) return;
    this.page = page;
    this.updateAndLoad();
  }

  get pageNumbers(): number[] {
    if (!this.board) return [];
    const start = Math.max(1, Math.min(this.page - 2, this.board.totalPages - 4));
    const end = Math.min(this.board.totalPages, start + 4);
    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  }

  private updateAndLoad(): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { period: this.period, game: this.game, page: this.page > 1 ? this.page : null }, replaceUrl: true });
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.errorMessage = '';
    this.api.getLeaderboard(this.period, this.game, this.page, this.pageSize).pipe(finalize(() => {
      this.loading = false;
      this.detector.markForCheck();
    })).subscribe({
      next: board => { this.board = board; this.page = board.page; },
      error: () => { this.board = null; this.errorMessage = 'The leaderboard could not be loaded. Please try again.'; }
    });
  }
}
