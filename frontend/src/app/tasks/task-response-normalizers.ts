import {
  BaseTaskDto,
  ChangeStatusWorkflowResponse,
  CloseTaskResponse,
  PagedResultDto,
  TASK_STATUS,
  TaskCustomData
} from './task.interfaces';

const DEFAULT_TIMESTAMP = new Date(0).toISOString();

export function normalizeTaskCollection(payload: PagedResultDto<unknown> | unknown[]): BaseTaskDto[] {
  if (Array.isArray(payload)) {
    return payload.map((task) => normalizeTask(task));
  }

  if (isPagedResult(payload) && Array.isArray(payload.items)) {
    return payload.items.map((task) => normalizeTask(task));
  }

  return [];
}

export function normalizeChangeStatusResponse(payload: unknown): ChangeStatusWorkflowResponse {
  const response = asRecord(payload);
  const task = normalizeTask(response['task']);
  const newStatus = toNumber(response['newStatus'], task.currentStatus);

  return {
    success: toBoolean(response['success']),
    message: toStringValue(response['message'], ''),
    newStatus,
    task
  };
}

export function normalizeCloseResponse(payload: unknown): CloseTaskResponse {
  const response = asRecord(payload);

  return {
    success: toBoolean(response['success']),
    message: toStringValue(response['message'], ''),
    task: normalizeTask(response['task'])
  };
}

export function normalizeTask(payload: unknown): BaseTaskDto {
  const task = asRecord(payload);
  const assignedToUserPayload = task['assignedToUser'];
  const assignedToUser =
    assignedToUserPayload && typeof assignedToUserPayload === 'object' && !Array.isArray(assignedToUserPayload)
      ? {
          id: toNumber((assignedToUserPayload as Record<string, unknown>)['id']),
          name: toStringValue((assignedToUserPayload as Record<string, unknown>)['name'], ''),
          email: toStringValue((assignedToUserPayload as Record<string, unknown>)['email'], ''),
          createdAt: toStringValue((assignedToUserPayload as Record<string, unknown>)['createdAt'], '')
        }
      : null;

  return {
    id: toNumber(task['id']),
    taskType: toStringValue(task['taskType'], ''),
    currentStatus: toNumber(task['currentStatus'], TASK_STATUS.IN_PROGRESS),
    assignedToUserId: toNumber(task['assignedToUserId']),
    assignedToUser,
    description: toStringValue(task['description'], ''),
    customDataJson: extractCustomDataJson(task),
    createdAt: toStringValue(task['createdAt'], DEFAULT_TIMESTAMP),
    updatedAt: toStringValue(task['updatedAt'], DEFAULT_TIMESTAMP)
  };
}

function extractCustomDataJson(task: Record<string, unknown>): string {
  const customDataJson = task['customDataJson'];
  if (typeof customDataJson === 'string') {
    return customDataJson;
  }

  const customFields = task['customFields'];
  if (customFields === null || customFields === undefined) {
    return '{}';
  }

  if (typeof customFields === 'string') {
    return customFields;
  }

  if (typeof customFields === 'object' && !Array.isArray(customFields)) {
    return JSON.stringify(customFields as TaskCustomData);
  }

  return '{}';
}

function isPagedResult(value: unknown): value is PagedResultDto<unknown> {
  return value !== null && typeof value === 'object' && 'items' in value;
}

function asRecord(value: unknown): Record<string, unknown> {
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }

  throw new Error('Unexpected task response payload.');
}

function toNumber(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function toStringValue(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
}

function toBoolean(value: unknown): boolean {
  return value === true;
}
