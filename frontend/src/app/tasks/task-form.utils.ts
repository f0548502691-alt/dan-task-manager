import { AbstractControl, ValidatorFn } from '@angular/forms';
import { TaskCustomData } from './task.interfaces';

export function syncControlState(control: AbstractControl | null, enabled: boolean, validators: ValidatorFn[]): void {
  if (!control) {
    return;
  }

  if (enabled) {
    control.setValidators(validators);
  } else {
    resetControl(control);
    control.clearValidators();
  }

  control.updateValueAndValidity({ emitEvent: false });
}

export function resetControl(control: AbstractControl | null, value: unknown = ''): void {
  if (!control) {
    return;
  }

  control.setValue(value, { emitEvent: false });
  control.setErrors(null);
  control.markAsPristine();
  control.markAsUntouched();
}

export interface ParseTaskCustomDataResult {
  data: TaskCustomData;
  isValid: boolean;
}

export function parseTaskCustomDataJson(rawValue: string): ParseTaskCustomDataResult {
  if (!rawValue.trim()) {
    return { data: {}, isValid: true };
  }

  try {
    const parsed: unknown = JSON.parse(rawValue);
    return {
      data: isTaskCustomData(parsed) ? parsed : {},
      isValid: true
    };
  } catch {
    return { data: {}, isValid: false };
  }
}

function isTaskCustomData(value: unknown): value is TaskCustomData {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
