import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-confirm-email', standalone: true, imports: [RouterLink],
  template: `<main class="status"><p class="eyebrow">ACCOUNT ACTIVATION</p><h1>{{ title() }}</h1><p>{{ message() }}</p><a routerLink="/login">Continue to sign in</a></main>`,
  styles: [`.status{width:min(560px,calc(100% - 32px));min-height:55vh;margin:auto;display:grid;place-content:center;text-align:center}.eyebrow{color:var(--accent);font-size:11px;font-weight:800;letter-spacing:.15em}h1{font-size:34px;margin:4px 0}p{color:var(--text-secondary);line-height:1.6}a{display:inline-block;margin:auto;padding:12px 20px;border-radius:8px;background:var(--accent);color:var(--on-accent);font-weight:800;text-decoration:none}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute); private readonly auth = inject(AuthService);
  readonly title = signal('Confirming your email…'); readonly message = signal('Please wait a moment.');
  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId') || '';
    const token = this.route.snapshot.queryParamMap.get('token') || '';
    this.auth.confirmEmail(userId, token).subscribe({
      next: response => { this.title.set('Email confirmed'); this.message.set(response.message); },
      error: response => { this.title.set('Link unavailable'); this.message.set(response.error?.message || 'This confirmation link is invalid or expired.'); }
    });
  }
}
