import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { AppComponent } from './app/app.component';
import { GridGameComponent } from './app/grid-game.component';
import { HomeComponent } from './app/home.component';
import { TeamHistoryComponent } from './app/team-history.component';
import { CareerGamesComponent } from './app/career-games.component';

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'grid/:difficulty', component: GridGameComponent },
  { path: 'team-history', component: TeamHistoryComponent },
  { path: 'game/:game', component: CareerGamesComponent },
  { path: '**', redirectTo: '' }
];

bootstrapApplication(AppComponent, {
  providers: [provideHttpClient(), provideRouter(routes)]
}).catch((error: unknown) => console.error(error));
