import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  BaseTaskDto,
  ChangeStatusWorkflowRequest,
  DEFAULT_STATUS_LABELS,
  TASK_STATUS,
  TaskCustomData,
  TaskTypeSchemaDto
} from './task.interfaces';
import { TaskDynamicFieldsComponent } from './task-dynamic-fields.component';
import { TaskService } from './task.service';
import { parseTaskCustomDataJson, resetControl } from './task-form.utils';
import {
  buildTaskFieldPayload,
  FALLBACK_TASK_TYPE_SCHEMAS,
  getTaskFieldViewModels,
  hydrateTaskFieldControls,
  resetTaskFieldControls
} from './task-workflow-schema';

interface StatusOption {
  value: number;
  label: string;
}

interface TaskTypeOption {
  value: string;
  label: string;
}

const DEFAULT_CURRENT_USER_ID = 1;

@Component({
  selector: 'app-task-workflow-board',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TaskDynamicFieldsComponent],
  templateUrl: './task-workflow-board.component.html',
  styleUrls: ['./task-workflow-board.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TaskWorkflowBoardComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly TASK_STATUS = TASK_STATUS;
  readonly taskService = inject(TaskService);
  readonly currentUserId = DEFAULT_CURRENT_USER_ID;
  readonly taskTypeSchemas = signal<readonly TaskTypeSchemaDto[]>([]);
  readonly selectedTask = signal<BaseTaskDto | null>(null);
  readonly createInFlight = signal(false);
  readonly submitInFlight = signal(false);
  readonly closeInFlight = signal(false);
  readonly taskTypeMetadataInFlight = signal(false);
  readonly successMessage = signal<string | null>(null);

  readonly taskTypeOptions = computed<readonly TaskTypeOption[]>(() =>
    this.taskTypeSchemas().map((schema) => ({
      value: schema.taskType,
      label: schema.displayName || schema.taskType
    }))
  );

  readonly selectedTaskSchema = computed(() => {
    const task = this.selectedTask();
    return task ? this.getTaskTypeSchema(task.taskType) : null;
  });

  readonly createForm = this.fb.group({
    createTaskType: this.fb.nonNullable.control<string>('', [Validators.required]),
    createTaskDescription: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(5)]),
    createAssignedToUserId: this.fb.nonNullable.control(DEFAULT_CURRENT_USER_ID, [Validators.required, Validators.min(1)])
  });

  readonly statusForm = this.fb.group({
    newStatus: this.fb.control<number | null>(null, [Validators.required]),
    nextAssignedToUserId: this.fb.nonNullable.control(DEFAULT_CURRENT_USER_ID, [Validators.required, Validators.min(1)]),
    fallbackJson: this.fb.nonNullable.control('')
  });

  readonly closeForm = this.fb.group({
    closeNotes: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  readonly statusOptions = computed<StatusOption[]>(() => {
    const task = this.selectedTask();
    if (!task) {
      return [];
    }
    if (this.isTaskClosed(task)) {
      return [{ value: TASK_STATUS.CLOSED, label: this.getStatusLabel(TASK_STATUS.CLOSED) }];
    }

    const finalStatus = this.getFinalStatus(task.taskType, task.currentStatus);
    const maxStatus = Math.max(finalStatus, task.currentStatus);
    const options: StatusOption[] = [];

    for (let status = TASK_STATUS.CREATED; status <= maxStatus; status += 1) {
      options.push({ value: status, label: this.getStatusLabel(status) });
    }

    return options;
  });

  get selectedNextStatus(): number {
    return Number(this.statusForm.controls['newStatus'].value ?? TASK_STATUS.CREATED);
  }

  ngOnInit(): void {
    this.taskService.setCurrentUserId(this.currentUserId);
    this.loadTaskTypeMetadata();
  }

  submitCreateTask(): void {
    const typeControl = this.createForm.controls['createTaskType'];
    const descriptionControl = this.createForm.controls['createTaskDescription'];
    const assignedToControl = this.createForm.controls['createAssignedToUserId'];
    if (typeControl.invalid || descriptionControl.invalid || assignedToControl.invalid) {
      typeControl.markAsTouched();
      descriptionControl.markAsTouched();
      assignedToControl.markAsTouched();
      return;
    }

    const taskType = typeControl.value?.trim();
    const description = descriptionControl.value?.trim();
    const assignedToUserId = Number(assignedToControl.value);

    if (!taskType || !description || assignedToUserId < 1) {
      return;
    }

    this.createInFlight.set(true);
    this.successMessage.set(null);
    this.taskService.clearError();

    this.taskService
      .createTask({
        taskType,
        description,
        assignedToUserId
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.createInFlight.set(false))
      )
      .subscribe({
        next: (task) => {
          this.successMessage.set(`Task #${task.id} created successfully.`);
          resetControl(this.createForm.controls['createTaskDescription']);
          this.selectTask(task);
        },
        error: () => {
          // Errors are propagated to taskService.error.
        }
      });
  }

  selectTask(task: BaseTaskDto): void {
    this.selectedTask.set(task);
    this.taskService.clearError();
    this.successMessage.set(null);
    resetControl(this.closeForm.controls['closeNotes']);

    this.resetStatusFields();
    this.hydrateStatusFields(task);

    this.statusForm.controls['nextAssignedToUserId'].setValue(task.assignedToUserId);
    this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(task));
    this.loadTaskDetails(task.id);
  }

  submitStatusUpdate(): void {
    const task = this.selectedTask();
    if (!task || this.isTaskClosed(task)) {
      return;
    }

    if (this.statusForm.invalid) {
      this.statusForm.markAllAsTouched();
      return;
    }

    const payload = this.buildPayload(task.taskType, this.selectedNextStatus);
    if (this.statusForm.controls['fallbackJson'].hasError('invalidJson')) {
      return;
    }

    const nextAssignedToUserId = Number(this.statusForm.controls['nextAssignedToUserId'].value);
    if (nextAssignedToUserId < 1) {
      this.statusForm.controls['nextAssignedToUserId'].markAsTouched();
      return;
    }

    const request: ChangeStatusWorkflowRequest = {
      newStatus: this.selectedNextStatus,
      nextAssignedToUserId,
      customFields: payload
    };

    this.submitInFlight.set(true);
    this.successMessage.set(null);
    this.taskService.clearError();

    this.taskService
      .changeTaskStatus(task.id, request)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.submitInFlight.set(false))
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.selectedTask.set(response.task);
          this.resetStatusFields();
          this.hydrateStatusFields(response.task);
          this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(response.task));
        },
        error: () => {
          // Errors are propagated to taskService.error.
        }
      });
  }

  submitCloseTask(): void {
    const task = this.selectedTask();
    if (!task || this.isTaskClosed(task)) {
      return;
    }

    const closeNotesControl = this.closeForm.controls['closeNotes'];
    if (closeNotesControl.invalid) {
      closeNotesControl.markAsTouched();
      return;
    }

    const finalNotes = closeNotesControl.value.trim();
    if (!finalNotes) {
      closeNotesControl.markAsTouched();
      return;
    }

    this.closeInFlight.set(true);
    this.successMessage.set(null);
    this.taskService.clearError();

    this.taskService
      .closeTask(task.id, {
        nextAssignedToUserId: task.assignedToUserId,
        finalNotes
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.closeInFlight.set(false))
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.selectedTask.set(response.task);
          resetControl(closeNotesControl);
        },
        error: () => {
          // Errors are propagated to taskService.error.
        }
      });
  }

  isCreateDescriptionInvalid(): boolean {
    return this.isControlInvalid(this.createForm.controls['createTaskDescription']);
  }

  isCloseNotesInvalid(): boolean {
    return this.isControlInvalid(this.closeForm.controls['closeNotes']);
  }

  isTaskClosed(task: BaseTaskDto): boolean {
    return task.currentStatus === TASK_STATUS.CLOSED;
  }

  canCloseTask(task: BaseTaskDto): boolean {
    const finalStatus = this.getFinalStatus(task.taskType, task.currentStatus);
    return typeof finalStatus === 'number' && task.currentStatus === finalStatus;
  }

  canUpdateSelectedTask(): boolean {
    const task = this.selectedTask();
    return !!task && !this.isTaskClosed(task);
  }

  hasSchemaFieldsForSelectedStatus(): boolean {
    return getTaskFieldViewModels(this.selectedTaskSchema(), this.selectedNextStatus).length > 0;
  }

  taskStatusLabel(status: number): string {
    return this.getStatusLabel(status);
  }

  trackByTaskId(_: number, task: BaseTaskDto): number {
    return task.id;
  }

  private isControlInvalid(control: { invalid: boolean; touched: boolean; dirty: boolean }): boolean {
    return control.invalid && (control.touched || control.dirty);
  }

  private getSuggestedStatus(task: BaseTaskDto): number {
    const finalStatus = this.getFinalStatus(task.taskType, task.currentStatus);
    return Math.min(task.currentStatus + 1, finalStatus);
  }

  private getStatusLabel(status: number): string {
    return DEFAULT_STATUS_LABELS[status] ?? `Status ${status}`;
  }

  private getFinalStatus(taskType: string, fallbackStatus: number): number {
    return this.getTaskTypeSchema(taskType)?.finalStatus ?? fallbackStatus;
  }

  private getTaskTypeSchema(taskType: string): TaskTypeSchemaDto | null {
    return (
      this.taskTypeSchemas().find((schema) => schema.taskType.toLowerCase() === taskType.toLowerCase()) ?? null
    );
  }

  private loadTaskTypeMetadata(): void {
    this.taskTypeMetadataInFlight.set(true);

    this.taskService
      .getTaskTypes()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.taskTypeMetadataInFlight.set(false))
      )
      .subscribe({
        next: (taskTypes) => this.setTaskTypeMetadata(taskTypes),
        error: () => this.setFallbackTaskTypeMetadata()
      });
  }

  private setTaskTypeMetadata(taskTypes: readonly TaskTypeSchemaDto[]): void {
    if (taskTypes.length === 0) {
      this.setFallbackTaskTypeMetadata();
      return;
    }

    this.taskTypeSchemas.set(taskTypes);
    this.ensureCreateTaskTypeIsSelected();
  }

  private setFallbackTaskTypeMetadata(): void {
    this.taskTypeSchemas.set(FALLBACK_TASK_TYPE_SCHEMAS);
    this.ensureCreateTaskTypeIsSelected();
  }

  private ensureCreateTaskTypeIsSelected(): void {
    const typeControl = this.createForm.controls['createTaskType'];
    const selectedType = typeControl.value;
    const options = this.taskTypeOptions();
    if (options.length === 0) {
      typeControl.setValue('');
      return;
    }

    if (!selectedType || !options.some((option) => option.value === selectedType)) {
      typeControl.setValue(options[0].value);
    }
  }

  private resetStatusFields(): void {
    resetTaskFieldControls(this.statusForm, this.taskTypeSchemas());
    const fallbackControl = this.statusForm.controls['fallbackJson'];
    resetControl(fallbackControl);
    fallbackControl.clearValidators();
    fallbackControl.updateValueAndValidity({ emitEvent: false });
  }

  private hydrateStatusFields(task: BaseTaskDto): void {
    const data = task.customFields ?? {};
    const schema = this.getTaskTypeSchema(task.taskType);
    if (schema && schema.fields.length > 0) {
      hydrateTaskFieldControls(this.statusForm, schema, data);
      return;
    }

    this.statusForm.controls['fallbackJson'].setValue(JSON.stringify(data, null, 2), { emitEvent: false });
  }

  private buildPayload(taskType: string, status: number): TaskCustomData {
    const schema = this.getTaskTypeSchema(taskType);
    if (schema && getTaskFieldViewModels(schema, status).length > 0) {
      this.statusForm.controls['fallbackJson'].setErrors(null);
      return buildTaskFieldPayload(this.statusForm, schema, status);
    }

    const fallbackControl = this.statusForm.controls['fallbackJson'];
    const parsedResult = parseTaskCustomDataJson(fallbackControl.value);
    if (parsedResult.isValid) {
      fallbackControl.setErrors(null);
      return parsedResult.data;
    }

    fallbackControl.setErrors({ invalidJson: true });
    return {};
  }

  private loadTaskDetails(taskId: number): void {
    this.taskService
      .getTask(taskId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (taskDetails) => {
          if (this.selectedTask()?.id !== taskId) {
            return;
          }

          this.selectedTask.set(taskDetails);
          this.resetStatusFields();
          this.hydrateStatusFields(taskDetails);
          this.statusForm.controls['nextAssignedToUserId'].setValue(taskDetails.assignedToUserId);
          this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(taskDetails));
        },
        error: () => {
          // Errors are propagated to taskService.error.
        }
      });
  }
}
