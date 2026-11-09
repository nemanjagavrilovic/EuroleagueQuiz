import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AdminUser, AdminUserPage, AdminUsersService } from './admin-users.service';
import { AuthService } from './auth.service';
import { ConfirmationModalService } from './shared/confirmation-modal/confirmation-modal.service';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminUsersComponent {
  private readonly usersApi = inject(AdminUsersService);
  private readonly confirmation = inject(ConfirmationModalService);
  readonly auth = inject(AuthService);
  readonly result = signal<AdminUserPage | null>(null);
  readonly loading = signal(false);
  readonly actionUserId = signal<string | null>(null);
  readonly message = signal('');
  readonly error = signal('');
  search = '';
  includeDeleted = true;
  page = 1;
  readonly pageSize = 25;

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.usersApi.getUsers(this.search, this.page, this.pageSize, this.includeDeleted).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: result => { this.result.set(result); this.page = result.page; },
      error: error => this.error.set(this.errorMessage(error, 'Users could not be loaded.'))
    });
  }

  searchUsers(): void { this.page = 1; this.load(); }
  toggleDeleted(): void { this.page = 1; this.load(); }
  previous(): void { if (this.page > 1) { this.page--; this.load(); } }
  next(): void { if (this.page < (this.result()?.totalPages ?? 1)) { this.page++; this.load(); } }

  async sendPasswordReset(user: AdminUser): Promise<void> {
    const confirmed = await this.confirmation.confirm({
      title: 'Set a temporary password?',
      message: `A temporary password will be emailed to ${user.email}. Their current password and active sessions will be invalidated.`,
      confirmLabel: 'Set temporary password'
    });
    if (!confirmed) return;

    this.runAction(user, () => this.usersApi.sendPasswordReset(user.id));
  }

  resendConfirmation(user: AdminUser): void {
    this.runAction(user, () => this.usersApi.resendConfirmation(user.id));
  }

  async deleteUser(user: AdminUser): Promise<void> {
    if (!this.canDelete(user)) return;
    const confirmed = await this.confirmation.confirm({
      title: 'Delete this user?',
      message: `${user.displayName} (${user.email}) will be signed out and unable to sign in. Their stored account remains available for audit purposes.`,
      confirmLabel: 'Delete user',
      tone: 'danger'
    });
    if (!confirmed) return;
    this.runAction(user, () => this.usersApi.deleteUser(user.id), true);
  }

  canDelete(user: AdminUser): boolean {
    return !user.isDeleted && user.id !== this.auth.user()?.id && !user.roles.includes('Admin');
  }

  private runAction(user: AdminUser, request: () => ReturnType<AdminUsersService['deleteUser']>, reload = false): void {
    if (this.actionUserId()) return;
    this.actionUserId.set(user.id);
    this.message.set('');
    this.error.set('');
    request().pipe(finalize(() => this.actionUserId.set(null))).subscribe({
      next: response => {
        this.message.set(response.message);
        if (reload) this.load();
      },
      error: error => this.error.set(this.errorMessage(error, 'The action could not be completed.'))
    });
  }

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    return (error.error as { message?: string } | null)?.message || fallback;
  }
}
