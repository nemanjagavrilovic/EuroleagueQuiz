import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { AuthService } from '../auth/auth.service';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomeComponent {
  readonly auth = inject(AuthService);

  logout(): void {
    this.auth.logout();
  }
  readonly levels = [
    { name: 'Easy', description: 'More familiar clubs and broader player pools.' },
    { name: 'Normal', description: 'A balanced daily challenge.' },
    { name: 'Hard', description: 'Rarer club intersections and smaller answer pools.' },
    { name: 'Legend', description: 'The most demanding EuroLeague challenge.' }
  ];
}
