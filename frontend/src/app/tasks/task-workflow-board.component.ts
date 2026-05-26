import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  BaseTaskDto,
  ChangeStatusWorkflowRequest,
  DEFAULT_STATUS_LABELS,
  TASK_STATUS,
  TASK_FINAL_STATUS_BY_TYPE,
  TaskCustomData
} from './task.interfaces';
import { TaskService } from './task.service';
import { DevelopmentFieldsComponent } from './development-fields.component';
import { ProcurementFieldsComponent } from './procurement-fields.component';

interface StatusOption {
  value: number;
  label: string;
}

const DEFAULT_CURRENT_USER_ID = 1;
const TASK_TYPE_OPTIONS = ['Procurement', 'Development'] as const;

@Component({
  selector: 'app-task-workflow-board',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ProcurementFieldsComponent, DevelopmentFieldsComponent],
  templateUrl: './task-workflow-board.component.html',
  styleUrls: ['./task-workflow-board.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TaskWorkflowBoardComponent implements OnInit {
  private readonly fb = inject(FormBuilder);

  readonly TASK_STATUS = TASK_STATUS;
  readonly taskService = inject(TaskService);
  readonly currentUserId = DEFAULT_CURRENT_USER_ID;
  readonly taskTypeOptions = [...TASK_TYPE_OPTIONS];
  readonly selectedTask = signal<BaseTaskDto | null>(null);
  readonly createInFlight = signal(false);
  readonly submitInFlight = signal(false);
  readonly closeInFlight = signal(false);
  readonly successMessage = signal<string | null>(null);

  readonly createForm = this.fb.group({
    createTaskType: this.fb.nonNullable.control<string>(TASK_TYPE_OPTIONS[0], [Validators.required]),
    createTaskDescription: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(5)])
  });

  readonly statusForm = this.fb.group({
    newStatus: this.fb.control<number | null>(null, [Validators.required]),
    priceA: this.fb.nonNullable.control(''),
    priceB: this.fb.nonNullable.control(''),
    receipt: this.fb.nonNullable.control(''),
    specification: this.fb.nonNullable.control(''),
    branchName: this.fb.nonNullable.control(''),
    versionNumber: this.fb.nonNullable.control(''),
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

    const finalStatus = TASK_FINAL_STATUS_BY_TYPE[task.taskType] ?? task.currentStatus;
    const maxStatus = Math.max(finalStatus, task.currentStatus);
    const options: StatusOption[] = [];

    for (let status = 0; status <= maxStatus; status += 1) {
      options.push({ value: status, label: this.getStatusLabel(status) });
    }

    return options;
  });

  get selectedNextStatus(): number {
    return Number(this.statusForm.controls['newStatus'].value ?? 0);
  }

  ngOnInit(): void {
    this.taskService.setCurrentUserId(this.currentUserId);
  }

  submitCreateTask(): void {
    const typeControl = this.createForm.controls['createTaskType'];
    const descriptionControl = this.createForm.controls['createTaskDescription'];
    if (typeControl.invalid || descriptionControl.invalid) {
      typeControl.markAsTouched();
      descriptionControl.markAsTouched();
      return;
    }

    const taskType = typeControl.value?.trim();
    const description = descriptionControl.value?.trim();

    if (!taskType || !description) {
      return;
    }

    this.createInFlight.set(true);
    this.successMessage.set(null);
    this.taskService.clearError();

    this.taskService
      .createTask({
        taskType,
        description,
        assignedToUserId: this.currentUserId
      })
      .pipe(finalize(() => this.createInFlight.set(false)))
      .subscribe({
        next: (task) => {
          this.successMessage.set(`Task #${task.id} created successfully.`);
          this.createForm.controls['createTaskDescription'].setValue('');
          this.createForm.controls['createTaskDescription'].markAsPristine();
          this.createForm.controls['createTaskDescription'].markAsUntouched();
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
    this.closeForm.controls['closeNotes'].setValue('');
    this.closeForm.controls['closeNotes'].markAsPristine();
    this.closeForm.controls['closeNotes'].markAsUntouched();

    this.resetStatusSpecificFields();
    this.hydrateStatusFields(task);

    this.statusForm.controls['newStatus'].setValue(this.getSuggestedStatus(task));
  }

  submitStatusUpdate(): void {
    const task = this.selectedTask();
    if (!task || task.currentStatus === TASK_STATUS.CLOSED) {
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

    const request: ChangeStatusWorkflowRequest = {
      newStatus: this.selectedNextStatus,
      newDataJson: JSON.stringify(payload)
    };

    this.submitInFlight.set(true);
    this.successMessage.set(null);
    this.taskService.clearError();

    this.taskService
      .changeTaskStatus(task.id, request)
      .pipe(finalize(() => this.submitInFlight.set(false)))
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.selectedTask.set(response.task);
          this.resetStatusSpecificFields();
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
      .closeTask(task.id, { finalNotes })
      .pipe(finalize(() => this.closeInFlight.set(false)))
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.selectedTask.set(response.task);
          closeNotesControl.setValue('');
          closeNotesControl.markAsPristine();
          closeNotesControl.markAsUntouched();
        },
        error: () => {
          // Errors are propagated to taskService.error.
        }
      });
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
    return task.currentStatus !== TASK_STATUS.CLOSED;
  }

  taskStatusLabel(status: number): string {
    return this.getStatusLabel(status);
  }

  trackByTaskId(_: number, task: BaseTaskDto): number {
    return task.id;
  }

  private getSuggestedStatus(task: BaseTaskDto): number {
    const finalStatus = TASK_FINAL_STATUS_BY_TYPE[task.taskType];
    if (typeof finalStatus !== 'number') {
      return task.currentStatus;
    }

    return Math.min(task.currentStatus + 1, finalStatus);
  }

  private getStatusLabel(status: number): string {
    return DEFAULT_STATUS_LABELS[status] ?? `Status ${status}`;
  }

  private resetStatusSpecificFields(): void {
    const dynamicControls = [
      'priceA',
      'priceB',
      'receipt',
      'specification',
      'branchName',
      'versionNumber',
      'fallbackJson'
    ] as const;

    for (const controlName of dynamicControls) {
      const control = this.statusForm.controls[controlName];
      control.setValue('');
      control.clearValidators();
      control.setErrors(null);
      control.markAsPristine();
      control.markAsUntouched();
      control.updateValueAndValidity({ emitEvent: false });
    }
  }

  private hydrateStatusFields(task: BaseTaskDto): void {
    const data = this.safeParseTaskData(task.customDataJson);

    if (task.taskType === 'Procurement') {
      const prices = Array.isArray(data['prices']) ? data['prices'] : [];
      this.statusForm.patchValue(
        {
          priceA: typeof prices[0] === 'string' ? prices[0] : '',
          priceB: typeof prices[1] === 'string' ? prices[1] : '',
          receipt: typeof data['receipt'] === 'string' ? data['receipt'] : ''
        },
        { emitEvent: false }
      );
      return;
    }

    if (task.taskType === 'Development') {
      this.statusForm.patchValue(
        {
          specification: typeof data['specification'] === 'string' ? data['specification'] : '',
          branchName: typeof data['branchName'] === 'string' ? data['branchName'] : '',
          versionNumber:
            typeof data['versionNumber'] === 'string' || typeof data['versionNumber'] === 'number'
              ? String(data['versionNumber'])
              : ''
        },
        { emitEvent: false }
      );
      return;
    }

    this.statusForm.controls['fallbackJson'].setValue(JSON.stringify(data, null, 2), { emitEvent: false });
  }

  private buildPayload(taskType: string, status: number): TaskCustomData {
    if (taskType === 'Procurement') {
      if (status === TASK_STATUS.READY_FOR_REVIEW) {
        return {
          prices: [this.statusForm.controls['priceA'].value, this.statusForm.controls['priceB'].value]
        };
      }

      if (status === TASK_STATUS.DONE) {
        return {
          receipt: this.statusForm.controls['receipt'].value
        };
      }

      return {};
    }

    if (taskType === 'Development') {
      if (status === TASK_STATUS.READY_FOR_REVIEW) {
        return {
          specification: this.statusForm.controls['specification'].value
        };
      }

      if (status === TASK_STATUS.DONE) {
        return {
          branchName: this.statusForm.controls['branchName'].value
        };
      }

      if (status === TASK_STATUS.RELEASED) {
        return {
          versionNumber: this.statusForm.controls['versionNumber'].value
        };
      }

      return {};
    }

    return this.parseFallbackJson(this.statusForm.controls['fallbackJson'].value);
  }

  private parseFallbackJson(value: string): TaskCustomData {
    const fallbackControl = this.statusForm.controls['fallbackJson'];

    if (!value.trim()) {
      fallbackControl.setErrors(null);
      return {};
    }

    try {
      const parsed: unknown = JSON.parse(value);
      fallbackControl.setErrors(null);
      return this.isTaskCustomData(parsed) ? parsed : {};
    } catch {
      fallbackControl.setErrors({ invalidJson: true });
      return {};
    }
  }

  private safeParseTaskData(value: string): TaskCustomData {
    if (!value.trim()) {
      return {};
    }

    try {
      const parsed: unknown = JSON.parse(value);
      return this.isTaskCustomData(parsed) ? parsed : {};
    } catch {
      return {};
    }
  }

  private isTaskCustomData(value: unknown): value is TaskCustomData {
    return value !== null && typeof value === 'object' && !Array.isArray(value);
  }
}
