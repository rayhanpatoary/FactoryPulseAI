import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { Component } from '@angular/core';
import de from '@angular/common/locales/de';
import en from '@angular/common/locales/en-GB';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, RouterOutlet, Routes, withInMemoryScrolling } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { expireOnUnauthorized, requireSession } from './app/auth/auth-session';

registerLocaleData(de);
registerLocaleData(en);

const dashboard = () => import('./app/dashboard/dashboard').then((module) => module.Dashboard);
const monitoring: Routes = [
  { path: '', pathMatch: 'full', loadComponent: dashboard },
  {
    path: 'machines/:id',
    loadComponent: () => import('./app/machines/machine-detail').then((module) => module.MachineDetail),
  },
  { path: 'machines', loadComponent: dashboard, data: { catalogOnly: true } },
  { path: 'alarms', loadComponent: () => import('./app/alarms/alarms').then((module) => module.Alarms) },
  {
    path: 'history',
    loadComponent: () => import('./app/history/history').then((module) => module.History),
  },
  { path: 'settings', loadComponent: () => import('./app/settings').then((module) => module.Settings) },
];

const routes: Routes = [
  { path: 'login', loadComponent: () => import('./app/auth/login').then((module) => module.Login) },
  {
    path: '',
    canActivate: [requireSession],
    loadComponent: () => import('./app/app').then((module) => module.App),
    children: monitoring,
  },
  { path: '**', redirectTo: '' },
];

@Component({ selector: 'fp-root', imports: [RouterOutlet], template: '<router-outlet />' })
class Root {}

bootstrapApplication(Root, {
  providers: [
    provideHttpClient(withInterceptors([expireOnUnauthorized])),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideTranslateService({ fallbackLang: 'en' }),
  ],
}).catch((error: unknown) => console.error(error));
