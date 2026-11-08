import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';

type AuthMode = 'login' | 'register' | 'forgot' | 'reset' | 'resend' | 'setup';

@Component({
  selector: 'app-auth-page', standalone: true,
  imports: [NgIf, ReactiveFormsModule, RouterLink],
  templateUrl: './auth-page.component.html', styleUrl: './auth-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuthPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly mode = signal<AuthMode>('login');
  readonly loading = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly showResend = signal(false);
  readonly invalidSetupLink = signal(false);
  readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
    confirmPassword: ['']
  });

  ngOnInit(): void {
    this.mode.set((this.route.snapshot.data['mode'] as AuthMode) || 'login');
    this.form.patchValue({ email: this.route.snapshot.queryParamMap.get('email') || '' });
    if (this.mode() === 'setup' && (!this.route.snapshot.queryParamMap.get('token') || this.form.controls.email.invalid)) {
      this.invalidSetupLink.set(true);
      this.error.set('This password creation link is incomplete. Request a new password email.');
    }
    const googleError = this.route.snapshot.queryParamMap.get('error');
    if (googleError === 'google-cancelled') this.error.set('Google sign-in was cancelled. Try again or sign in with email.');
    else if (googleError === 'google-email') this.error.set('Your Google account did not provide an email address. Sign in with email or choose another Google account.');
    else if (googleError) this.error.set('Google sign-in could not be completed. Please try again.');
  }

  submit(): void {
    if (this.loading() || this.invalidSetupLink()) return;
    this.message.set(''); this.error.set(''); this.showResend.set(false);
    const mode = this.mode();
    const { displayName, email, password, confirmPassword } = this.form.getRawValue();
    if (!email || this.form.controls.email.invalid || ((mode === 'login' || mode === 'register' || mode === 'reset' || mode === 'setup') && !password)) {
      this.form.markAllAsTouched(); return;
    }
    if ((mode === 'register' || mode === 'reset' || mode === 'setup') && this.form.controls.password.invalid) {
      this.error.set('Use a password between 8 and 128 characters.'); return;
    }
    if ((mode === 'register' || mode === 'reset' || mode === 'setup') && password !== confirmPassword) {
      this.error.set('Passwords do not match.'); return;
    }
    this.loading.set(true);
    const request: Observable<unknown> = mode === 'login' ? this.auth.login(email, password)
      : mode === 'register' ? this.auth.register(displayName, email, password)
      : mode === 'forgot' ? this.auth.forgotPassword(email)
      : mode === 'resend' ? this.auth.resendConfirmation(email)
      : mode === 'setup' ? this.auth.setPassword(email, this.route.snapshot.queryParamMap.get('token') || '', password)
      : this.auth.resetPassword(email, this.route.snapshot.queryParamMap.get('token') || '', password);
    request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (response: unknown) => {
        if (mode === 'login') void this.router.navigateByUrl(this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')));
        else {
          this.message.set(this.responseMessage(response));
          if (mode === 'register') this.showResend.set(true);
          if (mode === 'setup' || mode === 'reset') this.form.patchValue({ password: '', confirmPassword: '' });
        }
      },
      error: (response: { status?: number; error?: { message?: string; errors?: { description?: string }[] | Record<string, string[]> } }) => {
        if (mode === 'register' && response.status === 503) {
          this.message.set(response.error?.message || 'Your account was created, but the confirmation email could not be sent.');
          this.showResend.set(true);
          return;
        }
        if (mode === 'login' && response.status === 403) {
          this.error.set(response.error?.message || 'Confirm your email before signing in.');
          this.showResend.set(true);
          return;
        }
        const errors = response.error?.errors;
        const details = Array.isArray(errors) ? errors.map(item => item.description).filter(Boolean).join(' ')
          : errors ? Object.values(errors).flat().join(' ') : '';
        this.error.set(details || response.error?.message || 'Something went wrong. Please try again.');
      }
    });
  }

  google(): void { this.auth.beginGoogleLogin(this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))); }
  resend(): void {
    const email = this.form.controls.email.value;
    if (!email || this.form.controls.email.invalid) return;
    this.loading.set(true); this.error.set('');
    this.auth.resendConfirmation(email).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => this.message.set(response.message),
      error: response => this.error.set(response.error?.message || 'The confirmation email could not be sent. Please try again shortly.')
    });
  }
  private responseMessage(response: unknown): string {
    return typeof response === 'object' && response !== null && 'message' in response && typeof response.message === 'string' ? response.message : 'Done.';
  }
  private safeReturnUrl(value: string | null): string { return value?.startsWith('/') && !value.startsWith('//') ? value : '/'; }
}
