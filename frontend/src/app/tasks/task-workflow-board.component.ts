import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
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
import {
  TaskWorkflowDynamicControlName,
  TaskWorkflowForm,
  TaskWorkflowFormControls
} from './task-workflow-form.types';

interface StatusOption {
  value: number;
  label: string;
}

@Component({
  selector: 'app-task-workflow-board',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ProcurementFieldsComponent, DevelopmentFieldsComponent],
  templateUrl: './task-workflow-board.component.html',
  styleUrls: ['./task-workflow-board.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TaskWorkflowBoardComponent {
  readonly taskService = inject(TaskService);
  readonly selectedTask = signal<BaseTaskDto | null>(null);
  readonly submitInFlight = signal(false);
  readonly successMessage = signal<string | null>(null);

  readonly form: TaskWorkflowForm = new FormGroup<TaskWorkflowFormControls>({
    newStatus: new FormControl<number | null>(null, { validators: [Validators.required] }),
    priceA: new FormControl('', { nonNullable: true }),
    priceB: new FormControl('', { nonNullable: true }),
    receipt: new FormControl('', { nonNullable: true }),
    specification: new FormControl('', { nonNullable: true }),
    branchName: new FormControl('', { nonNullable: true }),
    versionNumber: new FormControl('', { nonNullable: true }),
    fallbackJson: new FormControl('', { nonNullable: true })
  });

  readonly statusOptions = computed<StatusOption[]>(() => {
    const task = this.selectedTask();
    if (!task) {
      return [];
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
    return this.form.controls.newStatus.value ?? TASK_STATUS.BACKLOG;
  }

  selectTask(task: BaseTaskDto): void {
    this.selectedTask.set(task);
    this.taskService.clearError();
    this.successMessage.set(null);

    this.resetStatusSpecificFields();
    this.hydrateStatusFields(task);

    this.form.controls.newStatus.setValue(this.getSuggestedStatus(task));
  }

  submitStatusUpdate(): void {
    const task = this.selectedTask();
    if (!task) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.buildPayload(task.taskType, this.selectedNextStatus);
    if (this.form.controls.fallbackJson.hasError('invalidJson')) {
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
          this.form.controls.newStatus.setValue(this.getSuggestedStatus(response.task));
        },
        error: () => {
          // Errors are propagated to taskService.error.
        }
      });
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
    const dynamicControls: TaskWorkflowDynamicControlName[] = [
      'priceA',
      'priceB',
      'receipt',
      'specification',
      'branchName',
      'versionNumber',
      'fallbackJson'
    ];

    for (const controlName of dynamicControls) {
      const control = this.form.controls[controlName];
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
      this.form.patchValue(
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
      this.form.patchValue(
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

    this.form.controls.fallbackJson.setValue(JSON.stringify(data, null, 2), { emitEvent: false });
  }

  private buildPayload(taskType: string, status: number): TaskCustomData {
    if (taskType === 'Procurement') {
      if (status === TASK_STATUS.READY_FOR_REVIEW) {
        return {
          prices: [this.form.controls.priceA.value, this.form.controls.priceB.value]
        };
      }

      if (status === TASK_STATUS.DONE) {
        return {
          receipt: this.form.controls.receipt.value
        };
      }

      return {};
    }

    if (taskType === 'Development') {
      if (status === TASK_STATUS.READY_FOR_REVIEW) {
        return {
          specification: this.form.controls.specification.value
        };
      }

      if (status === TASK_STATUS.DONE) {
        return {
          branchName: this.form.controls.branchName.value
        };
      }

      if (status === TASK_STATUS.RELEASED) {
        return {
          versionNumber: this.form.controls.versionNumber.value
        };
      }

      return {};
    }

    return this.parseFallbackJson(this.form.controls.fallbackJson.value);
  }

  private parseFallbackJson(value: string): TaskCustomData {
    const fallbackControl = this.form.controls.fallbackJson;

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
