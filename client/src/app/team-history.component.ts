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
  selectedPlayer: Player | null = null;
  searchTerm = '';
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
        this.changeDetector.markForCheck();
      },
      error: () => {
        this.suggestions = [];
        this.changeDetector.markForCheck();
      }
    });

    this.start();
  }

  start(): void {
    this.loading = true;
    this.errorMessage = '';
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
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
    this.searchTerms.next(value);
  }

  selectPlayer(player: Player): void {
    this.selectedPlayer = player;
    this.searchTerm = player.displayName;
    this.suggestions = [];
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
    this.loading = true;
    this.api.guessTeamHistory(this.round.roundId, playerId, skip).pipe(finalize(() => {
      this.loading = false;
      this.changeDetector.markForCheck();
    })).subscribe({
      next: round => {
        this.round = round;
        this.selectedPlayer = null;
        this.searchTerm = '';
        this.suggestions = [];
      },
      error: () => this.errorMessage = 'Your answer could not be submitted.'
    });
  }
}
