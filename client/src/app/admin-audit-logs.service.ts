import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface AdminAuditLog {
  id: number;
  occurredAtUtc: string;
  actorUserId: string | null;
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
  fromUtc: string;
  toUtc: string;
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
}
