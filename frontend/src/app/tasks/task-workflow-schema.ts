import { FormControl, FormGroup, ValidatorFn, Validators } from '@angular/forms';
import { TASK_STATUS, TaskCustomData, TaskFieldSchemaDto, TaskTypeSchemaDto } from './task.interfaces';
import { resetControl } from './task-form.utils';

export interface TaskFieldViewModel {
  readonly field: TaskFieldSchemaDto;
  readonly label: string;
  readonly controlNames: readonly string[];
  readonly isArray: boolean;
  readonly inputType: 'text' | 'number' | 'textarea';
}

export const FALLBACK_TASK_TYPE_SCHEMAS: readonly TaskTypeSchemaDto[] = [
  {
    taskType: 'Procurement',
    displayName: 'Procurement',
    finalStatus: TASK_STATUS.STATUS_3,
    isActive: true,
    version: 1,
    fields: [
      {
        field: 'prices',
        type: 'array',
        required: true,
        arrayLength: 2,
        elementType: 'string',
        appliesFromStatus: TASK_STATUS.STATUS_2,
        appliesToStatus: TASK_STATUS.STATUS_2,
        isIndexed: false
      },
      {
        field: 'receipt',
        type: 'string',
        required: true,
        appliesFromStatus: TASK_STATUS.STATUS_3,
        appliesToStatus: TASK_STATUS.STATUS_3,
        isIndexed: false
      }
    ]
  },
  {
    taskType: 'Development',
    displayName: 'Development',
    finalStatus: TASK_STATUS.STATUS_4,
    isActive: true,
    version: 1,
    fields: [
      {
        field: 'specification',
        type: 'string',
        required: true,
        minLength: 10,
        appliesFromStatus: TASK_STATUS.STATUS_2,
        appliesToStatus: TASK_STATUS.STATUS_2,
        isIndexed: false
      },
      {
        field: 'branchName',
        type: 'string',
        required: true,
        pattern: 'valid_git_branch',
        appliesFromStatus: TASK_STATUS.STATUS_3,
        appliesToStatus: TASK_STATUS.STATUS_3,
        isIndexed: true
      },
      {
        field: 'versionNumber',
        type: 'stringOrNumber',
        required: true,
        pattern: 'semantic_version',
        appliesFromStatus: TASK_STATUS.STATUS_4,
        appliesToStatus: TASK_STATUS.STATUS_4,
        isIndexed: false
      }
    ]
  }
];

const NAMED_PATTERNS: Readonly<Record<string, RegExp>> = {
  valid_git_branch: /^\S+$/,
  semantic_version: /^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/
};

export function getTaskFieldViewModels(
  schema: TaskTypeSchemaDto | null | undefined,
  status: number
): readonly TaskFieldViewModel[] {
  if (!schema) {
    return [];
  }

  return schema.fields
    .filter((field) => isFieldApplicable(field, status))
    .map((field) => ({
      field,
      label: humanizeFieldName(field.field),
      controlNames: getControlNames(field),
      isArray: isArrayField(field),
      inputType: getInputType(field)
    }));
}

export function syncTaskFieldControls(
  form: FormGroup,
  schemas: readonly TaskTypeSchemaDto[],
  activeSchema: TaskTypeSchemaDto | null | undefined,
  status: number
): void {
  const activeControls = new Set(
    getTaskFieldViewModels(activeSchema, status).flatMap((field) => field.controlNames)
  );

  for (const schema of schemas) {
    for (const field of schema.fields) {
      for (const controlName of getControlNames(field)) {
        ensureControl(form, controlName);
        const control = form.get(controlName);
        if (!control) {
          continue;
        }

        if (activeControls.has(controlName)) {
          control.setValidators(createValidators(field));
        } else {
          resetControl(control);
          control.clearValidators();
        }

        control.updateValueAndValidity({ emitEvent: false });
      }
    }
  }
}

export function resetTaskFieldControls(form: FormGroup, schemas: readonly TaskTypeSchemaDto[]): void {
  for (const schema of schemas) {
    for (const field of schema.fields) {
      for (const controlName of getControlNames(field)) {
        const control = form.get(controlName);
        resetControl(control);
        control?.clearValidators();
        control?.updateValueAndValidity({ emitEvent: false });
      }
    }
  }
}

export function hydrateTaskFieldControls(
  form: FormGroup,
  schema: TaskTypeSchemaDto,
  data: TaskCustomData
): void {
  for (const field of schema.fields) {
    const controlNames = getControlNames(field);
    if (isArrayField(field)) {
      const values = Array.isArray(data[field.field]) ? (data[field.field] as unknown[]) : [];
      controlNames.forEach((controlName, index) => {
        ensureControl(form, controlName);
        form.get(controlName)?.setValue(formatControlValue(values[index]), { emitEvent: false });
      });
      continue;
    }

    const controlName = controlNames[0];
    ensureControl(form, controlName);
    form.get(controlName)?.setValue(formatControlValue(data[field.field]), { emitEvent: false });
  }
}

export function buildTaskFieldPayload(
  form: FormGroup,
  schema: TaskTypeSchemaDto,
  status: number
): TaskCustomData {
  const payload: TaskCustomData = {};

  for (const viewModel of getTaskFieldViewModels(schema, status)) {
    if (viewModel.isArray) {
      payload[viewModel.field.field] = viewModel.controlNames.map((controlName) =>
        coercePayloadValue(form.get(controlName)?.value, viewModel.field.elementType ?? 'string')
      );
      continue;
    }

    const controlName = viewModel.controlNames[0];
    payload[viewModel.field.field] = coercePayloadValue(
      form.get(controlName)?.value,
      viewModel.field.type
    );
  }

  return payload;
}

export function getControlName(field: TaskFieldSchemaDto, index?: number): string {
  return index === undefined ? `custom:${field.field}` : `custom:${field.field}:${index}`;
}

function getControlNames(field: TaskFieldSchemaDto): readonly string[] {
  if (!isArrayField(field)) {
    return [getControlName(field)];
  }

  const itemCount = field.arrayLength ?? field.minItems ?? field.maxItems ?? 1;
  return Array.from({ length: Math.max(itemCount, 1) }, (_, index) => getControlName(field, index));
}

function ensureControl(form: FormGroup, controlName: string): void {
  if (!form.get(controlName)) {
    form.addControl(controlName, new FormControl('', { nonNullable: true }));
  }
}

function createValidators(field: TaskFieldSchemaDto): ValidatorFn[] {
  const validators: ValidatorFn[] = [];

  if (field.required) {
    validators.push(Validators.required);
  }
  if (typeof field.minLength === 'number') {
    validators.push(Validators.minLength(field.minLength));
  }
  if (typeof field.maxLength === 'number') {
    validators.push(Validators.maxLength(field.maxLength));
  }
  if (typeof field.minValue === 'number') {
    validators.push(Validators.min(field.minValue));
  }
  if (typeof field.maxValue === 'number') {
    validators.push(Validators.max(field.maxValue));
  }
  if (field.pattern) {
    validators.push(Validators.pattern(NAMED_PATTERNS[field.pattern] ?? field.pattern));
  }

  return validators;
}

function isFieldApplicable(field: TaskFieldSchemaDto, status: number): boolean {
  const fromStatus = field.appliesFromStatus ?? Number.MIN_SAFE_INTEGER;
  const toStatus = field.appliesToStatus ?? Number.MAX_SAFE_INTEGER;
  return status >= fromStatus && status <= toStatus;
}

function isArrayField(field: TaskFieldSchemaDto): boolean {
  return field.type.toLowerCase() === 'array';
}

function getInputType(field: TaskFieldSchemaDto): 'text' | 'number' | 'textarea' {
  const normalizedType = field.type.toLowerCase();
  if (normalizedType === 'number' || normalizedType === 'integer' || normalizedType === 'decimal') {
    return 'number';
  }

  if ((field.maxLength ?? 0) > 120 || field.field.toLowerCase().includes('specification')) {
    return 'textarea';
  }

  return 'text';
}

function coercePayloadValue(value: unknown, type: string): unknown {
  const normalizedType = type.toLowerCase();
  if (normalizedType === 'number' || normalizedType === 'integer' || normalizedType === 'decimal') {
    const numericValue = Number(value);
    return Number.isNaN(numericValue) ? value : numericValue;
  }

  if (normalizedType === 'boolean') {
    if (value === 'true') {
      return true;
    }
    if (value === 'false') {
      return false;
    }
  }

  return value ?? '';
}

function formatControlValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  return String(value);
}

function humanizeFieldName(field: string): string {
  return field
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[-_]/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase());
}
