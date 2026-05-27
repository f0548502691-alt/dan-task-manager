import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { TaskTypeSchemaDto } from './task.interfaces';
import { getTaskFieldViewModels, syncTaskFieldControls, TaskFieldViewModel } from './task-workflow-schema';

@Component({
  selector: 'app-task-dynamic-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './task-dynamic-fields.component.html',
  styles: [
    `
      .dynamic-fields,
      .dynamic-fields__group {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }

      .dynamic-fields__group {
        border: 1px solid #d6dbe3;
        border-radius: 0.375rem;
        margin: 0;
        padding: 0.75rem;
      }

      .dynamic-fields__group legend {
        font-weight: 600;
        padding: 0 0.25rem;
      }

      label {
        font-weight: 600;
      }

      input,
      textarea {
        border: 1px solid #c8d0de;
        border-radius: 0.375rem;
        font: inherit;
        padding: 0.5rem 0.625rem;
      }

      small {
        color: #b00020;
      }
    `
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TaskDynamicFieldsComponent implements OnChanges {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) schemas: readonly TaskTypeSchemaDto[] = [];
  @Input() schema: TaskTypeSchemaDto | null = null;
  @Input({ required: true }) status!: number;

  fields: readonly TaskFieldViewModel[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if ('form' in changes || 'schemas' in changes || 'schema' in changes || 'status' in changes) {
      this.fields = getTaskFieldViewModels(this.schema, this.status);

      if (this.form) {
        syncTaskFieldControls(this.form, this.schemas, this.schema, this.status);
      }
    }
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  errorMessage(field: TaskFieldViewModel, controlName: string): string {
    const control = this.form.get(controlName);
    if (!control?.errors) {
      return `${field.label} is invalid.`;
    }

    if (control.errors['required']) {
      return `${field.label} is required.`;
    }
    if (control.errors['minlength']) {
      return `${field.label} is too short.`;
    }
    if (control.errors['maxlength']) {
      return `${field.label} is too long.`;
    }
    if (control.errors['min']) {
      return `${field.label} is too small.`;
    }
    if (control.errors['max']) {
      return `${field.label} is too large.`;
    }
    if (control.errors['pattern']) {
      return `${field.label} has an invalid format.`;
    }

    return `${field.label} is invalid.`;
  }
}
