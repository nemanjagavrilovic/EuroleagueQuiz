import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Injector, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, finalize, map, switchMap } from 'rxjs/operators';
import { DailyGridAttempt, DataApiService, GridCell, GridDefinition, Player } from './data-api.service';
import { BasketballLoaderComponent } from './basketball-loader.component';
import { createGuestUuid } from './guest-identity';

@Component({
  selector: 'app-grid-game',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, BasketballLoaderComponent],
  templateUrl: './grid-game.component.html',
  styleUrl: './grid-game.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GridGameComponent {
  @ViewChild('playerSearch') private searchInput?: ElementRef<HTMLInputElement>;
  @ViewChild('suggestionsList') private suggestionsList?: ElementRef<HTMLUListElement>;
  private readonly injector = inject(Injector);
  private readonly api = inject(DataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly playerSearch$ = new Subject<string>();
  grid: GridDefinition | null = null;
  gridLoading = true;
  gridError = '';
  selectedDifficulty = this.toTitleCase(this.route.snapshot.paramMap.get('difficulty') ?? 'normal');
  playerResults: Player[] = [];
  activeResultIndex = -1;
  answers: (Player | null)[] = Array(9).fill(null);
  selectedPlayer: Player | null = null;
  searchTerm = '';
  pendingCells: GridCell[] = [];
  revealedAnswers: (Player | null)[] = Array(9).fill(null);
  searchError = '';
  searchingPlayers = false;
  searchCompleted = false;
  checkingPlayer = false;
  revealing = false;
  gridRevealed = false;
  dailyAttempt: DailyGridAttempt | null = null;
  get attemptCompleted(): boolean { return this.dailyAttempt?.completed ?? false; }

  get guessedCount(): number { return this.answers.filter(player => player !== null).length; }

  constructor() {
    this.loadGrid();
    this.playerSearch$.pipe(
      debounceTime(250),
      switchMap(term => term.trim().length < 2
        ? of({ term, players: [] as Player[] })
        : this.api.searchPlayers(term.trim()).pipe(
          map(players => ({ term, players })),
          catchError(() => {
            if (term === this.searchTerm) this.searchError = 'Player search is currently unavailable.';
            return of({ term, players: [] as Player[] });
          })))
    ).subscribe(({ term, players }) => {
      if (term !== this.searchTerm || this.checkingPlayer || this.revealing || this.attemptCompleted) return;
      this.searchingPlayers = false;
      this.searchCompleted = term.trim().length >= 2;
      this.playerResults = players.filter(player => !this.isPlayerUsed(player.id));
      this.activeResultIndex = -1;
      this.changeDetector.markForCheck();
    });
  }

  private loadGrid(): void {
    this.api.startDailyGrid(this.selectedDifficulty).subscribe({
      next: attempt => { this.applyAttempt(attempt); this.gridLoading = false; this.changeDetector.markForCheck(); },
      error: () => { this.gridError = 'The grid could not be loaded.'; this.gridLoading = false; this.changeDetector.markForCheck(); }
    });
  }

  onSearch(term: string): void {
    if (this.attemptCompleted || this.pendingCells.length || this.checkingPlayer || this.revealing) return;
    this.searchTerm = term;
    this.searchError = '';
    this.searchingPlayers = term.trim().length >= 2;
    this.searchCompleted = false;
    this.playerResults = [];
    this.activeResultIndex = -1;
    this.playerSearch$.next(term);
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.playerResults = [];
      this.activeResultIndex = -1;
      this.changeDetector.markForCheck();
      return;
    }

    if (this.playerResults.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeResultIndex = (this.activeResultIndex + 1) % this.playerResults.length;
      this.scrollActiveSuggestion();
      this.changeDetector.markForCheck();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeResultIndex = this.activeResultIndex <= 0
        ? this.playerResults.length - 1
        : this.activeResultIndex - 1;
      this.scrollActiveSuggestion();
      this.changeDetector.markForCheck();
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.selectPlayer(this.playerResults[this.activeResultIndex >= 0 ? this.activeResultIndex : 0]);
    }
  }

  selectPlayer(player: Player): void {
    if (this.checkingPlayer || this.revealing || this.attemptCompleted || this.pendingCells.length) return;
    this.focusSearch();
    if (this.isPlayerUsed(player.id)) {
      this.searchError = 'This player has already been used in the grid.';
      this.playerResults = [];
      this.changeDetector.markForCheck();
      return;
    }
    this.selectedPlayer = player;
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.playerResults = [];
    this.activeResultIndex = -1;
    this.pendingCells = [];
    if (!this.grid) { this.searchError = 'The grid is not ready yet.'; return; }
    this.checkingPlayer = true;
    this.searchError = '';
    this.playerSearch$.next('');
    this.api.guessDailyGrid(this.dailyAttempt!.challengeId, player.id, createGuestUuid()).pipe(
      finalize(() => {
        this.checkingPlayer = false;
        this.focusSearch();
        this.changeDetector.markForCheck();
      })
    ).subscribe({
      next: result => {
        this.applyAttempt(result);
        this.changeDetector.markForCheck();
      },
      error: () => { this.searchError = 'The answer could not be confirmed. Restoring your saved progress…'; this.loadGrid(); }
    });
  }

  revealGrid(): void {
    if (!this.grid || !this.dailyAttempt || this.checkingPlayer || this.revealing || this.attemptCompleted) return;
    this.revealing = true;
    this.searchError = '';
    this.api.revealDailyGrid(this.dailyAttempt.challengeId).pipe(
      finalize(() => { this.revealing = false; this.changeDetector.markForCheck(); })
    ).subscribe({
      next: result => {
        this.applyAttempt(result);
        this.changeDetector.markForCheck();
      },
      error: () => { this.gridError = 'The grid answers could not be revealed.'; this.changeDetector.markForCheck(); }
    });
  }

  selectCell(row: number, column: number): void {
    const cell = this.pendingCells.find(candidate => candidate.row === row && candidate.column === column);
    if (cell) this.placePlayer(cell);
  }

  isPendingCell(row: number, column: number): boolean { return this.pendingCells.some(cell => cell.row === row && cell.column === column); }
  isCellFilled(row: number, column: number): boolean { return this.cellAnswer(row, column) !== null; }
  isCellRevealed(row: number, column: number): boolean { return !this.answers[row * 3 + column] && !!this.revealedAnswers[row * 3 + column]; }
  isGridComplete(): boolean { return this.answers.every(player => player !== null); }
  cellAnswer(row: number, column: number): Player | null { return this.answers[row * 3 + column] ?? this.revealedAnswers[row * 3 + column]; }

  placePlayer(cell: GridCell): void {
    if (!this.dailyAttempt || !this.selectedPlayer || this.isCellFilled(cell.row, cell.column) || this.checkingPlayer || this.revealing || this.attemptCompleted) return;
    this.checkingPlayer = true;
    this.api.placeDailyGrid(this.dailyAttempt.challengeId, cell.row, cell.column).pipe(finalize(() => {
      this.checkingPlayer = false; this.focusSearch(); this.changeDetector.markForCheck();
    })).subscribe({ next: result => this.applyAttempt(result), error: () => { this.searchError = 'Placement could not be confirmed. Restoring your saved progress…'; this.loadGrid(); } });
  }

  private applyAttempt(attempt: DailyGridAttempt): void {
    this.dailyAttempt = attempt;
    this.grid = attempt.grid;
    this.selectedDifficulty = attempt.grid.difficulty;
    if (this.route.snapshot.paramMap.get('difficulty')?.toLowerCase() !== this.selectedDifficulty.toLowerCase()) {
      void this.router.navigate(['/grid', this.selectedDifficulty.toLowerCase()], { replaceUrl: true });
    }
    this.gridRevealed = attempt.revealed;
    this.answers = Array(9).fill(null);
    this.revealedAnswers = Array(9).fill(null);
    for (const cell of attempt.answers) (cell.revealed ? this.revealedAnswers : this.answers)[cell.row * 3 + cell.column] = cell.player;
    this.clearSearch();
    this.pendingCells = attempt.pendingCells;
    this.selectedPlayer = attempt.pendingPlayer;
    this.searchError = attempt.pendingPlayer
      ? `${attempt.pendingPlayer.displayName} fits multiple squares. Tap a highlighted square.`
      : attempt.feedback || '';
    this.gridError = '';
    this.changeDetector.markForCheck();
  }

  private clearSearch(): void {
    this.selectedPlayer = null;
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.searchTerm = '';
    this.playerResults = [];
    this.activeResultIndex = -1;
    this.playerSearch$.next('');
    this.focusSearch();
  }

  private focusSearch(): void {
    if (this.attemptCompleted) return;
    this.searchInput?.nativeElement.focus({ preventScroll: true });
    // Reapply focus after Angular finishes updating the result list and grid cells.
    afterNextRender(() => {
      if (!this.attemptCompleted) {
        this.searchInput?.nativeElement.focus({ preventScroll: true });
      }
    }, { injector: this.injector });
  }

  private scrollActiveSuggestion(): void {
    afterNextRender(() => {
      const list = this.suggestionsList?.nativeElement;
      const option = list?.children[this.activeResultIndex] as HTMLElement | undefined;
      if (!list || !option) return;
      const top = option.getBoundingClientRect().top - list.getBoundingClientRect().top + list.scrollTop;
      if (top < list.scrollTop) list.scrollTop = top;
      else if (top + option.offsetHeight > list.scrollTop + list.clientHeight) list.scrollTop = top + option.offsetHeight - list.clientHeight;
    }, { injector: this.injector });
  }

  private isPlayerUsed(playerId: number): boolean { return this.dailyAttempt?.usedPlayerIds.includes(playerId) ?? false; }
  private toTitleCase(value: string): string { return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase(); }
}
