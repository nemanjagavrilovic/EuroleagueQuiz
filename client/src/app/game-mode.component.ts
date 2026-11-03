import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-game-mode', standalone: true, imports: [RouterLink],
  templateUrl: './game-mode.component.html', styleUrl: './game-mode.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameModeComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly requestedGame = this.route.snapshot.paramMap.get('game') || 'grid';
  readonly game = this.requestedGame === 'history' ? 'team-history'
    : this.requestedGame === 'missing' ? 'missing-club'
    : this.requestedGame === 'order' ? 'career-order'
    : this.requestedGame === 'mystery' ? 'mystery-player'
    : this.requestedGame === 'five' ? 'five-player'
    : this.requestedGame;
  readonly difficulty = this.route.snapshot.queryParamMap.get('difficulty') || 'normal';
  readonly title = this.game === 'grid' ? 'Player Grid' : this.game === 'team-history' ? 'Team History'
    : this.game === 'missing-club' ? 'Missing Club' : this.game === 'career-order' ? 'Career Order'
    : this.game === 'mystery-player' ? 'Mystery Player' : 'Starting Five';
  readonly destination = this.game === 'grid' ? ['/grid', this.difficulty]
    : this.game === 'team-history' ? ['/team-history']
    : this.game === 'five-player' ? ['/game/five-player'] : ['/game', this.game];
}
