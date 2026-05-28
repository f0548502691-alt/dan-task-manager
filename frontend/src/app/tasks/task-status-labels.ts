import {
  DEFAULT_STATUS_LABELS,
  TASK_TYPE_STATUS_LABELS,
  TaskFieldRuleDto,
  TaskTypeSchemaDto
} from './task.interfaces';

export function getTaskStatusLabel(
  status: number,
  taskType?: string | null,
  schema?: TaskTypeSchemaDto | null
): string {
  return (
    DEFAULT_STATUS_LABELS[status] ??
    getTaskTypeStatusLabel(taskType, status) ??
    getSchemaStatusLabel(schema, status) ??
    `Status ${status}`
  );
}

export function getDropdownStatusLabel(
  status: number,
  taskType: string,
  finalStatus: number,
  schema?: TaskTypeSchemaDto | null
): string {
  const label = getTaskStatusLabel(status, taskType, schema);
  if (label !== `Status ${status}`) {
    return label;
  }

  return status === finalStatus ? 'Ready to close' : label;
}

export function getFinalStatus(schema: TaskTypeSchemaDto | null | undefined, fallbackStatus: number): number {
  if (schema && typeof schema.finalStatus === 'number') {
    return schema.finalStatus;
  }

  return fallbackStatus;
}

function getTaskTypeStatusLabel(taskType: string | null | undefined, status: number): string | null {
  if (!taskType) {
    return null;
  }

  const normalizedTaskType = taskType.trim().toLowerCase();
  const labels = Object.entries(TASK_TYPE_STATUS_LABELS).find(
    ([knownTaskType]) => knownTaskType.toLowerCase() === normalizedTaskType
  )?.[1];

  return labels?.[status] ?? null;
}

function getSchemaStatusLabel(schema: TaskTypeSchemaDto | null | undefined, status: number): string | null {
  const fields = getApplicableFields(schema, status);
  if (fields.length === 0) {
    return null;
  }

  const fieldLabels = fields
    .map((field) => formatFieldLabel(field.field))
    .filter((label) => label.length > 0);

  return fieldLabels.length > 0 ? fieldLabels.join(' + ') : null;
}

function getApplicableFields(
  schema: TaskTypeSchemaDto | null | undefined,
  status: number
): readonly TaskFieldRuleDto[] {
  if (!schema?.fields || schema.fields.length === 0) {
    return [];
  }

  return schema.fields.filter((rule) => isFieldApplicableForStatus(rule, status));
}

function isFieldApplicableForStatus(rule: TaskFieldRuleDto, status: number): boolean {
  const from = rule.appliesFromStatus ?? Number.NEGATIVE_INFINITY;
  const to = rule.appliesToStatus ?? Number.POSITIVE_INFINITY;
  return status >= from && status <= to;
}

function formatFieldLabel(field: string): string {
  const spaced = field.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
