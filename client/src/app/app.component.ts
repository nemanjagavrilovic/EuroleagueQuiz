import { ChangeDetectionStrategy, Component, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
import { SeoService } from './seo.service';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from './auth.service';
import { ConfirmationModalComponent } from './shared/confirmation-modal/confirmation-modal.component';
import { CookieConsentComponent } from './shared/cookie-consent/cookie-consent.component';
import { CookieConsentService } from './shared/cookie-consent/cookie-consent.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [NgIf, RouterOutlet, RouterLink, RouterLinkActive, ConfirmationModalComponent, CookieConsentComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  @ViewChild('accountMenu') private accountMenu?: ElementRef<HTMLElement>;
  private readonly seo = inject(SeoService);
  readonly year = new Date().getFullYear();
  readonly auth = inject(AuthService);
  readonly cookieConsent = inject(CookieConsentService);
  profileMenuOpen = false;

  toggleProfileMenu(): void { this.profileMenuOpen = !this.profileMenuOpen; }
  closeProfileMenu(): void { this.profileMenuOpen = false; }

  logout(): void {
    this.closeProfileMenu();
    this.auth.logout();
  }

  @HostListener('document:click', ['$event'])
  closeProfileMenuFromOutside(event: MouseEvent): void {
    if (this.profileMenuOpen && !this.accountMenu?.nativeElement.contains(event.target as Node)) this.closeProfileMenu();
  }

  @HostListener('document:keydown.escape')
  closeProfileMenuFromKeyboard(): void { this.closeProfileMenu(); }
}
