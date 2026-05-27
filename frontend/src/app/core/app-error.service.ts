import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AppErrorService {
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  setError(message: string): void {
    this._error.set(message);
  }

  clearError(): void {
    this._error.set(null);
  }
}
