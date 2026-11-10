import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { APP_INITIALIZER, inject } from '@angular/core';
import { CanActivateFn, provideRouter, Router, Routes, withInMemoryScrolling } from '@angular/router';
import { AppComponent } from './app/app.component';
import { GridGameComponent } from './app/grid-game.component';
import { HomeComponent } from './app/home.component';
import { TeamHistoryComponent } from './app/team-history.component';
import { CareerGamesComponent } from './app/career-games.component';
import { FivePlayerGameComponent } from './app/five-player-game.component';
import { ContactComponent } from './app/contact.component';
import { AuthPageComponent } from './app/auth-page.component';
import { AuthCallbackComponent } from './app/auth-callback.component';
import { ConfirmEmailComponent } from './app/confirm-email.component';
import { ProfileComponent } from './app/profile.component';
import { AuthService } from './app/auth.service';
import { authInterceptor } from './app/auth.interceptor';
import { GameModeComponent } from './app/game-mode.component';
import { LeaderboardComponent } from './app/leaderboard.component';
import { NotFoundComponent } from './app/not-found.component';
import { AdminUsersComponent } from './app/admin-users.component';
import { AdminAuditLogsComponent } from './app/admin-audit-logs.component';

const signedInGuard: CanActivateFn = (_route, state) => inject(AuthService).user()
  ? true
  : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

const adminGuard: CanActivateFn = () => inject(AuthService).user()?.roles.includes('Admin')
  ? true
  : inject(Router).createUrlTree(['/']);

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'grid', redirectTo: 'grid/normal', pathMatch: 'full' },
  { path: 'grid/:difficulty', component: GridGameComponent },
  { path: 'team-history', component: TeamHistoryComponent },
  { path: 'contact', component: ContactComponent },
  { path: 'login', component: AuthPageComponent, data: { mode: 'login' } },
  { path: 'register', component: AuthPageComponent, data: { mode: 'register' } },
  { path: 'forgot-password', component: AuthPageComponent, data: { mode: 'forgot' } },
  { path: 'resend-confirmation', component: AuthPageComponent, data: { mode: 'resend' } },
  { path: 'reset-password', component: AuthPageComponent, data: { mode: 'reset' } },
  { path: 'set-password', component: AuthPageComponent, data: { mode: 'setup' } },
  { path: 'change-temporary-password', component: AuthPageComponent, data: { mode: 'temporary' } },
  { path: 'confirm-email', component: ConfirmEmailComponent },
  { path: 'auth/callback', component: AuthCallbackComponent },
  { path: 'profile', component: ProfileComponent },
  { path: 'leaderboard', component: LeaderboardComponent },
  { path: 'admin/users', component: AdminUsersComponent, canActivate: [adminGuard] },
  { path: 'admin/audit-logs', component: AdminAuditLogsComponent, canActivate: [adminGuard] },
  { path: 'play/:game', component: GameModeComponent, canActivate: [signedInGuard] },
  { path: 'game/five-player', component: FivePlayerGameComponent },
  { path: 'game/history', component: TeamHistoryComponent },
  { path: 'game/:game', component: CareerGamesComponent },
  { path: '**', component: NotFoundComponent }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes, withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' })),
    { provide: APP_INITIALIZER, multi: true, deps: [AuthService], useFactory: (auth: AuthService) => () => auth.restoreSession() }
  ]
}).catch((error: unknown) => console.error(error));
