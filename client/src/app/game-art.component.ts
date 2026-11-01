import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { NgFor, NgSwitch, NgSwitchCase } from '@angular/common';

@Component({
  selector: 'app-game-art', standalone: true, imports: [NgFor, NgSwitch, NgSwitchCase],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 280 128" fill="none" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient [attr.id]="'pnr-art-surface-' + game" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#3a5b73"/><stop offset=".42" stop-color="#203b50"/><stop offset="1" stop-color="#102437"/></linearGradient>
        <linearGradient [attr.id]="'pnr-art-metal-' + game" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#a4bdce"/><stop offset=".4" stop-color="#57778e"/><stop offset="1" stop-color="#263e51"/></linearGradient>
      </defs>
      <g [ngSwitch]="game" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round">
        <g *ngSwitchCase="'grid'">
          <ellipse cx="140" cy="122" rx="64" ry="4" fill="#030c14" stroke="none" opacity=".6"/>
          <rect x="79" y="8" width="123" height="111" rx="9" fill="#081622" stroke="#7290a5"/>
          <path d="M88 11h105" stroke="#b4c8d5" opacity=".35"/>
          <rect *ngFor="let cell of cells" [attr.x]="cell[0]" [attr.y]="cell[1]" width="31" height="28" rx="5" [attr.fill]="surfaceFill" stroke="#54748c"/>
          <path d="M92 20h23m13 0h23m13 0h23M92 54h23m49 0h23M92 88h23m13 0h23m13 0h23" stroke="#b5cbd9" stroke-width=".7" opacity=".3"/>
          <g *ngFor="let point of [[103,30],[175,64],[103,98]]" [attr.transform]="'translate(' + point[0] + ',' + point[1] + ')'" fill="#adc4d3" stroke="none" opacity=".8"><circle cy="-4" r="3.5"/><path d="M-7 7V5c0-6 14-6 14 0v2Z"/></g>
          <path d="m170 24 5-2 5 2v6q0 5-5 8-5-3-5-8ZM134 92l6-2 6 2v7q0 5-6 8-6-3-6-8Z" stroke="#a7bdcb" stroke-width="1.2"/>
          <rect x="124" y="50" width="31" height="28" rx="5" fill="#493020" class="accent" />
          <text x="140" y="70" class="accent" text-anchor="middle" stroke="none" font-size="21">?</text>
        </g>
        <g *ngSwitchCase="'history'">
          <defs>
            <linearGradient id="pnr-history-fabric" x1="104" y1="58" x2="178" y2="68" gradientUnits="userSpaceOnUse"><stop stop-color="#35576d"/><stop offset=".24" stop-color="#203e54"/><stop offset=".58" stop-color="#294c64"/><stop offset="1" stop-color="#132b3e"/></linearGradient>
            <pattern id="pnr-history-mesh" width="4" height="4" patternUnits="userSpaceOnUse"><circle cx="2" cy="2" r=".45" fill="#acc2d2" opacity=".18" stroke="none"/></pattern>
          </defs>
          <ellipse cx="140" cy="118" rx="43" ry="4" fill="#030c14" opacity=".5" stroke="none"/>
          <path d="m112 16 14 4q14 21 28 0l14-4 6 9c-12 17-10 27 2 37l2 48q-38 10-76 0l2-48c12-10 14-20 2-37Z" fill="url(#pnr-history-fabric)" stroke="#7896aa" stroke-width="1.3"/>
          <path d="m112 16 14 4q14 21 28 0l14-4 6 9c-12 17-10 27 2 37l2 48q-38 10-76 0l2-48c12-10 14-20 2-37Z" fill="url(#pnr-history-mesh)" stroke="none"/>
          <path d="m126 20 2 7q12 19 24 0l2-7M109 25c12 17 10 29-3 40m65-40c-12 17-10 29 3 40" stroke="#ff8a32" stroke-width="3"/>
          <path d="m122 22 3 9q15 21 30 0l3-9M109 68l-2 38m64-38 2 38M106 107q34 8 68 0" stroke="#adc1ce" stroke-width=".7" opacity=".45" stroke-dasharray="2 3"/>
          <path d="m116 67-4 33m51-36 5 39m-40-8-4 12m20-14 5 16" stroke="#8baabc" stroke-width="1.3" opacity=".22"/>
          <text x="140" y="54" text-anchor="middle" stroke="none" font-size="6.5" font-weight="700" letter-spacing="1" class="jersey-lettering">PICK &amp; ROLL</text>
          <text x="140" y="86" text-anchor="middle" stroke="none" font-size="31" font-weight="800" class="jersey-lettering">23</text>
          <rect x="111" y="101" width="12" height="5" rx="1" fill="#d9e3e9" stroke="none" opacity=".8"/>
          <path d="M114 103.5h6" stroke="#203e54" stroke-width="1"/>
          <path d="M56 97h30m-7-6 7 6-7 6M194 97h30m-7-6 7 6-7 6" class="accent" />
        </g>
        <g *ngSwitchCase="'missing'">
          <ellipse cx="140" cy="113" rx="120" ry="5" fill="#030c14" opacity=".5" stroke="none"/>
          <g *ngFor="let x of [44,108,172]; let i = index" [attr.transform]="'translate(' + x + ',65)'">
            <path d="M0-39 27-28v28c0 19-27 38-27 38S-27 19-27 0v-28Z" [attr.fill]="metalFill" stroke="#8da8bb"/>
            <path d="M0-33 21-24V0c0 14-21 30-21 30S-21 14-21 0v-24Z" [attr.fill]="surfaceFill" stroke="#142b3d"/>
            <path d="M-17-20 17 5M-17-9 12 13" [attr.stroke]="i === 1 ? '#d78a4d' : '#819fb6'" stroke-width="5" opacity=".45"/>
            <path d="m0-27 1.7 3.4 3.8.6-2.8 2.7.7 3.8L0-18.3l-3.4 1.8.7-3.8-2.8-2.7 3.8-.6Z" fill="#ffb16e" stroke="none"/>
            <circle cy="2" r="10" fill="#173044" stroke="#b3c7d5" stroke-width="1.1"/>
            <path d="M-10 2h20M0-8v20M-7-5Q4 2-7 9M7-5Q-4 2 7 9" stroke="#b3c7d5" stroke-width=".8"/>
            <path d="M-8 20q8 5 16 0" stroke="#a6bece" stroke-width="1"/>
          </g>
          <path d="m236 26 27 11v28c0 19-27 38-27 38s-27-19-27-38V37Z" fill="#302820" stroke-dasharray="4 4" class="accent"/>
          <text x="236" y="78" text-anchor="middle" stroke="none" font-size="32" font-weight="700" class="accent">?</text>
        </g>
        <g *ngSwitchCase="'order'">
          <ellipse cx="140" cy="122" rx="116" ry="4" fill="#030c14" opacity=".5" stroke="none"/>
          <g *ngFor="let pos of [[30,37],[110,25],[190,13]]; let i = index" [attr.transform]="'translate(' + pos[0] + ',' + pos[1] + ')'">
            <rect x="3" y="4" width="58" height="78" rx="6" fill="#05121d" stroke="none"/>
            <rect width="58" height="78" rx="6" [attr.fill]="surfaceFill" [attr.stroke]="i === 2 ? '#ff8a32' : '#7896aa'"/>
            <path d="M6 4h46" stroke="#bbd0de" opacity=".35" stroke-width="1"/>
            <text x="10" y="19" stroke="none" font-size="12" font-weight="800" [class.accent]="i === 2" class="jersey-lettering">0{{ i + 1 }}</text>
            <path d="m29 26 12 5v10c0 9-12 17-12 17S17 50 17 41V31Z" fill="#102437" stroke="#a0bacb" stroke-width="1.1"/>
            <path d="M23 34h12m-6-3v18" stroke="#ff9d54" stroke-width="1.5"/>
            <path d="M10 65h38m-38 5h24" stroke="#91adc0" stroke-width="1.3" opacity=".5"/>
          </g>
          <path d="M94 83h11m-4-4 4 4-4 4m73-16h11m-4-4 4 4-4 4" class="accent"/>
        </g>
        <g *ngSwitchCase="'mystery'">
          <ellipse cx="140" cy="121" rx="60" ry="4" fill="#030c14" opacity=".5" stroke="none"/>
          <rect x="91" y="8" width="98" height="109" rx="9" [attr.fill]="metalFill" stroke="#8ca7ba"/>
          <rect x="95" y="12" width="90" height="101" rx="6" fill="#0b1b29" stroke="#203d51"/>
          <path d="M102 17h76M103 105h22m30 0h22" stroke="#93b1c5" opacity=".35" stroke-width="1"/>
          <path d="M113 92v-6c0-13 12-21 27-21s27 8 27 21v6Z" [attr.fill]="surfaceFill" stroke="#4e7089"/>
          <path d="m127 68 13 12 13-12m-28 8-6 16m36-16 6 16" stroke="#91b0c4" opacity=".4"/>
          <path d="M125 43c0-13 6-19 15-19s15 6 15 19v8c0 13-8 21-15 21s-15-8-15-21Z" [attr.fill]="surfaceFill" stroke="#5b7d96"/>
          <path d="M126 39q14-9 28 0" stroke="#8ba9bd" opacity=".3"/>
          <circle cx="173" cy="87" r="17" fill="#152536" stroke="#ff8a32"/>
          <path d="M168 82c0-7 11-7 11-1 0 4-6 4-6 9" class="accent" stroke-width="2.2"/><circle cx="173" cy="95" r="1.2" fill="#ff8a32" stroke="none"/>
          <path d="M75 40H63m18 35H68m137-27h12m-18 34h13" stroke="#5f819b" opacity=".6"/>
        </g>
        <g *ngSwitchCase="'five'">
          <ellipse cx="140" cy="120" rx="72" ry="4" fill="#030c14" opacity=".5" stroke="none"/>
          <path d="M111 82v12m58-12v12" stroke="#38566d" stroke-width="5"/>
          <rect x="78" y="8" width="124" height="80" rx="7" [attr.fill]="metalFill" stroke="#93adbf"/>
          <rect x="83" y="13" width="114" height="70" rx="4" fill="#03080d" stroke="#06131e"/>
          <path d="M85 15h110" stroke="#c3d5df" opacity=".2" stroke-width="1"/>
          <g *ngFor="let point of [[81,11],[199,11],[81,85],[199,85]]" [attr.transform]="'translate(' + point[0] + ',' + point[1] + ')'" stroke="#a1bacb" stroke-width=".8"><path d="M-1 0h2"/></g>
          <g fill="#ff3535" stroke="none">
            <path d="m102 25 4-4h22l4 4-4 4h-22Zm27 6 5-5 4 5v15l-4 4-5-4Zm-27 19 4-4h22l4 4-4 4h-22Zm-7 7 5-5 4 5v15l-4 4-5-4Zm7 20 4-4h22l4 4-4 4h-22Z"/>
            <path d="m151 26 5-5 4 5v20l-4 4-5-4Zm27 0 5-5 4 5v20l-4 4-5-4Zm-20 24 4-4h13l4 4-4 4h-13Zm20 7 5-5 4 5v15l-4 4-5-4Z"/>
          </g>
          <g *ngFor="let x of [92,116,140,164,188]" [attr.transform]="'translate(' + x + ',107)'">
            <circle r="8" [attr.fill]="surfaceFill" stroke="#7899b0" stroke-width="1"/>
            <circle cy="-2" r="2" fill="#bed0dc" stroke="none"/><path d="M-4 5V3q4-5 8 0v2Z" fill="#bed0dc" stroke="none"/>
          </g>
        </g>
      </g>
    </svg>`,
  styles: [`:host { display: block; width: 100%; } svg { display: block; width: 100%; height: 128px; color: #688095; } text { fill: currentColor; font-family: 'DM Sans', sans-serif; } .jersey-lettering { color: #edf3f7; } .accent { color: #ff8a32; } @media (max-width: 600px) { svg { height: 96px; } }`]
})
export class GameArtComponent {
  @Input() game = 'grid';
  get surfaceFill(): string { return 'url(#pnr-art-surface-' + this.game + ')'; }
  get metalFill(): string { return 'url(#pnr-art-metal-' + this.game + ')'; }
  readonly cells = [[88, 16], [124, 16], [160, 16], [88, 50], [124, 50], [160, 50], [88, 84], [124, 84], [160, 84]];
}
