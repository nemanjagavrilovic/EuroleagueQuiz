import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes, withInMemoryScrolling } from '@angular/router';
import { AppComponent } from './app/app.component';
import { GridGameComponent } from './app/grid-game.component';
import { HomeComponent } from './app/home.component';
import { TeamHistoryComponent } from './app/team-history.component';
import { CareerGamesComponent } from './app/career-games.component';
import { FivePlayerGameComponent } from './app/five-player-game.component';

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'grid', redirectTo: 'grid/normal', pathMatch: 'full' },
  { path: 'grid/:difficulty', component: GridGameComponent },
  { path: 'team-history', component: TeamHistoryComponent },
  { path: 'game/five-player', component: FivePlayerGameComponent },
  { path: 'game/:game', component: CareerGamesComponent },
  { path: '**', redirectTo: '' }
];

bootstrapApplication(AppComponent, {
  providers: [provideHttpClient(), provideRouter(routes, withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' }))]
}).catch((error: unknown) => console.error(error));
