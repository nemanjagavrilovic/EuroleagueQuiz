import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { GameArtComponent } from './game-art.component';
import { RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, GameArtComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomeComponent {
  @ViewChild('guideTabs') private guideTabs?: ElementRef<HTMLDivElement>;
  readonly auth = inject(AuthService);
  activeGuideIndex = 0;
  readonly games = [
    {
      art: 'grid', title: 'Player Grid', description: 'Find players who played for both clubs.', route: '/grid/normal',
      steps: [
        { title: 'Read the clubs', text: 'Each square connects a club across the top with one down the side.' },
        { title: 'Find their connection', text: 'Search for a player who represented both clubs in the available EuroLeague seasons. Select their name to submit.' },
        { title: 'Fill the grid', text: 'A single matching square fills automatically. If several squares are highlighted, tap one to place your answer.' }
      ],
      tip: 'Reveal ends the attempt and fills the remaining squares in red. Your correct answers stay green.'
    },
    {
      art: 'history', title: 'Team History', description: 'A daily career. Four clubs. Up to 100 points.', route: '/team-history',
      steps: [
        { title: 'Start with one club', text: 'One player connects four clubs. You see the first club when the game opens.' },
        { title: 'Guess the player', text: 'Search by first or last name and select a suggestion to submit your guess.' },
        { title: 'Use the next clue', text: 'A miss or Reveal next club uncovers another team. After four clubs, make your final guess or reveal the player.' }
      ],
      tip: 'A correct guess with fewer club clues earns more points.'
    },
    {
      art: 'missing', title: 'Missing Club', description: 'A daily missing club. One answer. 100 points.', route: '/game/missing-club',
      steps: [
        { title: 'Study the career', text: 'Look at the named player and three clubs from their recorded EuroLeague history.' },
        { title: 'Choose the missing club', text: 'Select the club that also belongs to that player’s career from the four options.' },
        { title: 'Confirm your answer', text: 'Press Submit answer when you are ready. A wrong answer or Reveal ends the attempt and shows the correct club.' }
      ],
      tip: 'You can change your selection before submitting. You get one submitted answer.'
    },
    {
      art: 'order', title: 'Career Order', description: 'Order a daily career. Earn up to 100 points.', route: '/game/career-order',
      steps: [
        { title: 'Read the clubs', text: 'The four clubs are consecutive stops from the named player’s recorded EuroLeague career.' },
        { title: 'Arrange the journey', text: 'Use the up and down arrows to put the earliest club at the top and the latest at the bottom.' },
        { title: 'Submit the order', text: 'Confirm your arrangement. Each correct transfer between neighboring clubs earns points when the direction is right.' }
      ],
      tip: 'You can keep rearranging before submitting. Reveal ends the attempt and shows the correct order.'
    },
    {
      art: 'mystery', title: 'Mystery Player', description: 'A daily mystery. Five clues. Up to 100 points.', route: '/game/mystery-player',
      steps: [
        { title: 'Read the first clue', text: 'Start with a detail about the mystery player’s EuroLeague career.' },
        { title: 'Search for the name', text: 'Type a first or last name, then select a player from the suggestions to submit.' },
        { title: 'Uncover more clues', text: 'A miss or Reveal next clue opens another hint. At the fifth clue, make your final guess or choose Reveal player.' }
      ],
      tip: 'Guess correctly with fewer clues to earn more points.'
    },
    {
      art: 'five', title: 'Starting Five', description: 'Five daily rounds. Beat the clock. Earn 100 points.', route: '/game/five-player',
      steps: [
        { title: 'Start when ready', text: 'Read the introduction, then press Start game. A short countdown gets you ready for the first 24-second round.' },
        { title: 'Name five players', text: 'Search and select five players who represented the shown club in any available season. Any position counts.' },
        { title: 'Complete five rounds', text: 'Each round brings a new club and 24 seconds. Press Next round when you are ready to start the next clock.' }
      ],
      tip: 'Each player can be counted once per round. You can use them for another club in a later round.'
    }
  ];
  readonly levels = [
    { name: 'Easy', description: 'More familiar clubs and broader player pools.' },
    { name: 'Normal', description: 'Familiar EuroLeague clubs with several possible answers.' },
    { name: 'Hard', description: 'Rarer club intersections and smaller answer pools.' },
    { name: 'Legend', description: 'The most demanding EuroLeague challenge.' }
  ];

  get activeGuide() { return this.games[this.activeGuideIndex]; }

  onGuideKeydown(event: KeyboardEvent): void {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    let nextIndex: number;
    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        nextIndex = (this.activeGuideIndex + 1) % this.games.length;
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        nextIndex = (this.activeGuideIndex + this.games.length - 1) % this.games.length;
        break;
      case 'Home':
        nextIndex = 0;
        break;
      case 'End':
        nextIndex = this.games.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.activeGuideIndex = nextIndex;
    this.guideTabs?.nativeElement.querySelectorAll<HTMLButtonElement>('button')[nextIndex]?.focus();
  }

  gameLink(game: { art: string; route: string }): string {
    if (!this.auth.user()) return game.route;
    const gameIds: Record<string, string> = {
      history: 'team-history',
      missing: 'missing-club',
      order: 'career-order',
      mystery: 'mystery-player',
      five: 'five-player'
    };
    const gameId = gameIds[game.art] ?? game.art;
    return `/play/${gameId}`;
  }
}
