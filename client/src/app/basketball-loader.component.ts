import { ChangeDetectionStrategy, Component, DestroyRef, Input, inject, signal } from '@angular/core';

interface NetMesh {
  front: string;
  back: string;
  frontKnots: string;
  backKnots: string;
}

@Component({
  selector: 'app-basketball-loader',
  standalone: true,
  template: `
    <div class="loader" [class.overlay]="overlay" role="status" aria-live="polite">
      <div class="loader-panel">
        <div class="loader-brand" aria-hidden="true"><img src="assets/branding/pick-and-roll-hoop-badge.svg" width="28" height="28" alt=""> PICK &amp; ROLL</div>
        <svg class="basketball-scene" viewBox="0 0 400 280" aria-hidden="true" focusable="false">
          <defs>
            <linearGradient [attr.id]="rimId" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stop-color="#ffc18a" />
              <stop offset=".35" stop-color="#ff8a32" />
              <stop offset="1" stop-color="#b84319" />
            </linearGradient>
            <radialGradient [attr.id]="glowId">
              <stop offset="0" stop-color="#ff8a32" stop-opacity=".13" />
              <stop offset="1" stop-color="#ff8a32" stop-opacity="0" />
            </radialGradient>
            <radialGradient [attr.id]="shadowId">
              <stop offset="0" stop-color="#02080d" stop-opacity=".7" />
              <stop offset="1" stop-color="#02080d" stop-opacity="0" />
            </radialGradient>
            <linearGradient [attr.id]="netId" gradientUnits="userSpaceOnUse" x1="222" y1="140" x2="310" y2="210">
              <stop offset="0" stop-color="#f9f5eb" />
              <stop offset=".5" stop-color="#cdd2d3" />
              <stop offset="1" stop-color="#f2eee5" />
            </linearGradient>
          </defs>

          <ellipse cx="265" cy="155" rx="128" ry="112" [attr.fill]="'url(#' + glowId + ')'" />
          <path class="court-line" d="M30 246 H370 M117 246 Q200 203 283 246" />
          <ellipse cx="272" cy="246" rx="74" ry="13" [attr.fill]="'url(#' + shadowId + ')'" />

          <!-- Rear strings and rear rim sit behind the incoming ball. -->
          <g class="net net-back">
            <path [attr.d]="net.back">
              @if (!reducedMotion()) {
                <animate attributeName="d" dur="2.8s" repeatCount="indefinite"
                  keyTimes="0;.66677;.72;.78;.85;.93;1" [attr.values]="netAnimation.back" />
              }
            </path>
            <path class="net-knots" [attr.d]="net.backKnots">
              @if (!reducedMotion()) {
                <animate attributeName="d" dur="2.8s" repeatCount="indefinite"
                  keyTimes="0;.66677;.72;.78;.85;.93;1" [attr.values]="netAnimation.backKnots" />
              }
            </path>
          </g>
          <path class="rim rim-back" d="M220 140 C220 127 320 127 320 140" [attr.stroke]="'url(#' + rimId + ')'" />

          <g class="ball-flight">
            <animateMotion dur="2.8s" repeatCount="indefinite" calcMode="paced"
              path="M-34 179 Q110 -21 270 139 Q279 148 275 322" />
            <g class="ball-spin">
              <animateTransform attributeName="transform" type="rotate" from="-30" to="390" dur="2.8s" repeatCount="indefinite" />
              <image href="assets/loader/basketball.png" x="-32" y="-32" width="64" height="64" />
            </g>
          </g>

          <!-- A still frame replaces the shot when reduced motion is preferred. -->
          <image class="resting-ball" href="assets/loader/basketball.png" x="238" y="72" width="64" height="64" />

          <g class="net net-front" [attr.stroke]="'url(#' + netId + ')'">
            <path [attr.d]="net.front">
              @if (!reducedMotion()) {
                <animate attributeName="d" dur="2.8s" repeatCount="indefinite"
                  keyTimes="0;.66677;.72;.78;.85;.93;1" [attr.values]="netAnimation.front" />
              }
            </path>
            <path class="net-knots" [attr.d]="net.frontKnots">
              @if (!reducedMotion()) {
                <animate attributeName="d" dur="2.8s" repeatCount="indefinite"
                  keyTimes="0;.66677;.72;.78;.85;.93;1" [attr.values]="netAnimation.frontKnots" />
              }
            </path>
          </g>

          <path class="rim rim-front" d="M220 140 C220 153 320 153 320 140" [attr.stroke]="'url(#' + rimId + ')'" />
          <path class="rim-highlight" d="M224 142 C234 151 307 151 317 142" />
        </svg>

        <div class="loader-caption">
          <span class="loader-message">{{ message }}</span>
          <div class="loading-track" aria-hidden="true"><span></span></div>
        </div>
      </div>
    </div>
  `,
  styleUrl: './basketball-loader.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BasketballLoaderComponent {
  private static nextId = 0;
  private readonly instanceId = BasketballLoaderComponent.nextId++;
  readonly rimId = 'loader-rim-' + this.instanceId;
  readonly glowId = 'loader-glow-' + this.instanceId;
  readonly shadowId = 'loader-shadow-' + this.instanceId;
  readonly netId = 'loader-net-' + this.instanceId;
  readonly net = this.createNet(0);
  readonly netAnimation = this.createNetAnimation();
  readonly reducedMotion = signal(false);

  @Input() message = 'Loading…';
  @Input() overlay = false;

  constructor() {
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
    const updateMotion = () => this.reducedMotion.set(preference.matches);
    updateMotion();
    preference.addEventListener('change', updateMotion);
    inject(DestroyRef).onDestroy(() => preference.removeEventListener('change', updateMotion));
  }

  private createNetAnimation(): NetMesh {
    const frames = [0, 0, .45, 1, -.12, 0, 0].map(stretch => this.createNet(stretch));
    return {
      front: frames.map(frame => frame.front).join(';'),
      back: frames.map(frame => frame.back).join(';'),
      frontKnots: frames.map(frame => frame.frontKnots).join(';'),
      backKnots: frames.map(frame => frame.backKnots).join(';')
    };
  }

  private createNet(stretch: number): NetMesh {
    const mesh: NetMesh = { front: '', back: '', frontKnots: '', backKnots: '' };
    const rings = [
      { radius: 50, y: 140, depth: 9.75, widen: 0, drop: 0 },
      { radius: 46, y: 155, depth: 8.5, widen: .75, drop: 1 },
      { radius: 40, y: 171, depth: 7.5, widen: 3, drop: 2 },
      { radius: 34, y: 187, depth: 6.5, widen: 5, drop: 5 },
      { radius: 32, y: 202, depth: 5.5, widen: 6, drop: 7 }
    ];
    const number = (value: number) => value.toFixed(2);
    const point = (row: number, column: number) => {
      const ring = rings[row];
      const angle = (column + (row % 2) / 2) * Math.PI / 6;
      return {
        x: 270 + (ring.radius + ring.widen * stretch) * Math.cos(angle),
        y: ring.y + ring.drop * stretch + ring.depth * Math.sin(angle),
        front: Math.sin(angle)
      };
    };

    // Staggered rings form regular diamonds on a tapered, open cylinder.
    // Every cord meets a shared knot; the top ring stays fixed to the rim.
    for (let row = 0; row < rings.length; row++) {
      for (let column = 0; column < 12; column++) {
        const start = point(row, column);
        const side = start.front >= 0 ? 'frontKnots' : 'backKnots';
        mesh[side] += `M${number(start.x - .7)},${number(start.y)}a.7,.7 0 1,0 1.4,0a.7,.7 0 1,0 -1.4,0`;
        if (row === rings.length - 1) {
          const end = point(row, column + 1);
          const middle = point(row, column + .5);
          const layer = middle.front >= 0 ? 'front' : 'back';
          mesh[layer] += `M${number(start.x)},${number(start.y)}Q${number(middle.x)},${number(middle.y + 2)} ${number(end.x)},${number(end.y)}`;
          continue;
        }
        for (const offset of [0, row % 2 ? 1 : -1]) {
          const end = point(row + 1, column + offset);
          const layer = start.front + end.front >= 0 ? 'front' : 'back';
          mesh[layer] += `M${number(start.x)},${number(start.y)}Q${number((start.x + end.x) / 2)},${number((start.y + end.y) / 2 + .65)} ${number(end.x)},${number(end.y)}`;
        }
      }
    }
    return mesh;
  }
}
