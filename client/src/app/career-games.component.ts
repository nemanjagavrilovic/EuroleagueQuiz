import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Injector, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, finalize, map, of, switchMap } from 'rxjs';
import { CareerOrderClub, CareerOrderRound, DataApiService, MissingClubRound, MysteryPlayerRound, Player } from './data-api.service';
import { BasketballLoaderComponent } from './basketball-loader.component';

type GameType = 'missing-club' | 'career-order' | 'mystery-player';

@Component({
  selector: 'app-career-games', standalone: true, imports: [NgFor, NgIf, RouterLink, BasketballLoaderComponent],
  templateUrl: './career-games.component.html', styleUrl: './career-games.component.css', changeDetection: ChangeDetectionStrategy.OnPush
})
export class CareerGamesComponent implements OnInit {
  @ViewChild('mysterySearch') private mysterySearch?: ElementRef<HTMLInputElement>;
  @ViewChild('mysterySuggestions') private mysterySuggestions?: ElementRef<HTMLDivElement>;
  private readonly injector = inject(Injector);
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
  activeSuggestionIndex = -1;
  searchTerm = '';
  answerFeedback = '';
  orderFeedback = '';
  loading = false;
  errorMessage = '';
  searchingPlayers = false;
  searchCompleted = false;

  get mysterySlots(): number[] { return Array.from({ length: this.mystery?.totalClues ?? 0 }, (_, index) => index); }

  ngOnInit(): void {
    this.game = (this.route.snapshot.paramMap.get('game') as GameType) || 'missing-club';
    this.searchTerms.pipe(debounceTime(180), switchMap(term => {
      const value = term.trim();
      return value.length < 2 ? of({ term, players: [] as Player[] }) : this.api.searchPlayers(value).pipe(
        map(players => ({ term, players })),
        catchError(() => {
          if (term === this.searchTerm) this.errorMessage = 'Player search is currently unavailable.';
          return of({ term, players: [] as Player[] });
        })
      );
    })).subscribe(({ term, players }) => {
      if (term !== this.searchTerm || this.loading || this.mystery?.completed) return;
      this.searchingPlayers = false; this.searchCompleted = term.trim().length >= 2;
      this.suggestions = players.slice(0, 10); this.activeSuggestionIndex = -1; this.detector.markForCheck();
    });
    this.start();
  }

  get title(): string {
    return this.game === 'missing-club' ? 'Which club is missing?' : this.game === 'career-order' ? 'Order the career' : 'Mystery player';
  }

  start(): void {
    if (this.loading) return;
    this.selectedTeamId = null;
    this.orderFeedback = '';
    this.loading = true; this.errorMessage = ''; this.answerFeedback = ''; this.missing = null; this.order = null; this.mystery = null;
    this.clearSearch();
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

  chooseTeam(id: number): void { if (this.missing && !this.missing.completed && !this.loading) this.selectedTeamId = id; }
  submitMissing(skip = false): void {
    if (!this.missing || this.missing.completed || this.loading || (!skip && this.selectedTeamId === null)) return;
    this.errorMessage = '';
    this.loading = true;
    this.api.guessMissingClub(this.missing.roundId, this.selectedTeamId ?? undefined, skip).pipe(
      finalize(() => { this.loading = false; this.detector.markForCheck(); })
    ).subscribe({
      next: round => this.missing = round,
      error: () => this.errorMessage = 'Your answer could not be submitted. Please try again.'
    });
  }

  moveClub(index: number, direction: -1 | 1): void {
    if (!this.order || this.order.completed || this.loading) return;
    const target = index + direction; if (target < 0 || target >= this.orderClubs.length) return;
    [this.orderClubs[index], this.orderClubs[target]] = [this.orderClubs[target], this.orderClubs[index]];
    this.orderFeedback = `${this.orderClubs[target].name} moved to position ${target + 1} of ${this.orderClubs.length}.`;
  }
  submitOrder(): void {
    if (!this.order || this.order.completed || this.loading) return;
    this.loading = true;
    this.errorMessage = '';
    this.api.guessCareerOrder(this.order.roundId, this.orderClubs.map(x => x.id)).pipe(
      finalize(() => { this.loading = false; this.detector.markForCheck(); })
    ).subscribe({
      next: round => this.order = round,
      error: () => this.errorMessage = 'Your order could not be submitted. Please try again.'
    });
  }

  trackClub(_index: number, club: CareerOrderClub): number { return club.id; }

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
      this.scrollMysterySuggestion();
      this.detector.markForCheck();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeSuggestionIndex = this.activeSuggestionIndex <= 0
        ? this.suggestions.length - 1
        : this.activeSuggestionIndex - 1;
      this.scrollMysterySuggestion();
      this.detector.markForCheck();
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.selectPlayer(this.suggestions[this.activeSuggestionIndex >= 0 ? this.activeSuggestionIndex : 0]);
    }
  }

  selectPlayer(player: Player): void {
    if (this.loading || !this.mystery || this.mystery.completed) return;
    this.focusMysterySearch();
    this.selectedPlayer = player;
    this.searchTerm = player.displayName;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.submitMystery();
  }
  submitMystery(skip = false): void {
    if (!this.mystery || this.mystery.completed || this.loading || (!skip && !this.selectedPlayer)) return;
    const playerId = this.selectedPlayer?.id;
    this.errorMessage = '';
    this.answerFeedback = '';
    this.clearSearch();
    this.loading = true;
    this.api.guessMysteryPlayer(this.mystery.roundId, playerId, skip).pipe(
      finalize(() => { this.loading = false; this.focusMysterySearch(); this.detector.markForCheck(); })
    ).subscribe({
      next: round => {
        this.mystery = round;
        this.answerFeedback = round.completed ? '' : skip ? 'Another clue was revealed.' : 'Not this player — another clue was revealed.';
      },
      error: () => this.errorMessage = 'Your answer could not be submitted. Please try again.'
    });
  }

  private focusMysterySearch(): void {
    if (!this.mystery || this.mystery.completed) return;
    this.mysterySearch?.nativeElement.focus({ preventScroll: true });
    afterNextRender(() => {
      if (this.mystery && !this.mystery.completed) this.mysterySearch?.nativeElement.focus({ preventScroll: true });
    }, { injector: this.injector });
  }

  private clearSearch(): void {
    this.searchingPlayers = false;
    this.searchCompleted = false;
    this.selectedPlayer = null;
    this.searchTerm = '';
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.searchTerms.next('');
  }

  private scrollMysterySuggestion(): void {
    afterNextRender(() => {
      const list = this.mysterySuggestions?.nativeElement;
      const option = list?.children[this.activeSuggestionIndex] as HTMLElement | undefined;
      if (!list || !option) return;
      const top = option.getBoundingClientRect().top - list.getBoundingClientRect().top + list.scrollTop;
      if (top < list.scrollTop) list.scrollTop = top;
      else if (top + option.offsetHeight > list.scrollTop + list.clientHeight) list.scrollTop = top + option.offsetHeight - list.clientHeight;
    }, { injector: this.injector });
  }
}
