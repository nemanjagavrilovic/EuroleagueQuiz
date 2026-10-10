import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, distinctUntilChanged, finalize, of, switchMap } from 'rxjs';
import { CareerOrderClub, CareerOrderRound, DataApiService, MissingClubRound, MysteryPlayerRound, Player } from './data-api.service';

type GameType = 'missing-club' | 'career-order' | 'mystery-player';

@Component({
  selector: 'app-career-games', standalone: true, imports: [NgFor, NgIf, RouterLink],
  templateUrl: './career-games.component.html', styleUrl: './career-games.component.css', changeDetection: ChangeDetectionStrategy.OnPush
})
export class CareerGamesComponent implements OnInit {
  private readonly api = inject(DataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly detector = inject(ChangeDetectorRef);
  private readonly searchTerms = new Subject<string>();

  game: GameType = 'missing-club';
  missing: MissingClubRound | null = null;
  order: CareerOrderRound | null = null;
  mystery: MysteryPlayerRound | null = null;
  orderClubs: CareerOrderClub[] = [];
  selectedTeamId: number | null = null;
  selectedPlayer: Player | null = null;
  suggestions: Player[] = [];
  searchTerm = '';
  loading = false;
  errorMessage = '';

  ngOnInit(): void {
    this.game = (this.route.snapshot.paramMap.get('game') as GameType) || 'missing-club';
    this.searchTerms.pipe(debounceTime(180), distinctUntilChanged(), switchMap(term => {
      const value = term.trim();
      return value.length < 2 ? of([] as Player[]) : this.api.searchPlayers(value).pipe(catchError(() => of([] as Player[])));
    })).subscribe(players => { this.suggestions = players.slice(0, 10); this.detector.markForCheck(); });
    this.start();
  }

  get title(): string {
    return this.game === 'missing-club' ? 'Which club is missing?' : this.game === 'career-order' ? 'Order the career' : 'Mystery player';
  }

  start(): void {
    this.loading = true; this.errorMessage = ''; this.missing = null; this.order = null; this.mystery = null;
    if (this.game === 'missing-club') {
      this.loadRound(this.api.startMissingClub(), value => this.missing = value);
    } else if (this.game === 'career-order') {
      this.loadRound(this.api.startCareerOrder(), value => { this.order = value; this.orderClubs = [...value.clubs]; });
    } else {
      this.loadRound(this.api.startMysteryPlayer(), value => this.mystery = value);
    }
  }

  private loadRound<T>(request: Observable<T>, onSuccess: (value: T) => void): void {
    request.pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
      next: onSuccess,
      error: () => this.errorMessage = 'The game could not be started. Import player and club data first.'
    });
  }

  chooseTeam(id: number): void { if (!this.missing?.completed) this.selectedTeamId = id; }
  submitMissing(skip = false): void {
    if (!this.missing || (!skip && this.selectedTeamId === null)) return;
    this.loading = true; this.api.guessMissingClub(this.missing.roundId, this.selectedTeamId ?? undefined, skip).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe(round => { this.missing = round; });
  }

  moveClub(index: number, direction: -1 | 1): void {
    const target = index + direction; if (target < 0 || target >= this.orderClubs.length) return;
    [this.orderClubs[index], this.orderClubs[target]] = [this.orderClubs[target], this.orderClubs[index]];
  }
  submitOrder(): void {
    if (!this.order) return;
    this.loading = true; this.api.guessCareerOrder(this.order.roundId, this.orderClubs.map(x => x.id)).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe(round => { this.order = round; });
  }

  onSearch(value: string): void { this.searchTerm = value; this.selectedPlayer = null; this.searchTerms.next(value); }
  selectPlayer(player: Player): void { this.selectedPlayer = player; this.searchTerm = player.displayName; this.suggestions = []; }
  submitMystery(skip = false): void {
    if (!this.mystery || (!skip && !this.selectedPlayer)) return;
    this.loading = true; this.api.guessMysteryPlayer(this.mystery.roundId, this.selectedPlayer?.id, skip).pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe(round => { this.mystery = round; this.selectedPlayer = null; this.searchTerm = ''; this.suggestions = []; });
  }
}
