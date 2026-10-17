import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { NgFor } from '@angular/common';

@Component({
  selector: 'app-shot-clock',
  standalone: true,
  imports: [NgFor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 140 100" role="img" [attr.aria-label]="seconds + ' seconds remaining'">
      <rect width="140" height="100" rx="6" fill="#050505" />
      <g *ngFor="let digit of digits; let index = index" [attr.transform]="'translate(' + (index * 64 + 9) + ', 8)'">
        <polygon *ngFor="let points of segments; let segment = index"
          [attr.points]="points" [attr.fill]="patterns[digit].includes(segment) ? '#ff1616' : '#240808'" />
      </g>
    </svg>
  `,
  styles: [`
    :host { display: block; width: 88px; max-width: 100%; }
    svg { display: block; width: 100%; height: auto; }
    @media (max-width: 600px) { :host { width: 76px; } }
  `]
})
export class ShotClockComponent {
  @Input() seconds = 24;

  readonly segments = [
    '8,0 44,0 51,7 44,14 8,14 1,7',
    '46,17 53,10 60,17 60,36 53,43 46,36',
    '46,48 53,41 60,48 60,67 53,74 46,67',
    '8,70 44,70 51,77 44,84 8,84 1,77',
    '-2,48 5,41 12,48 12,67 5,74 -2,67',
    '-2,17 5,10 12,17 12,36 5,43 -2,36',
    '8,35 44,35 51,42 44,49 8,49 1,42'
  ];
  readonly patterns: readonly (readonly number[])[] = [
    [0, 1, 2, 3, 4, 5], [1, 2], [0, 1, 6, 4, 3], [0, 1, 6, 2, 3],
    [5, 6, 1, 2], [0, 5, 6, 2, 3], [0, 5, 6, 4, 2, 3], [0, 1, 2],
    [0, 1, 2, 3, 4, 5, 6], [0, 1, 2, 3, 5, 6]
  ];

  get digits(): number[] {
    return Math.max(0, Math.min(99, Math.ceil(this.seconds))).toString().padStart(2, '0').split('').map(Number);
  }
}
