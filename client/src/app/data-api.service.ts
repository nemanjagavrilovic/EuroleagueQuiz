import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { createGuestUuid } from './guest-identity';

export interface Team {
  id: number;
  externalId: string;
  name: string;
  shortName?: string;
  country?: string;
  countryCode?: string;
  imageUrl?: string;
  countryFlagUrl?: string;
}

export interface Player {
  id: number;
  externalId: string;
  displayName: string;
  firstName?: string;
  lastName?: string;
  nationality?: string;
  nationalityCode?: string;
  imageUrl?: string;
  countryFlagUrl?: string;
}

export interface GridCell {
  row: number;
  column: number;
  rowTeamId: number;
  columnTeamId: number;
  playerCount: number;
}

export interface GridPlayerMatch {
  player: Player;
  availableCells: GridCell[];
}

export interface GridDefinition {
  rowTeams: Team[];
  columnTeams: Team[];
  difficulty: string;
}

export interface DailyGridAttempt {
  challengeId: number;
  date: string;
  resetsAtUtc: string;
  grid: GridDefinition;
  score: number;
  guessesUsed: number;
  completed: boolean;
  revealed: boolean;
  leaderboardEligible: boolean;
  answers: { row: number; column: number; player: Player; revealed: boolean }[];
  pendingPlayer: Player | null;
  pendingCells: GridCell[];
  usedPlayerIds: number[];
  feedback: string | null;
}

export interface GridRevealCell {
  row: number;
  column: number;
  player: Player | null;
  playerCount: number;
}

export interface GridRevealResponse {
  cells: GridRevealCell[];
}

export interface TeamHistoryRound {
  roundId: string;
  revealedTeams: Team[];
  revealedTeamCount: number;
  totalTeams: number;
  completed: boolean;
  correct: boolean | null;
  attempts: number;
  correctPlayer: Player | null;
}

export interface DailyTeamHistoryAttempt {
  challengeId: number;
  date: string;
  resetsAtUtc: string;
  round: TeamHistoryRound;
  score: number;
  availablePoints: number;
  leaderboardEligible: boolean;
  usedPlayerIds: number[];
  feedback: string | null;
}

export interface MissingClubRound {
  roundId: string;
  player: Player;
  knownTeams: Team[];
  options: Team[];
  completed: boolean;
  correct: boolean | null;
  correctTeam: Team | null;
}

export interface DailyMissingClubAttempt {
  challengeId: number;
  date: string;
  resetsAtUtc: string;
  round: MissingClubRound;
  score: number;
  leaderboardEligible: boolean;
  selectedTeamId: number | null;
  feedback: string | null;
}

export interface CareerOrderClub { id: number; name: string; imageUrl?: string; }
export interface CareerOrderRound {
  roundId: string;
  player: Player;
  clubs: CareerOrderClub[];
  completed: boolean;
  correct: boolean | null;
  correctOrder: CareerOrderClub[] | null;
}

export interface DailyCareerOrderAttempt {
  challengeId: number;
  date: string;
  resetsAtUtc: string;
  round: CareerOrderRound;
  score: number;
  correctPairs: number | null;
  totalPairs: number;
  leaderboardEligible: boolean;
}

export interface MysteryPlayerRound {
  roundId: string;
  revealedClues: string[];
  revealedClueCount: number;
  totalClues: number;
  completed: boolean;
  correct: boolean | null;
  correctPlayer: Player | null;
}

export interface DailyMysteryPlayerAttempt {
  challengeId: number;
  date: string;
  resetsAtUtc: string;
  round: MysteryPlayerRound;
  score: number;
  availablePoints: number;
  leaderboardEligible: boolean;
  usedPlayerIds: number[];
  feedback: string | null;
}

export interface FivePlayerGameRound {
  roundId: string;
  roundNumber: number;
  totalRounds: number;
  secondsPerRound: number;
  secondsRemaining: number;
  team: Team;
  players: Player[];
  completed: boolean;
  roundCorrect: boolean | null;
  gameCompleted: boolean;
  score: number;
  lastGuessCorrect: boolean | null;
}

export interface DailyFivePlayerAttempt {
  challengeId: number;
  date: string;
  resetsAtUtc: string;
  round: FivePlayerGameRound;
  score: number;
  correctGuesses: number;
  leaderboardEligible: boolean;
  serverNowUtc: string;
  deadlineUtc: string | null;
  revealedPlayerIds: number[];
  feedback: string | null;
}

@Injectable({ providedIn: 'root' })
export class DataApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/+$/, '');
  private guestId = '';

  private guestHeaders(): Record<string, string> {
    if (!this.guestId) {
      try { this.guestId = localStorage.getItem('pickandroll.guestId') || ''; } catch { /* Storage may be unavailable. */ }
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(this.guestId)) {
        this.guestId = createGuestUuid();
        try { localStorage.setItem('pickandroll.guestId', this.guestId); } catch { /* Keep the identity for this session. */ }
      }
    }
    return { 'X-Guest-Id': this.guestId };
  }

  startDailyGrid(difficulty: string): Observable<DailyGridAttempt> {
    return this.http.post<DailyGridAttempt>(`${this.baseUrl}/grid/daily/start`, {}, { params: { difficulty }, headers: this.guestHeaders() });
  }
  guessDailyGrid(challengeId: number, playerId: number, commandId: string): Observable<DailyGridAttempt> {
    return this.http.post<DailyGridAttempt>(`${this.baseUrl}/grid/daily/guess`, { challengeId, playerId, commandId }, { headers: this.guestHeaders() });
  }
  placeDailyGrid(challengeId: number, row: number, column: number): Observable<DailyGridAttempt> {
    return this.http.post<DailyGridAttempt>(`${this.baseUrl}/grid/daily/place`, { challengeId, row, column }, { headers: this.guestHeaders() });
  }
  revealDailyGrid(challengeId: number): Observable<DailyGridAttempt> {
    return this.http.post<DailyGridAttempt>(`${this.baseUrl}/grid/daily/reveal`, { challengeId }, { headers: this.guestHeaders() });
  }

  getTeams(): Observable<Team[]> {
    return this.http.get<Team[]>(`${this.baseUrl}/teams`);
  }

  getGrid(difficulty = 'Normal'): Observable<GridDefinition> {
    return this.http.get<GridDefinition>(`${this.baseUrl}/grid`, { params: { difficulty } });
  }

  changeGrid(difficulty = 'Normal'): Observable<GridDefinition> {
    return this.http.post<GridDefinition>(`${this.baseUrl}/grid/change`, {}, { params: { difficulty } });
  }

  revealGrid(rowTeamIds: number[], columnTeamIds: number[]): Observable<GridRevealResponse> {
    return this.http.post<GridRevealResponse>(`${this.baseUrl}/grid/reveal`, { rowTeamIds, columnTeamIds });
  }

  searchPlayers(term: string): Observable<Player[]> {
    return this.http.get<Player[]>(`${this.baseUrl}/players/search`, { params: { term } });
  }

  getGridPlayerMatch(playerId: number, rowTeamIds: number[], columnTeamIds: number[]): Observable<GridPlayerMatch> {
    return this.http.post<GridPlayerMatch>(`${this.baseUrl}/players/grid-match`, {
      playerId,
      rowTeamIds,
      columnTeamIds
    });
  }

  startTeamHistoryGame(): Observable<TeamHistoryRound> {
    return this.http.post<TeamHistoryRound>(`${this.baseUrl}/team-history/start`, {});
  }

  startDailyTeamHistory(): Observable<DailyTeamHistoryAttempt> {
    return this.http.post<DailyTeamHistoryAttempt>(`${this.baseUrl}/team-history/daily/start`, {}, { headers: this.guestHeaders() });
  }

  startDailyMissingClub(): Observable<DailyMissingClubAttempt> {
    return this.http.post<DailyMissingClubAttempt>(`${this.baseUrl}/career-games/missing-club/daily/start`, {}, { headers: this.guestHeaders() });
  }

  startDailyCareerOrder(): Observable<DailyCareerOrderAttempt> {
    return this.http.post<DailyCareerOrderAttempt>(`${this.baseUrl}/career-games/career-order/daily/start`, {}, { headers: this.guestHeaders() });
  }

  startDailyMysteryPlayer(): Observable<DailyMysteryPlayerAttempt> {
    return this.http.post<DailyMysteryPlayerAttempt>(`${this.baseUrl}/career-games/mystery-player/daily/start`, {}, { headers: this.guestHeaders() });
  }

  startDailyFivePlayer(): Observable<DailyFivePlayerAttempt> {
    return this.http.post<DailyFivePlayerAttempt>(`${this.baseUrl}/five-player/daily/start`, {}, { headers: this.guestHeaders() });
  }

  guessDailyFivePlayer(challengeId: number, roundNumber: number, commandId: string, playerId?: number, skip = false): Observable<DailyFivePlayerAttempt> {
    return this.http.post<DailyFivePlayerAttempt>(`${this.baseUrl}/five-player/daily/guess`, { challengeId, roundNumber, commandId, playerId, skip }, { headers: this.guestHeaders() });
  }

  nextDailyFivePlayer(challengeId: number, roundNumber: number, commandId: string): Observable<DailyFivePlayerAttempt> {
    return this.http.post<DailyFivePlayerAttempt>(`${this.baseUrl}/five-player/daily/next`, { challengeId, roundNumber, commandId }, { headers: this.guestHeaders() });
  }

  guessDailyMysteryPlayer(challengeId: number, commandId: string, playerId?: number, skip = false): Observable<DailyMysteryPlayerAttempt> {
    return this.http.post<DailyMysteryPlayerAttempt>(`${this.baseUrl}/career-games/mystery-player/daily/guess`, { challengeId, commandId, playerId, skip }, { headers: this.guestHeaders() });
  }

  saveDailyCareerOrder(challengeId: number, commandId: string, teamIds: number[]): Observable<DailyCareerOrderAttempt> {
    return this.http.post<DailyCareerOrderAttempt>(`${this.baseUrl}/career-games/career-order/daily/draft`, { challengeId, commandId, teamIds }, { headers: this.guestHeaders() });
  }

  submitDailyCareerOrder(challengeId: number, commandId: string, teamIds: number[]): Observable<DailyCareerOrderAttempt> {
    return this.http.post<DailyCareerOrderAttempt>(`${this.baseUrl}/career-games/career-order/daily/submit`, { challengeId, commandId, teamIds }, { headers: this.guestHeaders() });
  }

  guessDailyMissingClub(challengeId: number, commandId: string, teamId?: number, skip = false): Observable<DailyMissingClubAttempt> {
    return this.http.post<DailyMissingClubAttempt>(`${this.baseUrl}/career-games/missing-club/daily/guess`, { challengeId, commandId, teamId, skip }, { headers: this.guestHeaders() });
  }

  guessDailyTeamHistory(challengeId: number, commandId: string, playerId?: number, skip = false): Observable<DailyTeamHistoryAttempt> {
    return this.http.post<DailyTeamHistoryAttempt>(`${this.baseUrl}/team-history/daily/guess`, { challengeId, commandId, playerId, skip }, { headers: this.guestHeaders() });
  }

  guessTeamHistory(roundId: string, playerId?: number, skip = false): Observable<TeamHistoryRound> {
    return this.http.post<TeamHistoryRound>(`${this.baseUrl}/team-history/guess`, {
      roundId,
      playerId,
      skip
    });
  }

  startMissingClub(): Observable<MissingClubRound> { return this.http.post<MissingClubRound>(`${this.baseUrl}/career-games/missing-club/start`, {}); }
  guessMissingClub(roundId: string, teamId?: number, skip = false): Observable<MissingClubRound> { return this.http.post<MissingClubRound>(`${this.baseUrl}/career-games/missing-club/guess`, { roundId, teamId, skip }); }
  startCareerOrder(): Observable<CareerOrderRound> { return this.http.post<CareerOrderRound>(`${this.baseUrl}/career-games/career-order/start`, {}); }
  guessCareerOrder(roundId: string, teamIds: number[]): Observable<CareerOrderRound> { return this.http.post<CareerOrderRound>(`${this.baseUrl}/career-games/career-order/guess`, { roundId, teamIds }); }
  startMysteryPlayer(): Observable<MysteryPlayerRound> { return this.http.post<MysteryPlayerRound>(`${this.baseUrl}/career-games/mystery-player/start`, {}); }
  guessMysteryPlayer(roundId: string, playerId?: number, skip = false): Observable<MysteryPlayerRound> { return this.http.post<MysteryPlayerRound>(`${this.baseUrl}/career-games/mystery-player/guess`, { roundId, playerId, skip }); }
  startFivePlayerGame(): Observable<FivePlayerGameRound> { return this.http.post<FivePlayerGameRound>(`${this.baseUrl}/five-player/start`, {}); }
  guessFivePlayer(roundId: string, playerId?: number, skip = false): Observable<FivePlayerGameRound> { return this.http.post<FivePlayerGameRound>(`${this.baseUrl}/five-player/guess`, { roundId, playerId, skip }); }
  nextFivePlayerRound(roundId: string): Observable<FivePlayerGameRound> { return this.http.post<FivePlayerGameRound>(`${this.baseUrl}/five-player/next`, {}, { params: { roundId } }); }
}
