import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { DataApiService, GridCell, GridDefinition, Player } from './data-api.service';

@Component({
  selector: 'app-grid-game',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink],
  templateUrl: './grid-game.component.html',
  styleUrl: './grid-game.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GridGameComponent {
  private readonly api = inject(DataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly playerSearch$ = new Subject<string>();
  grid: GridDefinition | null = null;
  gridLoading = true;
  gridError = '';
  selectedDifficulty = this.toTitleCase(this.route.snapshot.paramMap.get('difficulty') ?? 'normal');
  playerResults: Player[] = [];
  answers: (Player | null)[] = Array(9).fill(null);
  selectedPlayer: Player | null = null;
  searchTerm = '';
  pendingCells: GridCell[] = [];
  revealedAnswers: (Player | null)[] = Array(9).fill(null);
  searchError = '';

  constructor() {
    this.loadGrid();
    this.playerSearch$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(term => term.trim().length < 2
        ? of([])
        : this.api.searchPlayers(term.trim()).pipe(catchError(() => {
            this.searchError = 'Player search is currently unavailable.';
            this.changeDetector.markForCheck();
            return of([]);
          })))
    ).subscribe(players => {
      this.playerResults = players.filter(player => !this.isPlayerUsed(player.id));
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
    this.playerSearch$.next(term);
  }

  selectPlayer(player: Player): void {
    if (this.isPlayerUsed(player.id)) {
      this.searchError = 'This player has already been used in the grid.';
      this.playerResults = [];
      this.changeDetector.markForCheck();
      return;
    }
    this.selectedPlayer = player;
    this.playerResults = [];
    this.pendingCells = [];
    if (!this.grid) { this.searchError = 'The grid is not ready yet.'; return; }

    this.api.getGridPlayerMatch(player.id, this.grid.rowTeams.map(team => team.id), this.grid.columnTeams.map(team => team.id)).subscribe({
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
    if (!this.grid) return;
    this.api.revealGrid(this.grid.rowTeams.map(team => team.id), this.grid.columnTeams.map(team => team.id)).subscribe({
      next: result => {
        this.revealedAnswers = Array(9).fill(null);
        for (const cell of result.cells) this.revealedAnswers[cell.row * 3 + cell.column] = cell.player;
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
  cellAnswer(row: number, column: number): Player | null { return this.answers[row * 3 + column] ?? this.revealedAnswers[row * 3 + column]; }

  placePlayer(cell: GridCell): void {
    if (!this.selectedPlayer || this.isCellFilled(cell.row, cell.column)) return;
    this.answers[cell.row * 3 + cell.column] = this.selectedPlayer;
    this.pendingCells = [];
    this.clearSearch();
  }

  private resetAnswers(): void {
    this.answers = Array(9).fill(null);
    this.revealedAnswers = Array(9).fill(null);
    this.pendingCells = [];
    this.clearSearch();
  }

  private clearSearch(): void {
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.playerResults = [];
    this.playerSearch$.next('');
  }

  private isPlayerUsed(playerId: number): boolean { return [...this.answers, ...this.revealedAnswers].some(player => player?.id === playerId); }
  private toTitleCase(value: string): string { return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase(); }
}
