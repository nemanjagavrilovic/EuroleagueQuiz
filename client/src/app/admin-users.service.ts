import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  hasPassword: boolean;
  mustChangePassword: boolean;
  subscriptionPlan: string;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
  isDeleted: boolean;
  deletedAtUtc: string | null;
  roles: string[];
}

export interface AdminUserPage {
  users: AdminUser[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

interface MessageResponse { message: string; }

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl.replace(/\/+$/, '')}/admin/users`;

  getUsers(search: string, page: number, pageSize: number, includeDeleted: boolean): Observable<AdminUserPage> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('includeDeleted', includeDeleted);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<AdminUserPage>(this.baseUrl, { params });
  }

  deleteUser(id: string): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/${id}`);
  }

  sendPasswordReset(id: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/${id}/password-reset`, {});
  }

  resendConfirmation(id: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/${id}/resend-confirmation`, {});
  }
}
