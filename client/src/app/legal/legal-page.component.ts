import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CookieConsentService } from '../shared/cookie-consent/cookie-consent.service';
import { legalDetails, privacyPolicy, termsOfUse } from './legal-content';

@Component({
  selector: 'app-legal-page',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink],
  templateUrl: './legal-page.component.html',
  styleUrl: './legal-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LegalPageComponent {
  private readonly route = inject(ActivatedRoute);
  readonly consent = inject(CookieConsentService);
  readonly isPrivacy = this.route.snapshot.data['document'] === 'privacy';
  readonly path = this.isPrivacy ? '/privacy' : '/terms';
  readonly document = this.isPrivacy ? privacyPolicy : termsOfUse;
  readonly details = legalDetails;
  readonly storage = [
    { name: 'Sign-in cookies', purpose: 'Keep your session secure and complete Google sign-in.', duration: 'Session or configured expiry; signing out removes the portal refresh cookie.', category: 'Essential' },
    { name: 'Guest daily identity', purpose: 'Recognise your guest daily progress and attempts.', duration: 'Local storage until you clear it; clearing it does not delete server records.', category: 'Essential' },
    { name: 'Privacy and interface choices', purpose: 'Remember cookie choices, viewed instructions and dismissed notices.', duration: 'Local storage until cleared or reset by a settings update.', category: 'Essential' },
    { name: 'Google Analytics', purpose: 'Measure visits, campaigns and supported game interactions.', duration: 'Provider-managed expiry and the Analytics property retention settings.', category: 'Optional analytics' },
    { name: 'Meta Pixel', purpose: 'Measure advertising and support advertising audiences.', duration: 'Provider-managed cookie expiry and retention.', category: 'Optional marketing' }
  ];
}
