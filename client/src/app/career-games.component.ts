import { afterNextRender, ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, ElementRef, Injector, OnInit, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, concatMap, debounceTime, finalize, map, of, switchMap } from 'rxjs';
import { CareerOrderClub, CareerOrderRound, DailyCareerOrderAttempt, DailyMissingClubAttempt, DailyMysteryPlayerAttempt, DataApiService, MissingClubRound, MysteryPlayerRound, Player } from './data-api.service';
import { createGuestUuid } from './guest-identity';
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
  private readonly destroyRef = inject(DestroyRef);
  private readonly orderDrafts = new Subject<{ challengeId: number; commandId: string; teamIds: number[] }>();

  game: GameType = 'missing-club';
  missing: MissingClubRound | null = null;
  dailyMissing: DailyMissingClubAttempt | null = null;
  restoringMissing = false;
  order: CareerOrderRound | null = null;
  dailyOrder: DailyCareerOrderAttempt | null = null;
  restoringOrder = false;
  pendingOrderSaves = 0;
  mystery: MysteryPlayerRound | null = null;
  dailyMystery: DailyMysteryPlayerAttempt | null = null;
  restoringMystery = false;
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
    this.orderDrafts.pipe(
      concatMap(draft => this.api.saveDailyCareerOrder(draft.challengeId, draft.commandId, draft.teamIds).pipe(catchError(() => of(null)))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(attempt => {
      this.pendingOrderSaves = Math.max(0, this.pendingOrderSaves - 1);
      if (attempt?.round.completed) this.applyOrderAttempt(attempt);
      else if (attempt && this.pendingOrderSaves === 0) {
        this.dailyOrder = attempt; this.errorMessage = ''; this.orderFeedback = 'Order saved.';
      } else if (!attempt) this.errorMessage = 'Your order could not be saved. Submit your current order to confirm it, or refresh to load the last saved order.';
      this.detector.markForCheck();
    });
    this.searchTerms.pipe(debounceTime(180), switchMap(term => {
      const value = term.trim();
      return value.length < 2 ? of({ term, players: [] as Player[] }) : this.api.searchPlayers(value).pipe(
        map(players => ({ term, players })),
        catchError(() => {
          if (term === this.searchTerm) this.errorMessage = 'Player search is currently unavailable.';
          return of({ term, players: [] as Player[] });
        })
      );
    }), takeUntilDestroyed(this.destroyRef)).subscribe(({ term, players }) => {
      if (term !== this.searchTerm || this.loading || this.restoringMystery || this.mystery?.completed) return;
      this.searchingPlayers = false; this.searchCompleted = term.trim().length >= 2;
      this.suggestions = players.filter(player => !this.dailyMystery?.usedPlayerIds.includes(player.id)).slice(0, 10); this.activeSuggestionIndex = -1; this.detector.markForCheck();
    });
    this.start();
  }

  get title(): string {
    return this.game === 'missing-club' ? 'Which club is missing?' : this.game === 'career-order' ? 'Order the career' : 'Mystery player';
  }

  start(): void {
    if (this.loading || this.restoringMissing || this.restoringOrder || this.restoringMystery || this.pendingOrderSaves) return;
    this.selectedTeamId = null;
    this.orderFeedback = '';
    this.loading = true; this.errorMessage = ''; this.answerFeedback = ''; this.missing = null; this.order = null; this.mystery = null;
    this.clearSearch();
    if (this.game === 'missing-club') {
      this.loadRound(this.api.startDailyMissingClub(), value => this.applyMissingAttempt(value));
    } else if (this.game === 'career-order') {
      this.loadRound(this.api.startDailyCareerOrder(), value => this.applyOrderAttempt(value));
    } else {
      this.loadRound(this.api.startDailyMysteryPlayer(), value => this.applyMysteryAttempt(value));
    }
  }

  private loadRound<T>(request: Observable<T>, onSuccess: (value: T) => void): void {
    request.pipe(finalize(() => { this.loading = false; this.detector.markForCheck(); })).subscribe({
      next: onSuccess,
      error: () => this.errorMessage = 'The game could not be started. Import player and club data first.'
    });
  }

  chooseTeam(id: number): void { if (this.missing && !this.missing.completed && !this.loading && !this.restoringMissing) this.selectedTeamId = id; }
  submitMissing(skip = false): void {
    if (!this.missing || !this.dailyMissing || this.missing.completed || this.loading || this.restoringMissing || (!skip && this.selectedTeamId === null)) return;
    this.errorMessage = '';
    this.loading = true;
    this.api.guessDailyMissingClub(this.dailyMissing.challengeId, createGuestUuid(), this.selectedTeamId ?? undefined, skip).pipe(
      finalize(() => { this.loading = false; this.detector.markForCheck(); })
    ).subscribe({
      next: attempt => this.applyMissingAttempt(attempt),
      error: () => {
        this.restoringMissing = true;
        this.errorMessage = 'Your answer could not be confirmed. Restoring saved progress…';
        this.api.startDailyMissingClub().pipe(finalize(() => {
          this.restoringMissing = false; this.detector.markForCheck();
        })).subscribe({
          next: attempt => this.applyMissingAttempt(attempt),
          error: () => { this.errorMessage = 'Saved progress could not be loaded. Refresh before submitting another answer.'; this.detector.markForCheck(); }
        });
      }
    });
  }

  private applyMissingAttempt(attempt: DailyMissingClubAttempt): void {
    this.dailyMissing = attempt;
    this.missing = attempt.round;
    if (attempt.round.completed) this.selectedTeamId = attempt.selectedTeamId;
    else if (!attempt.round.options.some(team => team.id === this.selectedTeamId)) this.selectedTeamId = null;
    this.errorMessage = '';
    this.detector.markForCheck();
  }

  moveClub(index: number, direction: -1 | 1): void {
    if (!this.order || !this.dailyOrder || this.order.completed || this.loading || this.restoringOrder) return;
    const target = index + direction; if (target < 0 || target >= this.orderClubs.length) return;
    [this.orderClubs[index], this.orderClubs[target]] = [this.orderClubs[target], this.orderClubs[index]];
    this.orderFeedback = `${this.orderClubs[target].name} moved to position ${target + 1} of ${this.orderClubs.length}.`;
    this.pendingOrderSaves++;
    this.orderDrafts.next({ challengeId: this.dailyOrder.challengeId, commandId: createGuestUuid(), teamIds: this.orderClubs.map(x => x.id) });
  }
  submitOrder(): void {
    if (!this.order || !this.dailyOrder || this.order.completed || this.loading || this.restoringOrder || this.pendingOrderSaves) return;
    this.loading = true;
    this.errorMessage = '';
    this.api.submitDailyCareerOrder(this.dailyOrder.challengeId, createGuestUuid(), this.orderClubs.map(x => x.id)).pipe(
      finalize(() => { this.loading = false; this.detector.markForCheck(); })
    ).subscribe({
      next: attempt => this.applyOrderAttempt(attempt),
      error: () => {
        this.restoringOrder = true;
        this.errorMessage = 'Your submission could not be confirmed. Restoring saved progress…';
        this.api.startDailyCareerOrder().pipe(finalize(() => {
          this.restoringOrder = false; this.detector.markForCheck();
        })).subscribe({
          next: attempt => this.applyOrderAttempt(attempt),
          error: () => { this.errorMessage = 'Saved progress could not be loaded. Refresh before submitting another answer.'; this.detector.markForCheck(); }
        });
      }
    });
  }

  private applyOrderAttempt(attempt: DailyCareerOrderAttempt): void {
    this.dailyOrder = attempt;
    this.order = attempt.round;
    this.orderClubs = [...attempt.round.clubs];
    this.errorMessage = '';
    this.orderFeedback = attempt.round.completed ? '' : 'Move clubs up or down, then submit your order.';
    this.detector.markForCheck();
  }

  trackClub(_index: number, club: CareerOrderClub): number { return club.id; }

  onSearch(value: string): void {
    if (this.loading || this.restoringMystery || this.mystery?.completed) return;
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
    if (this.loading || this.restoringMystery || !this.mystery || this.mystery.completed || this.dailyMystery?.usedPlayerIds.includes(player.id)) return;
    this.focusMysterySearch();
    this.selectedPlayer = player;
    this.searchTerm = player.displayName;
    this.suggestions = [];
    this.activeSuggestionIndex = -1;
    this.submitMystery();
  }
  submitMystery(skip = false): void {
    if (!this.mystery || !this.dailyMystery || this.mystery.completed || this.loading || this.restoringMystery || (!skip && !this.selectedPlayer)) return;
    const playerId = this.selectedPlayer?.id;
    this.errorMessage = '';
    this.answerFeedback = '';
    this.clearSearch();
    this.loading = true;
    this.api.guessDailyMysteryPlayer(this.dailyMystery.challengeId, createGuestUuid(), playerId, skip).pipe(
      finalize(() => { this.loading = false; this.focusMysterySearch(); this.detector.markForCheck(); })
    ).subscribe({
      next: attempt => this.applyMysteryAttempt(attempt),
      error: () => {
        this.restoringMystery = true;
        this.errorMessage = 'Your answer could not be confirmed. Restoring saved progress…';
        this.api.startDailyMysteryPlayer().pipe(finalize(() => {
          this.restoringMystery = false; this.focusMysterySearch(); this.detector.markForCheck();
        })).subscribe({
          next: attempt => this.applyMysteryAttempt(attempt),
          error: () => { this.errorMessage = 'Saved progress could not be loaded. Refresh before submitting another answer.'; this.detector.markForCheck(); }
        });
      }
    });
  }

  private applyMysteryAttempt(attempt: DailyMysteryPlayerAttempt): void {
    this.dailyMystery = attempt;
    this.mystery = attempt.round;
    this.clearSearch();
    this.errorMessage = '';
    this.answerFeedback = attempt.feedback || '';
    this.detector.markForCheck();
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
