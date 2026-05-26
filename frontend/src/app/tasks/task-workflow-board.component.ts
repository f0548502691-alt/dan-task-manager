import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { BaseTaskDto, ChangeStatusWorkflowRequest, TASK_STATUS } from './task.interfaces';
import { TaskService } from './task.service';
import { DevelopmentFieldsComponent } from './development-fields.component';
import { ProcurementFieldsComponent } from './procurement-fields.component';
import {
  WorkflowFieldValues,
  buildWorkflowPayload,
  canCloseTaskStatus,
  getSuggestedTaskStatus,
  getTaskStatusLabel,
  getTaskStatusOptions,
  hydrateWorkflowFields
} from './task-workflow-board.logic';

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

  readonly statusOptions = computed(() => getTaskStatusOptions(this.selectedTask()));

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

    this.statusForm.controls['newStatus'].setValue(getSuggestedTaskStatus(task));
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

    const payloadResult = this.buildPayload(task.taskType, this.selectedNextStatus);
    if (payloadResult.invalidFallbackJson) {
      return;
    }

    const request: ChangeStatusWorkflowRequest = {
      newStatus: this.selectedNextStatus,
      newDataJson: JSON.stringify(payloadResult.payload)
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
          this.statusForm.controls['newStatus'].setValue(getSuggestedTaskStatus(response.task));
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
    return canCloseTaskStatus(task.currentStatus);
  }

  taskStatusLabel(status: number): string {
    return getTaskStatusLabel(status);
  }

  trackByTaskId(_: number, task: BaseTaskDto): number {
    return task.id;
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
    this.statusForm.patchValue(hydrateWorkflowFields(task), { emitEvent: false });
  }

  private buildPayload(taskType: string, status: number): ReturnType<typeof buildWorkflowPayload> {
    const fallbackControl = this.statusForm.controls['fallbackJson'];
    const result = buildWorkflowPayload(taskType, status, this.getWorkflowFieldValues());

    if (result.invalidFallbackJson) {
      fallbackControl.setErrors({ invalidJson: true });
    } else {
      fallbackControl.setErrors(null);
    }

    return result;
  }

  private getWorkflowFieldValues(): WorkflowFieldValues {
    return {
      priceA: this.statusForm.controls['priceA'].value,
      priceB: this.statusForm.controls['priceB'].value,
      receipt: this.statusForm.controls['receipt'].value,
      specification: this.statusForm.controls['specification'].value,
      branchName: this.statusForm.controls['branchName'].value,
      versionNumber: this.statusForm.controls['versionNumber'].value,
      fallbackJson: this.statusForm.controls['fallbackJson'].value
    };
  }
}
