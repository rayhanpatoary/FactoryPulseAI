import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import de from '@angular/common/locales/de';
import en from '@angular/common/locales/en-GB';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, Routes, withInMemoryScrolling } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { App } from './app/app';

registerLocaleData(de);
registerLocaleData(en);

const dashboard = () => import('./app/dashboard/dashboard').then((module) => module.Dashboard);
const routes: Routes = [
  { path: '', pathMatch: 'full', loadComponent: dashboard },
  {
    path: 'machines/:id',
    loadComponent: () => import('./app/machines/machine-detail').then((module) => module.MachineDetail),
  },
  { path: 'machines', loadComponent: dashboard, data: { catalogOnly: true } },
  { path: 'settings', loadComponent: () => import('./app/settings').then((module) => module.Settings) },
  { path: '**', redirectTo: '' },
];

bootstrapApplication(App, {
  providers: [
    provideHttpClient(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideTranslateService({ fallbackLang: 'en' }),
  ],
}).catch((error: unknown) => console.error(error));
