import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Injector, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, finalize, map, of, switchMap } from 'rxjs';
import { DailyTeamHistoryAttempt, DataApiService, Player, TeamHistoryRound } from './data-api.service';
import { createGuestUuid } from './guest-identity';
import { BasketballLoaderComponent } from './basketball-loader.component';

@Component({
  selector: 'app-team-history',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, BasketballLoaderComponent],
  templateUrl: './team-history.component.html',
  styleUrl: './team-history.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamHistoryComponent implements OnInit {
  @ViewChild('playerSearch') private searchInput?: ElementRef<HTMLInputElement>;
  @ViewChild('suggestionsList') private suggestionsList?: ElementRef<HTMLDivElement>;
  private readonly injector = inject(Injector);
  private readonly api = inject(DataApiService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly searchTerms = new Subject<string>();

  round: TeamHistoryRound | null = null;
  dailyAttempt: DailyTeamHistoryAttempt | null = null;
  suggestions: Player[] = [];
  activeSuggestionIndex = -1;
  selectedPlayer: Player | null = null;
  searchTerm = '';
  answerFeedback = '';
  loading = false;
  restoring = false;
  errorMessage = '';
  searchingPlayers = false;
  searchCompleted = false;
  readonly mode = this.route.snapshot.queryParamMap.get('mode') || 'daily';
  readonly ranked = this.mode === 'ranked';
  readonly practice = this.mode === 'practice';

  get clueSlots(): number[] { return Array.from({ length: this.round?.totalTeams ?? 4 }, (_, index) => index); }

  ngOnInit(): void {
    this.searchTerms.pipe(
      debounceTime(180),
      switchMap(term => {
        const value = term.trim();
        if (value.length < 2) return of({ term, players: [] as Player[] });
        return this.api.searchPlayers(value).pipe(
          map(players => ({ term, players })),
          catchError(() => {
            if (term === this.searchTerm) this.errorMessage = 'Player search is currently unavailable.';
            return of({ term, players: [] as Player[] });
          })
        );
      })
    ).subscribe({
      next: ({ term, players }) => {
        if (term !== this.searchTerm || this.loading || this.round?.completed) return;
        this.searchingPlayers = false;
        this.searchCompleted = term.trim().length >= 2;
        this.suggestions = players.filter(player => !this.dailyAttempt?.usedPlayerIds.includes(player.id)).slice(0, 12);
        this.activeSuggestionIndex = -1;
        this.changeDetector.markForCheck();
      },
      error: () => {
        this.suggestions = [];
        this.activeSuggestionIndex = -1;
        this.changeDetector.markForCheck();
      }
    });

    this.start();
  }

  start(): void {
    if (this.loading || this.restoring) return;
    this.round = null;
    this.loading = true;
    this.errorMessage = '';
    this.answerFeedback = '';
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next('');
    const request: Observable<TeamHistoryRound | DailyTeamHistoryAttempt> = this.practice
      ? this.api.startTeamHistoryGame()
      : this.api.startDailyTeamHistory(this.ranked);
    request.pipe(finalize(() => {
      this.loading = false;
      this.changeDetector.markForCheck();
    })).subscribe({
      next: value => this.practice ? this.applyPracticeRound(value as TeamHistoryRound) : this.applyAttempt(value as DailyTeamHistoryAttempt),
      error: () => this.errorMessage = 'The game could not be started. Make sure player and club data has been imported.'
    });
  }

  onSearch(value: string): void {
    if (this.loading || this.restoring || this.round?.completed) return;
    this.searchTerm = value;
    this.errorMessage = '';
    this.searchingPlayers = value.trim().length >= 2;
    this.searchCompleted = false;
    this.selectedPlayer = null;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next(value);
  }

  selectPlayer(player: Player): void {
    if (this.loading || this.restoring || !this.round || this.round.completed || this.dailyAttempt?.usedPlayerIds.includes(player.id)) return;
    this.focusSearch();
    this.selectedPlayer = player;
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.searchTerm = player.displayName;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.guess();
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.suggestions = [];
      this.activeSuggestionIndex = -1;
      this.changeDetector.markForCheck();
      return;
    }

    if (this.suggestions.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeSuggestionIndex = (this.activeSuggestionIndex + 1) % this.suggestions.length;
      this.scrollActiveSuggestion();
      this.changeDetector.markForCheck();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeSuggestionIndex = this.activeSuggestionIndex <= 0
        ? this.suggestions.length - 1
        : this.activeSuggestionIndex - 1;
      this.scrollActiveSuggestion();
      this.changeDetector.markForCheck();
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.selectPlayer(this.suggestions[this.activeSuggestionIndex >= 0 ? this.activeSuggestionIndex : 0]);
    }
  }

  guess(): void {
    if (!this.round || !this.selectedPlayer || this.round.completed) return;
    this.submitGuess(this.selectedPlayer.id, false);
  }

  skip(): void {
    if (!this.round || this.round.completed) return;
    this.submitGuess(undefined, true);
  }

  private submitGuess(playerId: number | undefined, skip: boolean): void {
    if (!this.round || (!this.practice && !this.dailyAttempt) || this.loading || this.restoring || this.round.completed) return;
    this.errorMessage = '';
    this.answerFeedback = '';
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next('');
    this.loading = true;
    if (this.practice) {
      this.api.guessTeamHistory(this.round.roundId, playerId, skip).pipe(finalize(() => {
        this.loading = false; this.focusSearch(); this.changeDetector.markForCheck();
      })).subscribe({ next: round => this.applyPracticeRound(round), error: () => this.errorMessage = 'The answer could not be checked.' });
      return;
    }
    const challengeId = this.dailyAttempt!.challengeId;
    this.api.guessDailyTeamHistory(challengeId, createGuestUuid(), playerId, skip, this.ranked).pipe(finalize(() => {
      this.loading = false;
      this.focusSearch();
      this.changeDetector.markForCheck();
    })).subscribe({
      next: attempt => this.applyAttempt(attempt),
      error: () => {
        this.restoring = true;
        this.errorMessage = 'Your answer could not be confirmed. Restoring saved progress…';
        this.api.startDailyTeamHistory(this.ranked).pipe(finalize(() => {
          this.restoring = false; this.focusSearch(); this.changeDetector.markForCheck();
        })).subscribe({
          next: attempt => { this.applyAttempt(attempt); this.changeDetector.markForCheck(); },
          error: () => { this.errorMessage = 'Saved progress could not be loaded. Refresh before submitting another answer.'; this.changeDetector.markForCheck(); }
        });
      }
    });
  }

  private applyAttempt(attempt: DailyTeamHistoryAttempt): void {
    this.dailyAttempt = attempt;
    this.round = attempt.round;
    this.answerFeedback = attempt.feedback || '';
    this.errorMessage = '';
    this.searchTerm = '';
    this.searchCompleted = false;
    this.searchingPlayers = false;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.selectedPlayer = null;
    this.searchTerms.next('');
  }

  private applyPracticeRound(round: TeamHistoryRound): void {
    this.dailyAttempt = null; this.round = round; this.answerFeedback = '';
    this.errorMessage = ''; this.searchTerm = ''; this.searchCompleted = false; this.searchingPlayers = false;
    this.suggestions = []; this.activeSuggestionIndex = -1; this.selectedPlayer = null; this.searchTerms.next('');
  }

  private focusSearch(): void {
    if (!this.round || this.round.completed) return;
    this.searchInput?.nativeElement.focus({ preventScroll: true });
    afterNextRender(() => {
      if (this.round && !this.round.completed) this.searchInput?.nativeElement.focus({ preventScroll: true });
    }, { injector: this.injector });
  }

  private scrollActiveSuggestion(): void {
    afterNextRender(() => {
      const list = this.suggestionsList?.nativeElement;
      const option = list?.children[this.activeSuggestionIndex] as HTMLElement | undefined;
      if (!list || !option) return;
      const top = option.getBoundingClientRect().top - list.getBoundingClientRect().top + list.scrollTop;
      if (top < list.scrollTop) list.scrollTop = top;
      else if (top + option.offsetHeight > list.scrollTop + list.clientHeight) list.scrollTop = top + option.offsetHeight - list.clientHeight;
    }, { injector: this.injector });
  }
}
