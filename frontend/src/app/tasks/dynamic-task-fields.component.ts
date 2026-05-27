import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {
  TaskCustomData,
  TaskFieldRuleDto,
  TaskTypeSchemaDto
} from './task.interfaces';
import {
  ResolvedFieldRule,
  hydrateCustomFieldsGroup,
  rebuildCustomFieldsGroup
} from './task-schema.utils';

@Component({
  selector: 'app-dynamic-task-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './dynamic-task-fields.component.html',
  styleUrl: './dynamic-task-fields.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DynamicTaskFieldsComponent implements OnChanges {
  @Input({ required: true }) group!: FormGroup;
  @Input() schema: TaskTypeSchemaDto | null = null;
  @Input({ required: true }) status!: number;
  @Input() values: TaskCustomData | null = null;

  @Output() readonly schemaResolved = new EventEmitter<readonly ResolvedFieldRule[]>();

  resolvedFields: ResolvedFieldRule[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.group) {
      return;
    }

    const schemaOrStatusChanged = 'schema' in changes || 'status' in changes || 'group' in changes;

    if (schemaOrStatusChanged) {
      this.resolvedFields = rebuildCustomFieldsGroup(this.group, this.schema, this.status);
      hydrateCustomFieldsGroup(this.group, this.resolvedFields, this.values);
      this.schemaResolved.emit(this.resolvedFields);
      return;
    }

    if ('values' in changes) {
      hydrateCustomFieldsGroup(this.group, this.resolvedFields, this.values);
    }
  }

  isInvalid(control: FormControl | FormArray | null): boolean {
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  asFormControl(control: unknown): FormControl {
    return control as FormControl;
  }

  asFormArray(control: unknown): FormArray<FormControl> {
    return control as FormArray<FormControl>;
  }

  fieldLabel(rule: TaskFieldRuleDto): string {
    return formatFieldLabel(rule.field);
  }

  itemLabel(rule: TaskFieldRuleDto, index: number): string {
    return `${formatFieldLabel(rule.field)} #${index + 1}`;
  }

  validationMessage(rule: TaskFieldRuleDto, control: FormControl | FormArray | null): string {
    if (!control || !control.errors) {
      return '';
    }

    const label = formatFieldLabel(rule.field);

    if (control.errors['required']) {
      return `${label} is required.`;
    }
    if (control.errors['minlength']) {
      const requiredLength = control.errors['minlength'].requiredLength;
      return `${label} must contain at least ${requiredLength} characters.`;
    }
    if (control.errors['maxlength']) {
      const requiredLength = control.errors['maxlength'].requiredLength;
      return `${label} cannot contain more than ${requiredLength} characters.`;
    }
    if (control.errors['min']) {
      return `${label} must be greater than or equal to ${control.errors['min'].min}.`;
    }
    if (control.errors['max']) {
      return `${label} must be less than or equal to ${control.errors['max'].max}.`;
    }
    if (control.errors['pattern']) {
      return `${label} does not match the required format.`;
    }

    return `${label} is invalid.`;
  }
}

function formatFieldLabel(field: string): string {
  if (!field) {
    return '';
  }
  const spaced = field.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
