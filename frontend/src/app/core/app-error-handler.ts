import { ErrorHandler, Injectable, inject } from '@angular/core';
import { AppErrorService } from './app-error.service';
import { extractErrorMessage } from './error-message.utils';

@Injectable()
export class AppErrorHandler implements ErrorHandler {
  private readonly appErrorService = inject(AppErrorService);

  handleError(error: unknown): void {
    const message = extractErrorMessage(error);
    this.appErrorService.setError(message);
    console.error('Unhandled client-side error.', error);
  }
}
