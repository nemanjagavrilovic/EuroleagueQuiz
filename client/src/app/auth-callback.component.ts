import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-auth-callback', standalone: true, imports: [NgIf, RouterLink],
  template: `<main class="status"><div class="ball" aria-hidden="true"></div><h1>Signing you in…</h1><p *ngIf="error()">{{ error() }}</p><a *ngIf="error()" routerLink="/login">Return to sign in</a></main>`,
  styles: [`.status{min-height:60vh;display:grid;place-content:center;text-align:center}.ball{width:54px;height:54px;margin:auto;border:4px solid var(--accent);border-top-color:transparent;border-radius:50%;animation:spin .8s linear infinite}h1{font-size:26px}p{color:var(--error-text)}a{color:var(--accent)}@keyframes spin{to{transform:rotate(360deg)}}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuthCallbackComponent implements OnInit {
  private readonly auth = inject(AuthService); private readonly route = inject(ActivatedRoute);
  readonly error = signal('');
  ngOnInit(): void { this.auth.completeGoogleLogin(this.route.snapshot.queryParamMap.get('returnUrl') || '/').catch(() => this.error.set('Google sign-in could not be completed.')); }
}
