import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, HostListener, Injector, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, Subscription, catchError, debounceTime, finalize, interval, map, of, switchMap } from 'rxjs';
import { DailyFivePlayerAttempt, DataApiService, FivePlayerGameRound, Player } from './data-api.service';
import { createGuestUuid } from './guest-identity';
import { BasketballLoaderComponent } from './basketball-loader.component';
import { ShotClockComponent } from './shot-clock.component';

@Component({
  selector: 'app-five-player-game',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, BasketballLoaderComponent, ShotClockComponent],
  templateUrl: './five-player-game.component.html',
  styleUrl: './five-player-game.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FivePlayerGameComponent implements OnInit, OnDestroy {
  @ViewChild('playerSearch') private searchInput?: ElementRef<HTMLInputElement>;
  @ViewChild('suggestionsList') private suggestionsList?: ElementRef<HTMLDivElement>;
  private readonly injector = inject(Injector);
  private readonly api = inject(DataApiService);
  private readonly detector = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly searchTerms = new Subject<string>();
  private timer?: Subscription;
  private searchSubscription?: Subscription;
  private destroyed = false;

  round: FivePlayerGameRound | null = null;
  dailyAttempt: DailyFivePlayerAttempt | null = null;
  restoring = false;
  private localDeadline = 0;
  private nextSyncAt = 0;
  suggestions: Player[] = [];
  selectedPlayer: Player | null = null;
  activeSuggestionIndex = -1;
  searchTerm = '';
  feedback = '';
  errorMessage = '';
  loading = false;
  searchingPlayers = false;
  searchCompleted = false;
  secondsRemaining = 24;
  readonly answerSlots = [0, 1, 2, 3, 4];
  readonly revealedPlayerIds = new Set<number>();
  private readonly practiceGuessedPlayerIds = new Set<number>();
  private practiceRoundNumber = 0;
  private timeoutSubmitted = false;
  readonly mode = this.route.snapshot.queryParamMap.get('mode') || 'daily';
  readonly ranked = this.mode === 'ranked';
  readonly practice = this.mode === 'practice';

  get roundGuesses(): number { return this.round?.players.filter(player => !this.revealedPlayerIds.has(player.id)).length ?? 0; }
  get timePercent(): number { return Math.min(100, Math.max(0, this.secondsRemaining / (this.round?.secondsPerRound || 24) * 100)); }

  ngOnInit(): void {
    this.searchSubscription = this.searchTerms.pipe(
      debounceTime(180),
      switchMap(term => term.trim().length < 2 ? of({ term, players: [] as Player[] }) : this.api.searchPlayers(term.trim()).pipe(
        map(players => ({ term, players })),
        catchError(() => {
          if (term === this.searchTerm) this.errorMessage = 'Player search is currently unavailable.';
          return of({ term, players: [] as Player[] });
        })
      ))
    ).subscribe(({ term, players }) => {
      if (term !== this.searchTerm || this.loading || this.restoring || this.round?.completed || this.secondsRemaining <= 0) return;
      this.searchingPlayers = false;
      this.searchCompleted = term.trim().length >= 2;
      this.suggestions = players.filter(player => !this.round?.players.some(answer => answer.id === player.id)).slice(0, 10);
      this.activeSuggestionIndex = -1;
      this.detector.markForCheck();
    });
    this.start();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.timer?.unsubscribe();
    this.searchSubscription?.unsubscribe();
  }

  start(): void {
    if (this.loading || this.restoring) return;
    this.loading = true;
    this.errorMessage = '';
    this.feedback = '';
    this.round = null;
    this.revealedPlayerIds.clear();
    this.clearSearch();
    this.timer?.unsubscribe();
    const sentAt = performance.now();
    if (this.practice) {
      this.api.startFivePlayerGame().pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
        next: round => this.applyPracticeRound(round),
        error: () => this.errorMessage = 'The practice game could not be started.'
      });
      return;
    }
    this.api.startDailyFivePlayer(this.ranked).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
      next: attempt => this.applyAttempt(attempt, sentAt),
      error: () => this.errorMessage = 'The game could not be started. Import player and club data first.'
    });
  }

  onSearch(value: string): void {
    if (this.loading || this.restoring || this.round?.completed || this.secondsRemaining <= 0) return;
    this.searchTerm = value;
    this.errorMessage = '';
    this.searchingPlayers = value.trim().length >= 2;
    this.searchCompleted = false;
    this.selectedPlayer = null;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next(value);
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.suggestions = [];
      this.activeSuggestionIndex = -1;
      this.detector.markForCheck();
      return;
    }
    if (this.suggestions.length === 0) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeSuggestionIndex = (this.activeSuggestionIndex + 1) % this.suggestions.length;
      this.scrollActiveSuggestion();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeSuggestionIndex = this.activeSuggestionIndex <= 0 ? this.suggestions.length - 1 : this.activeSuggestionIndex - 1;
      this.scrollActiveSuggestion();
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.selectPlayer(this.suggestions[this.activeSuggestionIndex >= 0 ? this.activeSuggestionIndex : 0]);
    }
    this.detector.markForCheck();
  }

  selectPlayer(player: Player): void {
    if (this.loading || this.restoring || !this.round || this.round.completed || this.secondsRemaining <= 0) return;
    this.focusSearch();
    this.selectedPlayer = player;
    this.searchTerm = player.displayName;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.submitGuess();
  }

  submitGuess(skip = false): void {
    if (!this.round || (!this.practice && !this.dailyAttempt) || this.round.completed || this.loading || this.restoring || (!skip && (!this.selectedPlayer || this.secondsRemaining <= 0))) return;
    this.errorMessage = '';
    this.feedback = '';
    this.loading = true;
    const playerId = this.selectedPlayer?.id;
    this.clearSearch();
    const sentAt = performance.now();
    if (this.practice) {
      this.api.guessFivePlayer(this.round.roundId, playerId, skip).pipe(finalize(() => {
        this.loading = false; this.focusSearch(); this.detector.markForCheck();
      })).subscribe({
        next: round => {
          if (round.lastGuessCorrect && playerId) this.practiceGuessedPlayerIds.add(playerId);
          this.applyPracticeRound(round);
        },
        error: () => this.errorMessage = 'The answer could not be checked.'
      });
      return;
    }
    const challengeId = this.dailyAttempt!.challengeId;
    this.api.guessDailyFivePlayer(challengeId, this.round.roundNumber, createGuestUuid(), playerId, skip, this.ranked).pipe(finalize(() => { this.loading = false; this.focusSearch(); this.detector.markForCheck(); })).subscribe({
      next: attempt => this.applyAttempt(attempt, sentAt),
      error: () => { this.errorMessage = 'The answer could not be confirmed. Restoring saved progress…'; this.syncAttempt(); }
    });
  }

  nextRound(): void {
    if (!this.round || (!this.practice && !this.dailyAttempt) || !this.round.completed || this.round.gameCompleted || this.loading || this.restoring) return;
    this.loading = true;
    this.errorMessage = '';
    const sentAt = performance.now();
    if (this.practice) {
      this.api.nextFivePlayerRound(this.round.roundId).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
        next: round => this.applyPracticeRound(round), error: () => this.errorMessage = 'The next practice round could not be loaded.'
      });
      return;
    }
    const challengeId = this.dailyAttempt!.challengeId;
    this.api.nextDailyFivePlayer(challengeId, this.round.roundNumber, createGuestUuid(), this.ranked).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
      next: attempt => this.applyAttempt(attempt, sentAt),
      error: () => { this.errorMessage = 'The next round could not be confirmed. Restoring saved progress…'; this.syncAttempt(); }
    });
  }

  isRevealed(player: Player | undefined): boolean {
    return !!player && this.revealedPlayerIds.has(player.id);
  }

  private applyAttempt(attempt: DailyFivePlayerAttempt, sentAt: number): void {
    if (this.destroyed) return;
    this.dailyAttempt = attempt;
    this.round = attempt.round;
    this.revealedPlayerIds.clear();
    attempt.revealedPlayerIds.forEach(id => this.revealedPlayerIds.add(id));
    const remainingMs = attempt.deadlineUtc ? Date.parse(attempt.deadlineUtc) - Date.parse(attempt.serverNowUtc) : 0;
    this.localDeadline = performance.now() + Math.max(0, remainingMs - (performance.now() - sentAt) / 2);
    this.secondsRemaining = attempt.round.completed ? 0 : Math.min(attempt.round.secondsPerRound, Math.max(0, Math.ceil((this.localDeadline - performance.now()) / 1000)));
    this.timeoutSubmitted = false;
    this.nextSyncAt = 0;
    this.feedback = attempt.feedback || '';
    this.errorMessage = '';
    this.clearSearch();
    this.timer?.unsubscribe();
    if (!attempt.round.completed) {
      this.timer = interval(250).subscribe(() => {
        this.secondsRemaining = Math.min(attempt.round.secondsPerRound, Math.max(0, Math.ceil((this.localDeadline - performance.now()) / 1000)));
        if (this.secondsRemaining === 0 && !this.timeoutSubmitted && !this.loading && !this.restoring && performance.now() >= this.nextSyncAt) {
          this.timeoutSubmitted = true;
          this.suggestions = [];
          // Synchronize the server deadline; a client clock must not surrender a round.
          if (this.practice) this.submitGuess(true); else this.syncAttempt();
        }
        this.detector.markForCheck();
      });
    }
  }

  @HostListener('window:focus')
  @HostListener('document:visibilitychange')
  onResume(): void {
    if (!this.practice && document.visibilityState === 'visible' && this.round && !this.round.gameCompleted && !this.loading && !this.restoring) this.syncAttempt();
  }

  private syncAttempt(): void {
    if (this.destroyed || this.restoring) return;
    this.restoring = true;
    const sentAt = performance.now();
    this.api.startDailyFivePlayer(this.ranked).pipe(finalize(() => {
      this.restoring = false; this.focusSearch(); this.detector.markForCheck();
    })).subscribe({
      next: attempt => this.applyAttempt(attempt, sentAt),
      error: () => {
        this.timeoutSubmitted = false;
        this.nextSyncAt = performance.now() + 5000;
        this.errorMessage = 'Saved progress could not be loaded. Check your connection; the round clock continues on the server.';
      }
    });
  }

  private applyPracticeRound(round: FivePlayerGameRound): void {
    if (this.destroyed) return;
    if (round.roundNumber !== this.practiceRoundNumber) {
      this.practiceRoundNumber = round.roundNumber;
      this.practiceGuessedPlayerIds.clear();
    }
    this.dailyAttempt = null; this.round = round; this.revealedPlayerIds.clear();
    if (round.completed) {
      round.players.filter(player => !this.practiceGuessedPlayerIds.has(player.id)).forEach(player => this.revealedPlayerIds.add(player.id));
    }
    this.secondsRemaining = round.completed ? 0 : round.secondsRemaining;
    this.localDeadline = performance.now() + this.secondsRemaining * 1000;
    this.feedback = ''; this.errorMessage = ''; this.clearSearch(); this.timer?.unsubscribe();
    if (!round.completed) this.timer = interval(250).subscribe(() => {
      this.secondsRemaining = Math.max(0, Math.ceil((this.localDeadline - performance.now()) / 1000));
      if (this.secondsRemaining === 0 && !this.loading) this.submitGuess(true);
      this.detector.markForCheck();
    });
  }

  private clearSearch(): void {
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next('');
    this.focusSearch();
  }

  private focusSearch(): void {
    if (this.destroyed || !this.round || this.round.completed || this.secondsRemaining <= 0) return;
    this.searchInput?.nativeElement.focus({ preventScroll: true });
    afterNextRender(() => {
      if (this.round && !this.round.completed && this.secondsRemaining > 0) this.searchInput?.nativeElement.focus({ preventScroll: true });
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
