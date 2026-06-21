import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, RouteReuseStrategy, withComponentInputBinding } from '@angular/router';

import { authInterceptor } from './core/auth.interceptor';
import { leagueInterceptor } from './core/league.interceptor';
import { LeagueReuseStrategy } from './core/league-reuse.strategy';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([leagueInterceptor, authInterceptor])),
    { provide: RouteReuseStrategy, useClass: LeagueReuseStrategy },
  ],
};
