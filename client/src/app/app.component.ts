import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { SeoService } from './seo.service';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [NgIf, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  private readonly seo = inject(SeoService);
  readonly year = new Date().getFullYear();
  readonly auth = inject(AuthService);
}
