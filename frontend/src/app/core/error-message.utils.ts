import { HttpErrorResponse } from '@angular/common/http';
import { ApiErrorResponse } from '../tasks/task.interfaces';

const FALLBACK_HTTP_ERROR_MESSAGE = 'Unexpected server error.';
const FALLBACK_CLIENT_ERROR_MESSAGE = 'Unexpected error while communicating with the server.';

export function extractErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    return extractHttpErrorMessage(error);
  }

  if (error instanceof Error && error.message.trim().length > 0) {
    return error.message;
  }

  return FALLBACK_CLIENT_ERROR_MESSAGE;
}

function extractHttpErrorMessage(error: HttpErrorResponse): string {
  const payload = error.error;
  if (isApiErrorResponse(payload)) {
    return payload.error;
  }

  if (typeof payload === 'string' && payload.trim().length > 0) {
    return payload;
  }

  if (typeof error.message === 'string' && error.message.trim().length > 0) {
    return error.message;
  }

  return FALLBACK_HTTP_ERROR_MESSAGE;
}

function isApiErrorResponse(payload: unknown): payload is ApiErrorResponse {
  return (
    payload !== null &&
    typeof payload === 'object' &&
    'error' in payload &&
    typeof (payload as { error: unknown }).error === 'string'
  );
}
