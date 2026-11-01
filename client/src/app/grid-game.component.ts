import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Injector, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, map, switchMap } from 'rxjs/operators';
import { DataApiService, GridCell, GridDefinition, Player } from './data-api.service';
import { BasketballLoaderComponent } from './basketball-loader.component';

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

  get guessedCount(): number { return this.answers.filter(player => player !== null).length; }

  constructor() {
    this.loadGrid();
    this.playerSearch$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(term => term.trim().length < 2
        ? of({ term, players: [] as Player[] })
        : this.api.searchPlayers(term.trim()).pipe(
          map(players => ({ term, players })),
          catchError(() => {
            if (term === this.searchTerm) this.searchError = 'Player search is currently unavailable.';
            return of({ term, players: [] as Player[] });
          })))
    ).subscribe(({ term, players }) => {
      if (term !== this.searchTerm || this.checkingPlayer || this.revealing || this.gridRevealed || this.isGridComplete()) return;
      this.searchingPlayers = false;
      this.searchCompleted = term.trim().length >= 2;
      this.playerResults = players.filter(player => !this.isPlayerUsed(player.id));
      this.activeResultIndex = -1;
      this.changeDetector.markForCheck();
    });
  }

  private loadGrid(): void {
    this.api.getGrid(this.selectedDifficulty).subscribe({
      next: grid => { this.grid = grid; this.gridLoading = false; this.changeDetector.markForCheck(); },
      error: () => { this.gridError = 'The grid could not be loaded.'; this.gridLoading = false; this.changeDetector.markForCheck(); }
    });
  }

  onSearch(term: string): void {
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
    if (this.checkingPlayer || this.revealing || this.gridRevealed || this.isGridComplete()) return;
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
    this.api.getGridPlayerMatch(player.id, this.grid.rowTeams.map(team => team.id), this.grid.columnTeams.map(team => team.id)).pipe(
      finalize(() => {
        this.checkingPlayer = false;
        this.focusSearch();
        this.changeDetector.markForCheck();
      })
    ).subscribe({
      next: result => {
        this.pendingCells = result.availableCells.filter(cell => !this.cellAnswer(cell.row, cell.column));
        if (this.pendingCells.length === 0) {
          this.searchError = 'This player does not match any empty grid cell.';
          this.clearSearch();
        } else if (this.pendingCells.length === 1 || this.pendingCells.some(cell => cell.playerCount === 1)) {
          for (const cell of this.pendingCells) this.answers[cell.row * 3 + cell.column] = player;
          this.pendingCells = [];
          this.clearSearch();
        }
        this.changeDetector.markForCheck();
      },
      error: () => { this.searchError = 'The player could not be checked against this grid.'; this.changeDetector.markForCheck(); }
    });
  }

  changeGrid(): void {
    this.gridLoading = true;
    this.gridError = '';
    this.resetAnswers();
    this.api.changeGrid(this.selectedDifficulty).subscribe({
      next: grid => { this.grid = grid; this.gridLoading = false; this.changeDetector.markForCheck(); },
      error: () => { this.gridError = 'The new grid could not be loaded.'; this.gridLoading = false; this.changeDetector.markForCheck(); }
    });
  }

  revealGrid(): void {
    if (!this.grid || this.checkingPlayer || this.revealing || this.gridRevealed || this.isGridComplete()) return;
    this.revealing = true;
    this.searchError = '';
    this.api.revealGrid(this.grid.rowTeams.map(team => team.id), this.grid.columnTeams.map(team => team.id)).pipe(
      finalize(() => { this.revealing = false; this.changeDetector.markForCheck(); })
    ).subscribe({
      next: result => {
        this.revealedAnswers = Array(9).fill(null);
        for (const cell of result.cells) this.revealedAnswers[cell.row * 3 + cell.column] = cell.player;
        this.gridRevealed = true;
        this.pendingCells = [];
        this.clearSearch();
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
    if (!this.selectedPlayer || this.isCellFilled(cell.row, cell.column)) return;
    this.answers[cell.row * 3 + cell.column] = this.selectedPlayer;
    this.pendingCells = [];
    this.clearSearch();
  }

  private resetAnswers(): void {
    this.gridRevealed = false;
    this.answers = Array(9).fill(null);
    this.revealedAnswers = Array(9).fill(null);
    this.pendingCells = [];
    this.clearSearch();
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
    if (this.gridRevealed || this.isGridComplete()) return;
    this.searchInput?.nativeElement.focus({ preventScroll: true });
    // Reapply focus after Angular finishes updating the result list and grid cells.
    afterNextRender(() => {
      if (!this.gridRevealed && !this.isGridComplete()) {
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

  private isPlayerUsed(playerId: number): boolean { return [...this.answers, ...this.revealedAnswers].some(player => player?.id === playerId); }
  private toTitleCase(value: string): string { return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase(); }
}
