import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="not-found-shell">
      <section class="not-found-card">
        <div class="court-art" aria-hidden="true">
          <span class="score-code">404</span>
          <span class="court-circle"></span>
          <span class="court-line"></span>
          <span class="basketball"></span>
        </div>
        <div class="not-found-copy">
          <p class="eyebrow">404 · OUT OF BOUNDS</p>
          <h1>This page missed the rim.</h1>
          <p>The page may have moved, the link may be outdated, or the play simply isn’t available.</p>
          <div class="not-found-actions">
            <a class="primary" routerLink="/">Back to all games</a>
            <a class="secondary" routerLink="/contact">Contact us</a>
          </div>
        </div>
      </section>
    </main>
  `,
  styles: [`
    :host { display: block; }
    .not-found-shell { width: min(1120px, calc(100% - 32px)); min-height: min(680px, calc(100dvh - 180px)); margin: 34px auto; display: grid; place-items: center; }
    .not-found-card { width: 100%; min-height: 500px; display: grid; grid-template-columns: minmax(320px, .9fr) minmax(420px, 1.1fr); overflow: hidden; border: 1px solid var(--border); border-radius: 20px; background: var(--surface); box-shadow: 0 28px 70px #0006; }
    .court-art { position: relative; min-height: 500px; overflow: hidden; background: radial-gradient(circle at 66% 38%, #ff8a3233, transparent 26%), linear-gradient(145deg, #142c3e, #07131f 72%); border-right: 1px solid var(--border); }
    .score-code { position: absolute; z-index: 2; left: 50%; top: 50%; transform: translate(-50%, -50%); color: #f2f6fa; font-size: clamp(94px, 13vw, 164px); font-weight: 950; letter-spacing: -.09em; text-shadow: 0 10px 40px #0009; }
    .score-code::first-letter { color: var(--accent); }
    .court-circle { position: absolute; width: 390px; height: 390px; left: -185px; top: 55px; border: 2px solid #ffffff13; border-radius: 50%; }
    .court-line { position: absolute; inset: 50% 0 auto; height: 2px; background: #ffffff12; }
    .basketball { position: absolute; z-index: 3; width: 74px; height: 74px; right: 36px; bottom: 34px; border: 3px solid #1b0c05; border-radius: 50%; background: radial-gradient(circle at 32% 27%, #ffad63, #f97316 50%, #9f3413); box-shadow: 0 16px 28px #0007; transform: rotate(-18deg); }
    .basketball::before, .basketball::after { content: ''; position: absolute; inset: 33px -2px auto; height: 3px; border-radius: 50%; background: #281108; }
    .basketball::after { inset: -2px 33px; width: 3px; height: auto; }
    .not-found-copy { align-self: center; padding: clamp(38px, 7vw, 82px); }
    .eyebrow { margin: 0 0 12px; color: var(--accent); font-size: 12px; font-weight: 900; letter-spacing: .16em; }
    h1 { max-width: 520px; margin: 0; color: var(--text); font-size: clamp(38px, 6vw, 62px); line-height: .98; letter-spacing: -.055em; }
    .not-found-copy > p:not(.eyebrow) { max-width: 520px; margin: 22px 0 0; color: var(--text-secondary); font-size: 16px; line-height: 1.7; }
    .not-found-actions { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 30px; }
    .not-found-actions a { display: inline-flex; align-items: center; justify-content: center; min-height: 48px; padding: 0 20px; border-radius: 9px; text-decoration: none; font-size: 13px; font-weight: 850; }
    .primary { border: 1px solid var(--accent); background: var(--accent); color: var(--on-accent); }
    .primary:hover { background: #ff9c52; }
    .secondary { border: 1px solid var(--border-strong); color: var(--text); background: var(--surface-raised); }
    .secondary:hover { border-color: var(--accent); color: var(--accent-soft); }
    .not-found-actions a:focus-visible { outline: 2px solid var(--accent); outline-offset: 3px; }
    @media (max-width: 760px) {
      .not-found-shell { min-height: auto; margin: 20px auto; }
      .not-found-card { min-height: 0; grid-template-columns: 1fr; }
      .court-art { min-height: 260px; border-right: 0; border-bottom: 1px solid var(--border); }
      .score-code { font-size: clamp(92px, 31vw, 142px); }
      .court-circle { width: 280px; height: 280px; left: -130px; top: -8px; }
      .basketball { width: 58px; height: 58px; right: 22px; bottom: 20px; }
      .basketball::before { inset: 26px -2px auto; }
      .basketball::after { inset: -2px 26px; }
      .not-found-copy { padding: 32px 24px 36px; }
      .not-found-copy > p:not(.eyebrow) { font-size: 15px; }
      .not-found-actions a { flex: 1 1 150px; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotFoundComponent {}
