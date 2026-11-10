import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AdminAuditLog, AdminAuditLogPage, AdminAuditLogsService } from './admin-audit-logs.service';

@Component({
  selector: 'app-admin-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-audit-logs.component.html',
  styleUrl: './admin-audit-logs.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminAuditLogsComponent {
  private readonly api = inject(AdminAuditLogsService);
  readonly result = signal<AdminAuditLogPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly actions = [
    'User.Registered', 'User.ProfileUpdated',
    'Auth.LoginSucceeded', 'Auth.LoginFailed', 'Auth.Logout', 'Auth.GoogleLoginSucceeded',
    'Auth.GoogleLoginFailed', 'Auth.SessionRefreshFailed',
    'Auth.EmailConfirmed', 'Auth.EmailConfirmationFailed', 'Auth.ConfirmationRequested', 'Auth.PasswordResetRequested',
    'Auth.PasswordSetupRequested', 'Auth.PasswordChanged',
    'Admin.UserSoftDeleted', 'Admin.TemporaryPasswordSent', 'Admin.ConfirmationResent',
    'Import.SeasonStarted', 'Import.SeasonCompleted', 'Import.SeasonFailed',
    'Import.AllSeasonsStarted', 'Import.AllSeasonsCompleted', 'Import.AllSeasonsFailed',
    'Contact.VerificationFailed', 'Contact.MessageSent', 'Contact.ValidationFailed', 'Contact.DeliveryFailed',
    'Game.GridRankedCompleted', 'Game.TeamHistoryRankedCompleted', 'Game.MissingClubRankedCompleted',
    'Game.CareerOrderRankedCompleted', 'Game.MysteryPlayerRankedCompleted', 'Game.StartingFiveRankedCompleted'
  ];
  search = '';
  action = '';
  resultFilter = '';
  fromDate = '';
  toDate = '';
  page = 1;
  readonly pageSize = 25;

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getPage({
      search: this.search.trim(),
      action: this.action,
      result: this.resultFilter,
      fromUtc: this.dateBoundary(this.fromDate, false),
      toUtc: this.dateBoundary(this.toDate, true)
    }, this.page, this.pageSize).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => { this.result.set(result); this.page = result.page; },
      error: (error: HttpErrorResponse) => this.error.set(
        (error.error as { message?: string } | null)?.message || 'Audit logs could not be loaded.')
    });
  }

  applyFilters(): void { this.page = 1; this.load(); }
  clearFilters(): void {
    this.search = ''; this.action = ''; this.resultFilter = ''; this.fromDate = ''; this.toDate = '';
    this.page = 1; this.load();
  }
  previous(): void { if (this.page > 1) { this.page--; this.load(); } }
  next(): void { if (this.page < (this.result()?.totalPages ?? 1)) { this.page++; this.load(); } }

  target(entry: AdminAuditLog): string {
    return entry.entityType ? `${entry.entityType}${entry.entityId ? ` · ${entry.entityId}` : ''}` : '—';
  }

  formattedDetails(entry: AdminAuditLog): string {
    const data: Record<string, unknown> = {};
    if (entry.detailsJson) {
      try { Object.assign(data, JSON.parse(entry.detailsJson) as Record<string, unknown>); }
      catch { data['details'] = 'Details could not be displayed.'; }
    }
    if (entry.correlationId) data['correlationId'] = entry.correlationId;
    return JSON.stringify(data, null, 2);
  }

  hasDetails(entry: AdminAuditLog): boolean { return !!entry.detailsJson || !!entry.correlationId; }

  private dateBoundary(value: string, endOfDay: boolean): string {
    if (!value) return '';
    const date = new Date(`${value}T${endOfDay ? '23:59:59.999' : '00:00:00'}`);
    return Number.isNaN(date.getTime()) ? '' : date.toISOString();
  }
}
