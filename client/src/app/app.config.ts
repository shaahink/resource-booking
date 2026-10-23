import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { ApiClient } from './core/api-client';
import { ApiService } from './core/api.service';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';
import { USE_MOCKS } from './core/config';
import { MockApiService } from './core/mock-api.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
    { provide: ApiClient, useClass: USE_MOCKS ? MockApiService : ApiService },
    provideAppInitializer(() => inject(AuthService).init()),
  ],
};
