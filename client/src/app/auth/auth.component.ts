import { HttpErrorResponse } from '@angular/common/http';
import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

interface GoogleCredentialResponse {
  credential: string;
}

interface GoogleIdentityApi {
  initialize(options: { client_id: string; callback: (response: GoogleCredentialResponse) => void }): void;
  renderButton(element: HTMLElement, options: { theme: string; size: string; text: string; shape: string; width: number }): void;
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleIdentityApi } };
  }
}

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [NgIf, ReactiveFormsModule, RouterLink],
  templateUrl: './auth.component.html',
  styleUrl: './auth.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuthComponent implements AfterViewInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly isRegister = inject(ActivatedRoute).snapshot.routeConfig?.path === 'register';
  readonly googleClientIdConfigured = Boolean(environment.googleClientId);
  readonly form = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(100)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email, Validators.maxLength(320)] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8), Validators.maxLength(128)] }),
    confirmPassword: new FormControl('', { nonNullable: true })
  });
  @ViewChild('googleButton') private googleButton?: ElementRef<HTMLDivElement>;
  private static googleIdentityPromise: Promise<void> | null = null;
  loading = false;
  errorMessage = '';

  ngAfterViewInit(): void {
    if (!this.googleClientIdConfigured || !this.googleButton) return;

    this.loadGoogleIdentity().then(() => {
      const identity = window.google?.accounts.id;
      const button = this.googleButton?.nativeElement;
      if (!identity || !button) throw new Error('Google Identity Services did not initialize.');

      identity.initialize({
        client_id: environment.googleClientId,
        callback: response => this.signInWithGoogle(response.credential)
      });
      identity.renderButton(button, {
        theme: 'outline',
        size: 'large',
        text: 'continue_with',
        shape: 'rectangular',
        width: Math.min(button.clientWidth || 360, 400)
      });
    }).catch(() => {
      this.errorMessage = 'Google sign-in could not be loaded. Check your connection and try again.';
    });
  }

  submit(): void {
    this.errorMessage = '';
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { displayName, email, password, confirmPassword } = this.form.getRawValue();
    if (this.isRegister && password !== confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.loading = true;
    const request = this.isRegister
      ? this.auth.register(displayName.trim(), email.trim(), password)
      : this.auth.login(email.trim(), password);

    request.subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: (error: HttpErrorResponse) => {
        this.loading = false;
        this.errorMessage = error.error?.message ?? (error.status === 0
          ? 'Unable to connect to the server. Please try again.'
          : 'Something went wrong. Please try again.');
      }
    });
  }

  showGoogleSetupMessage(): void {
    this.errorMessage = 'Google sign-in is not configured yet. Add a Google OAuth web client ID to the Angular and API configuration.';
  }

  private signInWithGoogle(credential: string): void {
    this.errorMessage = '';
    this.loading = true;
    this.auth.loginWithGoogle(credential).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: (error: HttpErrorResponse) => {
        this.loading = false;
        this.errorMessage = error.error?.message ?? (error.status === 0
          ? 'Unable to connect to the server. Please try again.'
          : 'Google sign-in failed. Please try again.');
      }
    });
  }

  private loadGoogleIdentity(): Promise<void> {
    if (window.google?.accounts.id) return Promise.resolve();
    if (AuthComponent.googleIdentityPromise) return AuthComponent.googleIdentityPromise;

    AuthComponent.googleIdentityPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      script.defer = true;
      script.dataset['googleIdentity'] = 'true';
      script.addEventListener('load', () => resolve(), { once: true });
      script.addEventListener('error', () => reject(new Error('Google Identity Services failed to load.')), { once: true });
      document.head.appendChild(script);
    }).catch(error => {
      AuthComponent.googleIdentityPromise = null;
      throw error;
    });

    return AuthComponent.googleIdentityPromise;
  }
}
