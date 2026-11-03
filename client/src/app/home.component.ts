import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { GameArtComponent } from './game-art.component';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, GameArtComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomeComponent {
  readonly games = [
    { art: 'grid', title: 'Player Grid', description: 'Find players who played for both clubs.', route: '/grid/normal' },
    { art: 'history', title: 'Team History', description: 'A daily career. Four clubs. Up to 100 points.', route: '/team-history' },
    { art: 'missing', title: 'Missing Club', description: 'A daily missing club. One answer. 100 points.', route: '/game/missing-club' },
    { art: 'order', title: 'Career Order', description: 'Order a daily career. Earn up to 100 points.', route: '/game/career-order' },
    { art: 'mystery', title: 'Mystery Player', description: 'A daily mystery. Five clues. Up to 100 points.', route: '/game/mystery-player' },
    { art: 'five', title: 'Starting Five', description: 'Five daily rounds. Beat the clock. Earn 100 points.', route: '/game/five-player' }
  ];
  readonly levels = [
    { name: 'Easy', description: 'More familiar clubs and broader player pools.' },
    { name: 'Normal', description: 'A balanced challenge.' },
    { name: 'Hard', description: 'Rarer club intersections and smaller answer pools.' },
    { name: 'Legend', description: 'The most demanding EuroLeague challenge.' }
  ];
}
