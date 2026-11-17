import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CookieConsentService } from './cookie-consent.service';

@Component({
  selector: 'app-cookie-consent',
  standalone: true,
  imports: [NgIf, RouterLink],
  templateUrl: './cookie-consent.component.html',
  styleUrl: './cookie-consent.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CookieConsentComponent {
  readonly consent = inject(CookieConsentService);
  readonly customizing = signal(false);
  readonly analyticsEnabled = signal(false);
  readonly marketingEnabled = signal(false);

  customize(): void {
    this.analyticsEnabled.set(this.consent.preferences()?.analytics ?? false);
    this.marketingEnabled.set(this.consent.preferences()?.marketing ?? false);
    this.customizing.set(true);
  }

  back(): void { this.customizing.set(false); }

  readPrivacy(): void {
    this.customizing.set(false);
    this.consent.visible.set(false);
  }

  save(): void {
    this.consent.save(this.analyticsEnabled(), this.marketingEnabled());
    this.customizing.set(false);
  }

  acceptAll(): void {
    this.consent.acceptAll();
    this.customizing.set(false);
  }

  essentialOnly(): void {
    this.consent.essentialOnly();
    this.customizing.set(false);
  }

  @HostListener('document:keydown.escape')
  closeFromKeyboard(): void {
    if (this.consent.preferences()) {
      this.customizing.set(false);
      this.consent.close();
    }
  }
}
