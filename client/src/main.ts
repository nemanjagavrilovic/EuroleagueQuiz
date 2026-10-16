import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './app/auth/auth.interceptor';
import { provideRouter, Routes } from '@angular/router';
import { AppComponent } from './app/app/app.component';
import { GridGameComponent } from './app/grid-game/grid-game.component';
import { HomeComponent } from './app/home/home.component';
import { TeamHistoryComponent } from './app/team-history/team-history.component';
import { CareerGamesComponent } from './app/career-games/career-games.component';
import { FivePlayerGameComponent } from './app/five-player-game/five-player-game.component';
import { AuthComponent } from './app/auth/auth.component';

const routes: Routes = [
  { path: 'login', component: AuthComponent },
  { path: 'register', component: AuthComponent },
  { path: '', component: HomeComponent },
  { path: 'grid/:difficulty', component: GridGameComponent },
  { path: 'team-history', component: TeamHistoryComponent },
  { path: 'game/five-player', component: FivePlayerGameComponent },
  { path: 'game/:game', component: CareerGamesComponent },
  { path: '**', redirectTo: '' }
];

bootstrapApplication(AppComponent, {
  providers: [provideHttpClient(withInterceptors([authInterceptor])), provideRouter(routes)]
}).catch((error: unknown) => console.error(error));
