import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

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

export interface MissingClubRound {
  roundId: string;
  player: Player;
  knownTeams: Team[];
  options: Team[];
  completed: boolean;
  correct: boolean | null;
  correctTeam: Team | null;
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

export interface MysteryPlayerRound {
  roundId: string;
  revealedClues: string[];
  revealedClueCount: number;
  totalClues: number;
  completed: boolean;
  correct: boolean | null;
  correctPlayer: Player | null;
}

@Injectable({ providedIn: 'root' })
export class DataApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/+$/, '');

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
}
