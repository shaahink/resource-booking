import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** A 401 from the API means the session is gone (expired, or the user was disabled):
 *  drop it and go to the login page. Auth endpoints answer 401 as part of their own flow. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url.startsWith('/api/') &&
        !request.url.startsWith('/api/auth/')
      ) {
        auth.endSession();
      }
      return throwError(() => error);
    }),
  );
};
