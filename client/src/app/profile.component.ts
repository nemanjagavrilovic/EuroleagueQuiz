import { ChangeDetectionStrategy, Component, HostListener, effect, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [NgIf, ReactiveFormsModule, RouterLink],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileComponent {
  readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  readonly saving = signal(false);
  readonly sendingReset = signal(false);
  readonly profileMessage = signal('');
  readonly profileError = signal('');
  readonly passwordMessage = signal('');
  readonly passwordError = signal('');
  readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(50), Validators.pattern(/\S/)]],
    lastName: ['', [Validators.required, Validators.maxLength(50), Validators.pattern(/\S/)]],
    phoneNumber: ['', [Validators.required, Validators.minLength(7), Validators.maxLength(30), Validators.pattern(/^\+?[0-9 ()-]+$/)]]
  });

  constructor() {
    effect(() => {
      const user = this.auth.user();
      if (user && !this.form.dirty) {
        this.form.patchValue({ firstName: user.firstName ?? '', lastName: user.lastName ?? '', phoneNumber: user.phoneNumber ?? '' });
      }
    });
  }

  save(): void {
    if (this.saving() || this.sendingReset()) return;
    this.profileMessage.set('');
    this.profileError.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const { firstName, lastName, phoneNumber } = this.form.getRawValue();
    this.saving.set(true);
    this.auth.updateProfile(firstName.trim(), lastName.trim(), phoneNumber.trim()).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: user => {
        this.form.reset({ firstName: user.firstName ?? '', lastName: user.lastName ?? '', phoneNumber: user.phoneNumber ?? '' });
        this.profileMessage.set('Your profile has been updated.');
      },
      error: (error: HttpErrorResponse) => this.profileError.set(this.errorMessage(error, 'Your profile could not be saved. Please try again.'))
    });
  }

  @HostListener('window:focus')
  refreshProfile(): void {
    const current = this.auth.user();
    if (!current) return;
    this.auth.refreshUser().subscribe({
      next: user => {
        if (user.hasPassword !== current.hasPassword) {
          this.passwordMessage.set('');
          this.passwordError.set('');
        }
      },
      error: () => { /* Keep the existing profile during temporary connectivity problems. */ }
    });
  }

  sendPasswordReset(): void {
    const user = this.auth.user();
    if (this.sendingReset() || this.saving() || !user) return;
    this.passwordMessage.set('');
    this.passwordError.set('');
    this.sendingReset.set(true);
    const request = user.hasPassword ? this.auth.requestPasswordResetLink() : this.auth.requestPasswordSetupLink();
    request.pipe(
      finalize(() => this.sendingReset.set(false))
    ).subscribe({
      next: response => this.passwordMessage.set(response.message),
      error: (error: HttpErrorResponse) => this.passwordError.set(this.errorMessage(error, 'The password email could not be sent. Please try again.'))
    });
  }

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    const body = error.error as { message?: string; errors?: { description?: string }[] | Record<string, string[]> } | null;
    const details = Array.isArray(body?.errors)
      ? body.errors.map(item => item.description).filter(Boolean).join(' ')
      : body?.errors ? Object.values(body.errors).flat().join(' ') : '';
    return details || body?.message || fallback;
  }
}
