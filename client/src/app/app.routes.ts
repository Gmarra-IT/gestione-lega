import { Routes } from '@angular/router';
import { adminGuard } from './core/admin.guard';

export const routes: Routes = [
  // Pagina-base: elenco filtrabile delle leghe.
  {
    path: '',
    pathMatch: 'full',
    title: 'Leghe',
    loadComponent: () => import('./features/leagues/league-picker.component').then(m => m.LeaguePickerComponent),
  },
  // Console super-admin (slug riservato, vedi RESERVED_SLUGS).
  {
    path: 'gestione',
    title: 'Gestione leghe',
    loadComponent: () => import('./features/admin/super-admin.component').then(m => m.SuperAdminComponent),
  },
  // Shell di lega: tutto ciò che sta sotto /:slug vede solo i dati della lega.
  {
    path: ':slug',
    loadComponent: () => import('./features/league-shell/league-shell.component').then(m => m.LeagueShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'classifica' },
      {
        path: 'classifica',
        title: 'Classifica',
        loadComponent: () => import('./features/classifica/classifica.component').then(m => m.ClassificaComponent),
      },
      {
        path: 'tappe',
        title: 'Tappe',
        loadComponent: () => import('./features/tappe/tappe.component').then(m => m.TappeComponent),
      },
      {
        path: 'inserimento',
        title: 'Inserimento',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/inserimento/inserimento.component').then(m => m.InserimentoComponent),
      },
      {
        path: 'importazione',
        title: 'Import PDF',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/importazione/importazione.component').then(m => m.ImportazioneComponent),
      },
      {
        path: 'impostazioni',
        title: 'Impostazioni',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/impostazioni/impostazioni.component').then(m => m.ImpostazioniComponent),
      },
      {
        path: 'login',
        title: 'Login',
        loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent),
      },
      { path: '**', redirectTo: 'classifica' },
    ],
  },
  // Slug sconosciuto / path errato → torna alla pagina-base.
  { path: '**', redirectTo: '' },
];
