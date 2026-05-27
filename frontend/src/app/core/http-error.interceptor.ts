import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AppErrorService } from './app-error.service';
import { extractErrorMessage } from './error-message.utils';

export const httpErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const appErrorService = inject(AppErrorService);

  return next(request).pipe(
    catchError((error: unknown) => {
      const message = extractErrorMessage(error);
      appErrorService.setError(message);
      return throwError(() => new Error(message));
    })
  );
};
