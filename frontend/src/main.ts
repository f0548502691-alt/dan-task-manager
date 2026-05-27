import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ErrorHandler, provideZonelessChangeDetection } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { AppErrorHandler } from './app/core/app-error-handler';
import { httpErrorInterceptor } from './app/core/http-error.interceptor';

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([httpErrorInterceptor])),
    { provide: ErrorHandler, useClass: AppErrorHandler },
    provideZonelessChangeDetection()
  ]
}).catch((error: unknown) => {
  console.error('Failed to bootstrap Angular app.', error);
});
