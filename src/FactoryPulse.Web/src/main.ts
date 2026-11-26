import { Component } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';

@Component({ selector: 'fp-root', template: '<main><h1>FactoryPulse AI</h1></main>' })
export class App {}

bootstrapApplication(App).catch((error: unknown) => console.error(error));
