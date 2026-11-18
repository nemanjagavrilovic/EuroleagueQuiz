import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface AdminAuditLog {
  id: number;
  occurredAtUtc: string;
  actorUserId: string | null;
  actorGuestId: string | null;
  actorType: string;
  actorDisplayName: string;
  action: string;
  result: string;
  entityType: string | null;
  entityId: string | null;
  correlationId: string | null;
  detailsJson: string | null;
}

export interface AdminAuditLogPage {
  entries: AdminAuditLog[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AuditLogFilters {
  search: string;
  action: string;
  result: string;
  actorType: string;
  fromUtc: string;
  toUtc: string;
}

export interface GuestGameActivity {
  game: string;
  name: string;
  uniqueGuests: number;
  gamesPlayed: number;
  completedGames: number;
}

export interface GuestActivity {
  uniqueGuests: number;
  gamesPlayed: number;
  completedGames: number;
  completionRate: number;
  fromUtc: string | null;
  toUtc: string | null;
  trackingStartedAtUtc: string | null;
  games: GuestGameActivity[];
}

@Injectable({ providedIn: 'root' })
export class AdminAuditLogsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl.replace(/\/+$/, '')}/admin/audit-logs`;

  getPage(filters: AuditLogFilters, page: number, pageSize: number): Observable<AdminAuditLogPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(filters)) if (value) params = params.set(key, value);
    return this.http.get<AdminAuditLogPage>(this.baseUrl, { params });
  }

  getGuestActivity(fromUtc: string, toUtc: string): Observable<GuestActivity> {
    let params = new HttpParams();
    if (fromUtc) params = params.set('fromUtc', fromUtc);
    if (toUtc) params = params.set('toUtc', toUtc);
    return this.http.get<GuestActivity>(`${this.baseUrl}/guest-activity`, { params });
  }
}
