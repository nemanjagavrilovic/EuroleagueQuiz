import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-basketball-loader',
  standalone: true,
  template: `
    <div class="loader" [class.overlay]="overlay" role="status" aria-live="polite">
      <div class="loader-panel">
      <svg class="image-scene" viewBox="0 0 360 300" aria-hidden="true" focusable="false">
        <image href="assets/loader/compact-hoop.png" x="190" y="88" width="120" height="120" />
        <g class="ball-flight">
          <animateMotion dur="2.8s" repeatCount="indefinite"
            path="M 45 180 C 75 70 160 22 210 72 Q 230 92 250 112 L 250 242" />
          <g>
            <animateTransform attributeName="transform" type="rotate" from="0" to="540" dur="2.8s" repeatCount="indefinite" />
            <image href="assets/loader/basketball.png" x="-33" y="-33" width="66" height="66" />
          </g>
        </g>
        <!-- Front rim and net overlap the ball as it passes through. -->
        <svg x="190" y="112" width="120" height="96" viewBox="0 24 120 96" overflow="hidden">
          <image href="assets/loader/compact-hoop.png" width="120" height="120" />
        </svg>
        <image class="resting-ball" href="assets/loader/basketball.png" x="12" y="147" width="66" height="66" />
      </svg>
      <span>{{ message }}</span>
      </div>
    </div>
  `,
  styleUrl: './basketball-loader.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BasketballLoaderComponent {
  @Input() message = 'Loading…';
  @Input() overlay = false;
}
