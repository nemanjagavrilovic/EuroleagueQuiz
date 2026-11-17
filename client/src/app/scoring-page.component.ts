import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-scoring-page',
  standalone: true,
  imports: [NgFor, RouterLink],
  templateUrl: './scoring-page.component.html',
  styleUrl: './scoring-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScoringPageComponent {
  readonly sections = [
    { id: 'player-grid', title: 'Player Grid' },
    { id: 'team-history', title: 'Team History' },
    { id: 'missing-club', title: 'Missing Club' },
    { id: 'career-order', title: 'Career Order' },
    { id: 'mystery-player', title: 'Mystery Player' },
    { id: 'starting-five', title: 'Starting Five' },
    { id: 'leaderboard', title: 'Leaderboard' }
  ];
  readonly gridLevels = [
    { name: 'Easy', perSquare: 1, bonus: 1, maximum: 10 },
    { name: 'Normal', perSquare: 2, bonus: 2, maximum: 20 },
    { name: 'Hard', perSquare: 3, bonus: 3, maximum: 30 },
    { name: 'Legend', perSquare: 4, bonus: 4, maximum: 40 }
  ];
  readonly historyPoints = [100, 75, 50, 25];
  readonly transferPoints = [0, 33, 67, 100];
  readonly mysteryPoints = [100, 80, 60, 40, 20];
}
