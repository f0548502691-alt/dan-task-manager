import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, finalize, map, Observable, of, switchMap, tap, throwError } from 'rxjs';
import {
  BaseTaskDto,
  ChangeStatusWorkflowRequest,
  ChangeStatusWorkflowResponse,
  CloseTaskRequest,
  CloseTaskResponse,
  TaskTypeSchemaDto,
  PagedResultDto,
  TASK_STATUS,
  TaskCustomData,
  CreateTaskRequest,
  UpdateTaskRequest
} from './task.interfaces';
import { AppErrorService } from '../core/app-error.service';
import { extractErrorMessage } from '../core/error-message.utils';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);
  private readonly appErrorService = inject(AppErrorService);
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
    this.clearErrorsState();

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
    this.clearErrorsState();

    return this.http.get<PagedResultDto<unknown> | unknown[]>(`${this.apiUrl}/user/${userId}`).pipe(
      map((response) => this.normalizeTaskCollection(response)),
      map((tasks) => this.sortTasks(tasks)),
      tap((tasks) => this._tasks.set(tasks)),
      catchError((error) => this.handleHttpError(error)),
      finalize(() => this._isLoading.set(false))
    );
  }

  getTask(taskId: number): Observable<BaseTaskDto> {
    this._error.set(null);
    return this.http.get<BaseTaskDto>(`${this.apiUrl}/${taskId}`).pipe(
      catchError((error) => this.handleHttpError(error))
    );
  }

  createTask(request: CreateTaskRequest): Observable<BaseTaskDto> {
    this.clearErrorsState();

    return this.http.post<unknown>(this.apiUrl, request).pipe(
      map((response) => this.normalizeTask(response)),
      tap((task) => this.syncTaskWithState(task)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  changeTaskStatus(taskId: number, request: ChangeStatusWorkflowRequest): Observable<ChangeStatusWorkflowResponse> {
    this.clearErrorsState();

    return this.http.post<unknown>(`${this.apiUrl}/${taskId}/change-status`, request).pipe(
      map((response) => this.normalizeChangeStatusResponse(response)),
      tap((response) => this.syncTaskWithState(response.task)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  closeTask(taskId: number, request: CloseTaskRequest): Observable<CloseTaskResponse> {
    this.clearErrorsState();

    return this.http.post<unknown>(`${this.apiUrl}/${taskId}/close`, request).pipe(
      map((response) => this.normalizeCloseResponse(response)),
      tap((response) => this.syncTaskWithState(response.task)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  getTaskTypes(): Observable<readonly TaskTypeSchemaDto[]> {
    this.clearErrorsState();

    return this.http.get<TaskTypeSchemaDto[]>('/api/task-types').pipe(
      map((taskTypes) =>
        taskTypes
          .filter(
            (taskType) =>
              taskType.isActive &&
              typeof taskType.taskType === 'string' &&
              taskType.taskType.trim().length > 0
          )
          .sort((left, right) => left.taskType.localeCompare(right.taskType))
      ),
      catchError((error) => this.handleHttpError(error))
    );
  }

  updateTask(taskId: number, request: UpdateTaskRequest): Observable<void> {
    this.clearErrorsState();

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
    this.clearErrorsState();

    return this.http.delete<void>(`${this.apiUrl}/${taskId}`).pipe(
      tap(() => this.removeTaskFromState(taskId)),
      catchError((error) => this.handleHttpError(error))
    );
  }

  clearError(): void {
    this.clearErrorsState();
  }

  private syncTaskWithState(task: BaseTaskDto): void {
    const currentUserId = this._currentUserId();
    if (currentUserId === null || task.assignedToUserId !== currentUserId) {
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

  private normalizeTaskCollection(payload: PagedResultDto<unknown> | unknown[]): BaseTaskDto[] {
    if (Array.isArray(payload)) {
      return payload.map((task) => this.normalizeTask(task));
    }

    if (this.isPagedResult(payload) && Array.isArray(payload.items)) {
      return payload.items.map((task) => this.normalizeTask(task));
    }

    return [];
  }

  private normalizeChangeStatusResponse(payload: unknown): ChangeStatusWorkflowResponse {
    const response = this.asRecord(payload);
    const task = this.normalizeTask(response['task']);
    const newStatus = this.toNumber(response['newStatus'], task.currentStatus);

    return {
      success: this.toBoolean(response['success']),
      message: this.toStringValue(response['message'], ''),
      newStatus,
      task
    };
  }

  private normalizeCloseResponse(payload: unknown): CloseTaskResponse {
    const response = this.asRecord(payload);

    return {
      success: this.toBoolean(response['success']),
      message: this.toStringValue(response['message'], ''),
      task: this.normalizeTask(response['task'])
    };
  }

  private normalizeTask(payload: unknown): BaseTaskDto {
    const task = this.asRecord(payload);
    const assignedToUserPayload = task['assignedToUser'];
    const assignedToUser =
      assignedToUserPayload && typeof assignedToUserPayload === 'object' && !Array.isArray(assignedToUserPayload)
        ? {
            id: this.toNumber((assignedToUserPayload as Record<string, unknown>)['id']),
            name: this.toStringValue((assignedToUserPayload as Record<string, unknown>)['name'], ''),
            email: this.toStringValue((assignedToUserPayload as Record<string, unknown>)['email'], '')
          }
        : null;

    return {
      id: this.toNumber(task['id']),
      taskType: this.toStringValue(task['taskType'], ''),
      currentStatus: this.toNumber(task['currentStatus'], TASK_STATUS.CREATED),
      assignedToUserId: this.toNumber(task['assignedToUserId']),
      assignedToUser,
      description: this.toStringValue(task['description'], ''),
      customFields: this.extractCustomFields(task),
      createdAt: this.toStringValue(task['createdAt'], new Date(0).toISOString()),
      updatedAt: this.toStringValue(task['updatedAt'], new Date(0).toISOString())
    };
  }

  private extractCustomFields(task: Record<string, unknown>): TaskCustomData {
    const customFields = task['customFields'];
    if (typeof customFields === 'object' && customFields !== null && !Array.isArray(customFields)) {
      return customFields as TaskCustomData;
    }

    const customDataJson = task['customDataJson'];
    if (typeof customDataJson === 'string') {
      try {
        const parsed: unknown = JSON.parse(customDataJson);
        if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
          return parsed as TaskCustomData;
        }
      } catch {
        // keep fallback
      }
    }

    return {};
  }

  private isPagedResult(value: unknown): value is PagedResultDto<unknown> {
    return value !== null && typeof value === 'object' && 'items' in value;
  }

  private asRecord(value: unknown): Record<string, unknown> {
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      return value as Record<string, unknown>;
    }

    throw new Error('Unexpected task response payload.');
  }

  private toNumber(value: unknown, fallback = 0): number {
    return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
  }

  private toStringValue(value: unknown, fallback: string): string {
    return typeof value === 'string' ? value : fallback;
  }

  private toBoolean(value: unknown): boolean {
    return value === true;
  }

  private handleHttpError(error: unknown): Observable<never> {
    const message = extractErrorMessage(error);
    this._error.set(message);
    this.appErrorService.setError(message);
    return throwError(() => new Error(message));
  }

  private clearErrorsState(): void {
    this._error.set(null);
    this.appErrorService.clearError();
  }
}
