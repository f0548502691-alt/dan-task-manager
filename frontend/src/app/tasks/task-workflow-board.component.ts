import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  BaseTaskDto,
  ChangeStatusWorkflowRequest,
  DEFAULT_STATUS_LABELS,
  TASK_STATUS,
  TaskCustomData,
  TaskTypeSchemaDto
} from './task.interfaces';
import { TaskService } from './task.service';
import { DynamicTaskFieldsComponent } from './dynamic-task-fields.component';
import { parseTaskCustomDataJson, resetControl } from './task-form.utils';
import {
  ResolvedFieldRule,
  buildPayloadFromGroup,
  getApplicableFields
} from './task-schema.utils';

interface StatusOption {
  value: number;
  label: string;
}

const DEFAULT_CURRENT_USER_ID = 1;

@Component({
  selector: 'app-task-workflow-board',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DynamicTaskFieldsComponent],
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
  readonly taskTypeOptions = signal<readonly string[]>([]);
  readonly selectedTask = signal<BaseTaskDto | null>(null);
  readonly createInFlight = signal(false);
  readonly submitInFlight = signal(false);
  readonly closeInFlight = signal(false);
  readonly taskTypeMetadataInFlight = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly currentSchema = signal<TaskTypeSchemaDto | null>(null);
  readonly hydrationValues = signal<TaskCustomData | null>(null);
  private resolvedFields: readonly ResolvedFieldRule[] = [];

  readonly createForm = this.fb.group({
    createTaskType: this.fb.nonNullable.control<string>('', [Validators.required]),
    createTaskDescription: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(5)]),
    createAssignedToUserId: this.fb.nonNullable.control(DEFAULT_CURRENT_USER_ID, [Validators.required, Validators.min(1)])
  });

  readonly statusForm = this.fb.group({
    newStatus: this.fb.control<number | null>(null, [Validators.required]),
    nextAssignedToUserId: this.fb.nonNullable.control(DEFAULT_CURRENT_USER_ID, [Validators.required, Validators.min(1)]),
    customFields: this.fb.group({}),
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
    if (task.currentStatus === TASK_STATUS.CLOSED) {
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

  get customFieldsGroup(): FormGroup {
    return this.statusForm.controls['customFields'] as FormGroup;
  }

  get selectedNextStatus(): number {
    return Number(this.statusForm.controls['newStatus'].value ?? TASK_STATUS.CREATED);
  }

  get hasSchemaForSelected(): boolean {
    const schema = this.currentSchema();
    return !!schema?.fields && schema.fields.length > 0;
  }

  get hasApplicableSchemaFields(): boolean {
    return getApplicableFields(this.currentSchema(), this.selectedNextStatus).length > 0;
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
          // Errors are propagated to the global error service.
        }
      });
  }

  selectTask(task: BaseTaskDto): void {
    this.selectedTask.set(task);
    this.taskService.clearError();
    this.successMessage.set(null);
    resetControl(this.closeForm.controls['closeNotes']);

    this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(task));
    this.statusForm.controls['nextAssignedToUserId'].setValue(task.assignedToUserId);

    this.refreshSchemaContext(task);
    this.loadTaskDetails(task.id);
  }

  submitStatusUpdate(): void {
    const task = this.selectedTask();
    if (!task || task.currentStatus === TASK_STATUS.CLOSED) {
      return;
    }

    if (this.statusForm.controls['newStatus'].invalid || this.statusForm.controls['nextAssignedToUserId'].invalid) {
      this.statusForm.controls['newStatus'].markAsTouched();
      this.statusForm.controls['nextAssignedToUserId'].markAsTouched();
      return;
    }

    const nextStatus = this.selectedNextStatus;
    const payload = this.buildPayload(nextStatus);
    if (payload === null) {
      return;
    }

    const nextAssignedToUserId = Number(this.statusForm.controls['nextAssignedToUserId'].value);
    if (nextAssignedToUserId < 1) {
      this.statusForm.controls['nextAssignedToUserId'].markAsTouched();
      return;
    }

    const request: ChangeStatusWorkflowRequest = {
      newStatus: nextStatus,
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
          this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(response.task));
          this.refreshSchemaContext(response.task);
        },
        error: () => {
          // Errors are propagated to the global error service.
        }
      });
  }

  submitCloseTask(): void {
    const task = this.selectedTask();
    if (!task || task.currentStatus === TASK_STATUS.CLOSED) {
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
          // Errors are propagated to the global error service.
        }
      });
  }

  onResolvedFieldsChanged(resolved: readonly ResolvedFieldRule[]): void {
    this.resolvedFields = resolved;
  }

  isCreateDescriptionInvalid(): boolean {
    const control = this.createForm.controls['createTaskDescription'];
    return control.invalid && (control.touched || control.dirty);
  }

  isCloseNotesInvalid(): boolean {
    const control = this.closeForm.controls['closeNotes'];
    return control.invalid && (control.touched || control.dirty);
  }

  canCloseTask(task: BaseTaskDto): boolean {
    const finalStatus = this.getFinalStatus(task.taskType, task.currentStatus);
    return typeof finalStatus === 'number' && task.currentStatus === finalStatus;
  }

  taskStatusLabel(status: number): string {
    return this.getStatusLabel(status);
  }

  trackByTaskId(_: number, task: BaseTaskDto): number {
    return task.id;
  }

  private refreshSchemaContext(task: BaseTaskDto): void {
    const schema = this.taskService.getSchema(task.taskType) ?? null;
    this.currentSchema.set(schema);
    this.hydrationValues.set(task.customFields ?? {});

    const fallbackControl = this.statusForm.controls['fallbackJson'];
    if (schema?.fields && schema.fields.length > 0) {
      resetControl(fallbackControl);
      fallbackControl.clearValidators();
      fallbackControl.updateValueAndValidity({ emitEvent: false });
    } else {
      fallbackControl.setValue(JSON.stringify(task.customFields ?? {}, null, 2), { emitEvent: false });
      fallbackControl.setErrors(null);
      fallbackControl.markAsPristine();
      fallbackControl.markAsUntouched();
    }
  }

  private getSuggestedStatus(task: BaseTaskDto): number {
    const finalStatus = this.getFinalStatus(task.taskType, task.currentStatus);
    return Math.min(task.currentStatus + 1, finalStatus);
  }

  private getStatusLabel(status: number): string {
    return DEFAULT_STATUS_LABELS[status] ?? `Status ${status}`;
  }

  private getFinalStatus(taskType: string, fallbackStatus: number): number {
    const schema = this.taskService.getSchema(taskType);
    if (schema && typeof schema.finalStatus === 'number') {
      return schema.finalStatus;
    }
    return fallbackStatus;
  }

  private buildPayload(status: number): TaskCustomData | null {
    const schema = this.currentSchema();
    if (schema?.fields && schema.fields.length > 0) {
      const applicable = getApplicableFields(schema, status);
      const resolvedForStatus = this.resolvedFields.filter((entry) =>
        applicable.some((rule) => rule.field === entry.rule.field)
      );

      if (this.customFieldsGroup.invalid) {
        this.customFieldsGroup.markAllAsTouched();
        return null;
      }

      return buildPayloadFromGroup(this.customFieldsGroup, resolvedForStatus);
    }

    const fallbackControl = this.statusForm.controls['fallbackJson'];
    const parsedResult = parseTaskCustomDataJson(fallbackControl.value);
    if (!parsedResult.isValid) {
      fallbackControl.setErrors({ invalidJson: true });
      return null;
    }

    fallbackControl.setErrors(null);
    return parsedResult.data;
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
        error: () => this.setTaskTypeMetadata([])
      });
  }

  private setTaskTypeMetadata(taskTypes: readonly TaskTypeSchemaDto[]): void {
    this.taskTypeOptions.set(taskTypes.map((taskType) => taskType.taskType));
    this.ensureCreateTaskTypeIsSelected();

    const selected = this.selectedTask();
    if (selected) {
      this.refreshSchemaContext(selected);
    }
  }

  private ensureCreateTaskTypeIsSelected(): void {
    const typeControl = this.createForm.controls['createTaskType'];
    const selectedType = typeControl.value;
    const options = this.taskTypeOptions();
    if (options.length === 0) {
      typeControl.setValue('');
      return;
    }

    if (!selectedType || !options.includes(selectedType)) {
      typeControl.setValue(options[0]);
    }
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
          this.statusForm.controls['nextAssignedToUserId'].setValue(taskDetails.assignedToUserId);
          this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(taskDetails));
          this.refreshSchemaContext(taskDetails);
        },
        error: () => {
          // Errors are propagated to the global error service.
        }
      });
  }
}
