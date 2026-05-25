import { CommonModule } from '@angular/common';
import { Component, computed, inject, Injector, Input, OnChanges, signal, SimpleChanges, Type } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { BaseTaskDto } from '../../task.interfaces';
import { TaskService } from '../../task.service';
import { DefaultFieldsComponent } from '../dynamic-fields/default-fields.component';
import { DevelopmentFieldsComponent } from '../dynamic-fields/development-fields.component';
import { ProcurementFieldsComponent } from '../dynamic-fields/procurement-fields.component';
import { TASK_DYNAMIC_FIELDS_CONTEXT } from '../dynamic-fields/task-dynamic-fields-context';

type DynamicFieldsComponent = Type<unknown>;

interface TaskStatusUpdateForm {
  newStatus: FormControl<number>;
  details: FormGroup;
}

const TASK_TYPE_COMPONENTS: Record<string, DynamicFieldsComponent> = {
  procurement: ProcurementFieldsComponent,
  development: DevelopmentFieldsComponent
};

@Component({
  selector: 'app-task-list-status',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './task-list-status.component.html',
  styleUrls: ['./task-list-status.component.scss']
})
export class TaskListStatusComponent implements OnChanges {
  @Input({ required: true }) currentUserId!: number;

  private readonly taskService = inject(TaskService);
  private readonly rootInjector = inject(Injector);

  readonly tasks = this.taskService.tasks;
  readonly isLoading = this.taskService.isLoading;
  readonly serverError = this.taskService.error;

  readonly selectedTaskId = signal<number | null>(null);
  readonly isSubmitting = signal(false);

  private readonly formSignal = signal<FormGroup<TaskStatusUpdateForm>>(this.createForm(1));
  private readonly initialDataSignal = signal<Record<string, unknown>>({});

  readonly updateForm = this.formSignal.asReadonly();

  readonly selectedTask = computed(() => {
    const selectedTaskId = this.selectedTaskId();
    if (selectedTaskId === null) {
      return null;
    }

    return this.tasks().find((task) => task.id === selectedTaskId) ?? null;
  });

  readonly dynamicFieldsComponent = computed<DynamicFieldsComponent>(() => {
    const task = this.selectedTask();
    if (!task) {
      return DefaultFieldsComponent;
    }

    return TASK_TYPE_COMPONENTS[task.taskType.toLowerCase()] ?? DefaultFieldsComponent;
  });

  readonly dynamicFieldsInjector = computed(() => {
    const task = this.selectedTask();
    if (!task) {
      return this.rootInjector;
    }

    return Injector.create({
      parent: this.rootInjector,
      providers: [
        {
          provide: TASK_DYNAMIC_FIELDS_CONTEXT,
          useValue: {
            task,
            detailsGroup: this.formSignal().controls.details,
            newStatusControl: this.formSignal().controls.newStatus,
            initialData: this.initialDataSignal()
          }
        }
      ]
    });
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (!('currentUserId' in changes)) {
      return;
    }

    if (typeof this.currentUserId !== 'number') {
      return;
    }

    this.selectedTaskId.set(null);
    this.initialDataSignal.set({});
    this.formSignal.set(this.createForm(1));
    this.taskService.setCurrentUserId(this.currentUserId);
  }

  selectTask(task: BaseTaskDto): void {
    this.selectedTaskId.set(task.id);
    this.initialDataSignal.set(this.parseCustomData(task.customDataJson));
    this.formSignal.set(this.createForm(this.defaultNextStatus(task.currentStatus)));
  }

  getAvailableStatuses(task: BaseTaskDto): number[] {
    const statuses: number[] = [];
    for (let status = 0; status <= task.currentStatus + 1; status += 1) {
      if (status !== task.currentStatus) {
        statuses.push(status);
      }
    }
    return statuses;
  }

  submitStatusUpdate(): void {
    const task = this.selectedTask();
    const form = this.formSignal();

    if (!task) {
      return;
    }

    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }

    const newStatus = form.controls.newStatus.value;
    const details = form.controls.details.getRawValue();
    const mergedData = {
      ...this.parseCustomData(task.customDataJson),
      ...details
    };

    this.isSubmitting.set(true);

    this.taskService
      .changeTaskStatus(task.id, {
        newStatus,
        newDataJson: JSON.stringify(mergedData)
      })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          this.initialDataSignal.set(this.parseCustomData(response.task.customDataJson));
          this.formSignal.set(this.createForm(this.defaultNextStatus(response.task.currentStatus)));
        },
        error: () => {
          // Error message is stored in TaskService error signal.
        }
      });
  }

  trackByTaskId(_: number, task: BaseTaskDto): number {
    return task.id;
  }

  private createForm(initialStatus: number): FormGroup<TaskStatusUpdateForm> {
    return new FormGroup<TaskStatusUpdateForm>({
      newStatus: new FormControl<number>(initialStatus, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(0)]
      }),
      details: new FormGroup({})
    });
  }

  private defaultNextStatus(currentStatus: number): number {
    return currentStatus + 1;
  }

  private parseCustomData(customDataJson: string): Record<string, unknown> {
    try {
      const parsed = JSON.parse(customDataJson);
      return parsed !== null && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
    } catch {
      return {};
    }
  }
}
