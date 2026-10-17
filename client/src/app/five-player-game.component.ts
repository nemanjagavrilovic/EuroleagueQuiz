import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, catchError, debounceTime, distinctUntilChanged, finalize, interval, of, switchMap } from 'rxjs';
import { DataApiService, FivePlayerGameRound, Player } from './data-api.service';

@Component({
  selector: 'app-five-player-game',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink],
  templateUrl: './five-player-game.component.html',
  styleUrl: './five-player-game.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FivePlayerGameComponent implements OnInit, OnDestroy {
  private readonly api = inject(DataApiService);
  private readonly detector = inject(ChangeDetectorRef);
  private readonly searchTerms = new Subject<string>();
  private timer?: Subscription;

  round: FivePlayerGameRound | null = null;
  suggestions: Player[] = [];
  selectedPlayer: Player | null = null;
  activeSuggestionIndex = -1;
  searchTerm = '';
  feedback = '';
  errorMessage = '';
  loading = false;
  secondsRemaining = 24;
  readonly answerSlots = [0, 1, 2, 3, 4];
  readonly revealedPlayerIds = new Set<number>();
  private timeoutSubmitted = false;

  ngOnInit(): void {
    this.searchTerms.pipe(
      debounceTime(180),
      distinctUntilChanged(),
      switchMap(term => term.trim().length < 2 ? of([] as Player[]) : this.api.searchPlayers(term).pipe(catchError(() => of([] as Player[]))))
    ).subscribe(players => {
      this.suggestions = players.slice(0, 10);
      this.activeSuggestionIndex = -1;
      this.detector.markForCheck();
    });
    this.start();
  }

  ngOnDestroy(): void {
    this.timer?.unsubscribe();
  }

  start(): void {
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
    this.selectedPlayer = null;
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
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeSuggestionIndex = this.activeSuggestionIndex <= 0 ? this.suggestions.length - 1 : this.activeSuggestionIndex - 1;
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.selectPlayer(this.suggestions[this.activeSuggestionIndex >= 0 ? this.activeSuggestionIndex : 0]);
    }
    this.detector.markForCheck();
  }

  selectPlayer(player: Player): void {
    this.selectedPlayer = player;
    this.searchTerm = player.displayName;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.submitGuess();
  }

  submitGuess(skip = false): void {
    if (!this.round || this.round.completed || (!skip && !this.selectedPlayer)) return;
    this.loading = true;
    const previousPlayerIds = new Set(this.round.players.map(player => player.id));
    const playerId = this.selectedPlayer?.id;
    this.clearSearch();
    this.api.guessFivePlayer(this.round.roundId, playerId, skip).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
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
      error: () => this.errorMessage = 'The answer could not be submitted.'
    });
  }

  nextRound(): void {
    if (!this.round || !this.round.completed || this.round.gameCompleted) return;
    this.loading = true;
    this.revealedPlayerIds.clear();
    this.api.nextFivePlayerRound(this.round.roundId).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe(round => this.setRound(round));
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
        if (this.secondsRemaining === 0 && !this.timeoutSubmitted) {
          this.timeoutSubmitted = true;
          this.submitGuess(true);
        }
        this.detector.markForCheck();
      });
    }
  }

  private clearSearch(): void {
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next('');
  }
}
