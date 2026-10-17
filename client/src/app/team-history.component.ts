import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, finalize, of, switchMap } from 'rxjs';
import { DataApiService, Player, Team, TeamHistoryRound } from './data-api.service';

@Component({
  selector: 'app-team-history',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink],
  templateUrl: './team-history.component.html',
  styleUrl: './team-history.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamHistoryComponent implements OnInit {
  private readonly api = inject(DataApiService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly searchTerms = new Subject<string>();

  round: TeamHistoryRound | null = null;
  suggestions: Player[] = [];
  activeSuggestionIndex = -1;
  selectedPlayer: Player | null = null;
  searchTerm = '';
  answerFeedback = '';
  loading = false;
  errorMessage = '';

  ngOnInit(): void {
    this.searchTerms.pipe(
      debounceTime(180),
      distinctUntilChanged(),
      switchMap(term => {
        const value = term.trim();
        if (value.length < 2) return of([] as Player[]);
        return this.api.searchPlayers(value).pipe(catchError(() => of([] as Player[])));
      })
    ).subscribe({
      next: players => {
        this.suggestions = players.slice(0, 12);
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
    this.loading = true;
    this.errorMessage = '';
    this.answerFeedback = '';
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.api.startTeamHistoryGame().pipe(finalize(() => {
      this.loading = false;
      this.changeDetector.markForCheck();
    })).subscribe({
      next: round => this.round = round,
      error: () => this.errorMessage = 'The game could not be started. Make sure player and club data has been imported.'
    });
  }

  onSearch(value: string): void {
    this.searchTerm = value;
    this.selectedPlayer = null;
    this.activeSuggestionIndex = -1;
    this.searchTerms.next(value);
  }

  selectPlayer(player: Player): void {
    this.selectedPlayer = player;
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
      this.changeDetector.markForCheck();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeSuggestionIndex = this.activeSuggestionIndex <= 0
        ? this.suggestions.length - 1
        : this.activeSuggestionIndex - 1;
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
    if (!this.round) return;
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next('');
    this.loading = true;
    this.api.guessTeamHistory(this.round.roundId, playerId, skip).pipe(finalize(() => {
      this.loading = false;
      this.changeDetector.markForCheck();
    })).subscribe({
      next: round => {
        this.round = round;
        this.answerFeedback = skip
          ? 'Another club was revealed.'
          : round.completed
            ? ''
            : 'Not this player — another club was revealed.';
      },
      error: () => this.errorMessage = 'Your answer could not be submitted.'
    });
  }
}
