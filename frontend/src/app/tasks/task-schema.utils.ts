import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { TaskCustomData, TaskFieldRuleDto, TaskTypeSchemaDto } from './task.interfaces';

const TEXTAREA_MIN_MAX_LENGTH = 120;

export type FieldRenderKind =
  | 'text'
  | 'textarea'
  | 'number'
  | 'select'
  | 'checkbox'
  | 'array-scalar';

export interface ResolvedFieldRule {
  rule: TaskFieldRuleDto;
  kind: FieldRenderKind;
  itemCount: number;
}

export function getApplicableFields(
  schema: TaskTypeSchemaDto | null | undefined,
  status: number
): TaskFieldRuleDto[] {
  if (!schema?.fields || schema.fields.length === 0) {
    return [];
  }

  return schema.fields.filter((rule) => isFieldApplicableForStatus(rule, status));
}

export function isFieldApplicableForStatus(rule: TaskFieldRuleDto, status: number): boolean {
  const from = rule.appliesFromStatus ?? Number.NEGATIVE_INFINITY;
  const to = rule.appliesToStatus ?? Number.POSITIVE_INFINITY;
  return status >= from && status <= to;
}

export function resolveFieldRule(rule: TaskFieldRuleDto): ResolvedFieldRule {
  const type = (rule.type ?? '').toLowerCase();
  const elementType = (rule.elementType ?? '').toLowerCase();

  if (type === 'array') {
    const fallbackItemCount = Math.max(rule.arrayLength ?? rule.minItems ?? rule.maxItems ?? 1, 1);
    return { rule, kind: 'array-scalar', itemCount: fallbackItemCount };
  }

  if (type === 'boolean' || elementType === 'boolean') {
    return { rule, kind: 'checkbox', itemCount: 1 };
  }

  if (rule.allowedValues && rule.allowedValues.length > 0) {
    return { rule, kind: 'select', itemCount: 1 };
  }

  if (type === 'number' || type === 'integer' || type === 'decimal') {
    return { rule, kind: 'number', itemCount: 1 };
  }

  const maxLength = rule.maxLength ?? 0;
  if (type === 'string' && maxLength > TEXTAREA_MIN_MAX_LENGTH) {
    return { rule, kind: 'textarea', itemCount: 1 };
  }

  if (type === 'string' && (rule.minLength ?? 0) >= 10) {
    return { rule, kind: 'textarea', itemCount: 1 };
  }

  return { rule, kind: 'text', itemCount: 1 };
}

export function buildValidatorsForRule(rule: TaskFieldRuleDto): ValidatorFn[] {
  const validators: ValidatorFn[] = [];

  if (rule.required) {
    validators.push(Validators.required);
  }

  if (typeof rule.minLength === 'number') {
    validators.push(Validators.minLength(rule.minLength));
  }

  if (typeof rule.maxLength === 'number') {
    validators.push(Validators.maxLength(rule.maxLength));
  }

  if (typeof rule.minValue === 'number') {
    validators.push(Validators.min(rule.minValue));
  }

  if (typeof rule.maxValue === 'number') {
    validators.push(Validators.max(rule.maxValue));
  }

  if (rule.pattern) {
    try {
      const regex = new RegExp(rule.pattern);
      validators.push(Validators.pattern(regex));
    } catch {
      // ignore invalid regex from the server
    }
  }

  return validators;
}

function defaultValueForKind(kind: FieldRenderKind): unknown {
  switch (kind) {
    case 'checkbox':
      return false;
    case 'number':
      return null;
    default:
      return '';
  }
}

export function buildControlForRule(resolved: ResolvedFieldRule): AbstractControl {
  const itemValidators = buildValidatorsForRule(resolved.rule);

  if (resolved.kind === 'array-scalar') {
    const elementType = (resolved.rule.elementType ?? 'string').toLowerCase();
    const elementInitialValue = elementType === 'number' || elementType === 'integer' ? null : '';
    const controls: FormControl[] = [];
    for (let index = 0; index < resolved.itemCount; index += 1) {
      controls.push(new FormControl(elementInitialValue, itemValidators));
    }
    return new FormArray<FormControl>(controls);
  }

  return new FormControl(defaultValueForKind(resolved.kind), itemValidators);
}

export function rebuildCustomFieldsGroup(
  group: FormGroup,
  schema: TaskTypeSchemaDto | null | undefined,
  status: number
): ResolvedFieldRule[] {
  for (const controlName of Object.keys(group.controls)) {
    group.removeControl(controlName, { emitEvent: false });
  }

  const applicable = getApplicableFields(schema, status);
  const resolved: ResolvedFieldRule[] = [];

  for (const rule of applicable) {
    if (!rule.field) {
      continue;
    }

    const resolvedRule = resolveFieldRule(rule);
    const control = buildControlForRule(resolvedRule);
    group.addControl(rule.field, control, { emitEvent: false });
    resolved.push(resolvedRule);
  }

  group.updateValueAndValidity({ emitEvent: false });
  return resolved;
}

export function hydrateCustomFieldsGroup(
  group: FormGroup,
  resolvedFields: readonly ResolvedFieldRule[],
  data: TaskCustomData | null | undefined
): void {
  const safeData = data ?? {};

  for (const { rule, kind, itemCount } of resolvedFields) {
    const control = group.get(rule.field);
    if (!control) {
      continue;
    }

    const incomingValue = safeData[rule.field];

    if (kind === 'array-scalar' && control instanceof FormArray) {
      const elementType = (rule.elementType ?? 'string').toLowerCase();
      const sourceArray = Array.isArray(incomingValue) ? incomingValue : [];
      for (let index = 0; index < itemCount; index += 1) {
        const itemControl = control.at(index);
        if (!itemControl) {
          continue;
        }
        const sourceItem = sourceArray[index];
        itemControl.setValue(coerceScalarToControlValue(sourceItem, elementType), {
          emitEvent: false
        });
      }
      continue;
    }

    const ruleType = (rule.type ?? '').toLowerCase();
    control.setValue(coerceScalarToControlValue(incomingValue, ruleType, kind), {
      emitEvent: false
    });
  }

  group.updateValueAndValidity({ emitEvent: false });
  group.markAsPristine();
  group.markAsUntouched();
}

function coerceScalarToControlValue(
  rawValue: unknown,
  ruleType: string,
  kind?: FieldRenderKind
): unknown {
  if (kind === 'checkbox' || ruleType === 'boolean') {
    return typeof rawValue === 'boolean' ? rawValue : false;
  }

  if (kind === 'number' || ruleType === 'number' || ruleType === 'integer' || ruleType === 'decimal') {
    if (typeof rawValue === 'number' && Number.isFinite(rawValue)) {
      return rawValue;
    }
    if (typeof rawValue === 'string' && rawValue.trim() !== '') {
      const parsed = Number(rawValue);
      return Number.isFinite(parsed) ? parsed : null;
    }
    return null;
  }

  if (typeof rawValue === 'string') {
    return rawValue;
  }

  if (typeof rawValue === 'number' && Number.isFinite(rawValue)) {
    return String(rawValue);
  }

  if (typeof rawValue === 'boolean') {
    return String(rawValue);
  }

  return '';
}

export function buildPayloadFromGroup(
  group: FormGroup,
  resolvedFields: readonly ResolvedFieldRule[]
): TaskCustomData {
  const payload: TaskCustomData = {};

  for (const { rule, kind } of resolvedFields) {
    const control = group.get(rule.field);
    if (!control) {
      continue;
    }

    if (kind === 'array-scalar' && control instanceof FormArray) {
      const elementType = (rule.elementType ?? 'string').toLowerCase();
      payload[rule.field] = control.controls.map((itemControl) =>
        coercePayloadValue(itemControl.value, elementType)
      );
      continue;
    }

    payload[rule.field] = coercePayloadValue(control.value, (rule.type ?? '').toLowerCase(), kind);
  }

  return payload;
}

function coercePayloadValue(
  rawValue: unknown,
  ruleType: string,
  kind?: FieldRenderKind
): unknown {
  if (kind === 'checkbox' || ruleType === 'boolean') {
    return rawValue === true;
  }

  if (kind === 'number' || ruleType === 'number' || ruleType === 'integer' || ruleType === 'decimal') {
    if (typeof rawValue === 'number' && Number.isFinite(rawValue)) {
      return rawValue;
    }
    if (typeof rawValue === 'string' && rawValue.trim() !== '') {
      const parsed = Number(rawValue);
      return Number.isFinite(parsed) ? parsed : rawValue;
    }
    return null;
  }

  if (ruleType === 'stringornumber') {
    if (typeof rawValue === 'number' || typeof rawValue === 'string') {
      return rawValue;
    }
    return rawValue == null ? '' : String(rawValue);
  }

  if (rawValue == null) {
    return '';
  }

  return typeof rawValue === 'string' ? rawValue : String(rawValue);
}
