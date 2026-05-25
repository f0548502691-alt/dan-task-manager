import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, finalize, map, Observable, of, switchMap, tap, throwError } from 'rxjs';
import {
  ApiErrorResponse,
  BaseTaskDto,
  ChangeStatusWorkflowRequest,
  ChangeStatusWorkflowResponse,
  CloseTaskRequest,
  CloseTaskResponse,
  CLOSED_TASK_STATUS,
  CreateTaskRequest,
  UpdateTaskRequest
} from './task.interfaces';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/tasks';

  private readonly _currentUserId = signal<number | null>(null);
  private readonly _tasks = signal<readonly BaseTaskDto[]>([]);
  private readonly _isLoading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly currentUserId = this._currentUserId.asReadonly();
  readonly tasks = this._tasks.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly taskCount = computed(() => this._tasks().length);
  readonly hasTasks = computed(() => this.taskCount() > 0);

  setCurrentUserId(userId: number | null): void {
    this._currentUserId.set(userId);
    this._error.set(null);

    if (userId === null) {
      this._tasks.set([]);
      return;
    }

    void this.refreshCurrentUserTasks().subscribe({
      error: () => {
        // Error details are already stored in the `error` signal.
      }
    });
  }

  refreshCurrentUserTasks(): Observable<readonly BaseTaskDto[]> {
    const userId = this._currentUserId();
    if (userId === null) {
      this._tasks.set([]);
      return of([]);
    }

    this._isLoading.set(true);
    this._error.set(null);

    return this.http.get<BaseTaskDto[]>(`${this.apiUrl}/user/${userId}`).pipe(
      map((tasks) => this.sortTasks(tasks)),
      tap((tasks) => this._tasks.set(tasks)),
      catchError((error) => this.handleHttpError(error)),
      finalize(() => this._isLoading.set(false))
    );
  }

  createTask(request: CreateTaskRequest): Observable<BaseTaskDto> {
    this._error.set(null);

    return this.http.post<BaseTaskDto>(this.apiUrl, request).pipe(
      tap((task) => this.syncTaskWithState(task)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  changeTaskStatus(taskId: number, request: ChangeStatusWorkflowRequest): Observable<ChangeStatusWorkflowResponse> {
    this._error.set(null);

    return this.http
      .post<ChangeStatusWorkflowResponse>(`${this.apiUrl}/${taskId}/change-status`, request)
      .pipe(
        tap((response) => this.syncTaskWithState(response.task)),
        catchError((error) => this.handleHttpError(error))
      );
  }

  closeTask(taskId: number, request: CloseTaskRequest): Observable<CloseTaskResponse> {
    this._error.set(null);

    return this.http.post<CloseTaskResponse>(`${this.apiUrl}/${taskId}/close`, request).pipe(
      tap((response) => this.syncTaskWithState(response.task)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  updateTask(taskId: number, request: UpdateTaskRequest): Observable<void> {
    this._error.set(null);

    return this.http.put<void>(`${this.apiUrl}/${taskId}`, request).pipe(
      switchMap(() => {
        const currentUserId = this._currentUserId();
        if (currentUserId === null) {
          return of(void 0);
        }

        return this.refreshCurrentUserTasks().pipe(map(() => void 0));
      }),
      catchError((error) => this.handleHttpError(error))
    );
  }

  deleteTask(taskId: number): Observable<void> {
    this._error.set(null);

    return this.http.delete<void>(`${this.apiUrl}/${taskId}`).pipe(
      tap(() => this.removeTaskFromState(taskId)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  clearError(): void {
    this._error.set(null);
  }

  private syncTaskWithState(task: BaseTaskDto): void {
    const currentUserId = this._currentUserId();
    if (currentUserId === null || task.assignedToUserId !== currentUserId || task.currentStatus === CLOSED_TASK_STATUS) {
      this.removeTaskFromState(task.id);
      return;
    }

    this._tasks.update((tasks) => {
      const index = tasks.findIndex((currentTask) => currentTask.id === task.id);
      if (index === -1) {
        return this.sortTasks([...tasks, task]);
      }

      const updatedTasks = [...tasks];
      updatedTasks[index] = task;
      return this.sortTasks(updatedTasks);
    });
  }

  private removeTaskFromState(taskId: number): void {
    this._tasks.update((tasks) => tasks.filter((task) => task.id !== taskId));
  }

  private sortTasks(tasks: readonly BaseTaskDto[]): readonly BaseTaskDto[] {
    return [...tasks].sort((left, right) => Date.parse(right.createdAt) - Date.parse(left.createdAt));
  }

  private handleHttpError(error: unknown): Observable<never> {
    const message = this.extractErrorMessage(error);
    this._error.set(message);
    return throwError(() => new Error(message));
  }

  private extractErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Unexpected error while communicating with the server.';
    }

    const payload = error.error;
    if (this.isApiErrorResponse(payload)) {
      return payload.error;
    }

    if (typeof payload === 'string' && payload.trim().length > 0) {
      return payload;
    }

    if (typeof error.message === 'string' && error.message.trim().length > 0) {
      return error.message;
    }

    return 'Unexpected server error.';
  }

  private isApiErrorResponse(payload: unknown): payload is ApiErrorResponse {
    return (
      payload !== null &&
      typeof payload === 'object' &&
      'error' in payload &&
      typeof (payload as { error: unknown }).error === 'string'
    );
  }
}
