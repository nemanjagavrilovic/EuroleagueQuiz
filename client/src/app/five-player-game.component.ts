import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Injector, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, catchError, debounceTime, finalize, interval, map, of, switchMap } from 'rxjs';
import { DataApiService, FivePlayerGameRound, Player } from './data-api.service';
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
  private readonly searchTerms = new Subject<string>();
  private timer?: Subscription;
  private searchSubscription?: Subscription;

  round: FivePlayerGameRound | null = null;
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
  private timeoutSubmitted = false;

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
      if (term !== this.searchTerm || this.loading || this.round?.completed) return;
      this.searchingPlayers = false;
      this.searchCompleted = term.trim().length >= 2;
      this.suggestions = players.slice(0, 10);
      this.activeSuggestionIndex = -1;
      this.detector.markForCheck();
    });
    this.start();
  }

  ngOnDestroy(): void {
    this.timer?.unsubscribe();
    this.searchSubscription?.unsubscribe();
  }

  start(): void {
    if (this.loading) return;
    this.loading = true;
    this.errorMessage = '';
    this.feedback = '';
    this.round = null;
    this.revealedPlayerIds.clear();
    this.clearSearch();
    this.timer?.unsubscribe();
    this.api.startFivePlayerGame().pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
      next: round => this.setRound(round),
      error: () => this.errorMessage = 'The game could not be started. Import player and club data first.'
    });
  }

  onSearch(value: string): void {
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
    if (this.loading || !this.round || this.round.completed || this.secondsRemaining <= 0) return;
    this.focusSearch();
    this.selectedPlayer = player;
    this.searchTerm = player.displayName;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.submitGuess();
  }

  submitGuess(skip = false): void {
    if (!this.round || this.round.completed || this.loading || (!skip && (!this.selectedPlayer || this.secondsRemaining <= 0))) return;
    this.errorMessage = '';
    this.feedback = '';
    this.loading = true;
    const previousPlayerIds = new Set(this.round.players.map(player => player.id));
    const playerId = this.selectedPlayer?.id;
    this.clearSearch();
    this.api.guessFivePlayer(this.round.roundId, playerId, skip).pipe(finalize(() => { this.loading = false; this.focusSearch(); this.detector.markForCheck(); })).subscribe({
      next: round => {
        if (skip) {
          round.players
            .filter(player => !previousPlayerIds.has(player.id))
            .forEach(player => this.revealedPlayerIds.add(player.id));
        }
        this.round = round;
        this.secondsRemaining = round.secondsRemaining;
        this.feedback = skip ? 'Round revealed.' : round.lastGuessCorrect ? 'Correct player!' : 'This player did not play for this club, or was already guessed.';
        if (round.completed) this.timer?.unsubscribe();
      },
      error: () => { this.errorMessage = 'The answer could not be submitted. Please try again.'; }
    });
  }

  nextRound(): void {
    if (!this.round || !this.round.completed || this.round.gameCompleted || this.loading) return;
    this.loading = true;
    this.errorMessage = '';
    this.api.nextFivePlayerRound(this.round.roundId).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
      next: round => { this.revealedPlayerIds.clear(); this.setRound(round); },
      error: () => this.errorMessage = 'The next round could not be loaded. Please try again.'
    });
  }

  isRevealed(player: Player | undefined): boolean {
    return !!player && this.revealedPlayerIds.has(player.id);
  }

  private setRound(round: FivePlayerGameRound): void {
    this.round = round;
    this.secondsRemaining = round.secondsRemaining;
    this.timeoutSubmitted = false;
    this.feedback = '';
    this.clearSearch();
    this.timer?.unsubscribe();
    if (!round.completed) {
      this.timer = interval(1000).subscribe(() => {
        this.secondsRemaining = Math.max(0, this.secondsRemaining - 1);
        if (this.secondsRemaining === 0 && !this.timeoutSubmitted && !this.loading) {
          this.timeoutSubmitted = true;
          this.submitGuess(true);
        }
        this.detector.markForCheck();
      });
    }
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
