import {
  BaseTaskDto,
  DEFAULT_STATUS_LABELS,
  TASK_FINAL_STATUS_BY_TYPE,
  TASK_STATUS,
  TaskCustomData
} from './task.interfaces';

export interface StatusOption {
  value: number;
  label: string;
}

export interface WorkflowFieldValues {
  priceA: string;
  priceB: string;
  receipt: string;
  specification: string;
  branchName: string;
  versionNumber: string;
  fallbackJson: string;
}

export interface PayloadBuildResult {
  payload: TaskCustomData;
  invalidFallbackJson: boolean;
}

export const EMPTY_WORKFLOW_FIELD_VALUES: WorkflowFieldValues = {
  priceA: '',
  priceB: '',
  receipt: '',
  specification: '',
  branchName: '',
  versionNumber: '',
  fallbackJson: ''
};

export function getTaskStatusLabel(status: number): string {
  return DEFAULT_STATUS_LABELS[status] ?? `Status ${status}`;
}

export function getTaskStatusOptions(task: BaseTaskDto | null): StatusOption[] {
  if (!task) {
    return [];
  }

  if (task.currentStatus === TASK_STATUS.CLOSED) {
    return [{ value: TASK_STATUS.CLOSED, label: getTaskStatusLabel(TASK_STATUS.CLOSED) }];
  }

  const finalStatus = TASK_FINAL_STATUS_BY_TYPE[task.taskType] ?? task.currentStatus;
  const maxStatus = Math.max(finalStatus, task.currentStatus);
  const options: StatusOption[] = [];

  for (let status = 0; status <= maxStatus; status += 1) {
    options.push({ value: status, label: getTaskStatusLabel(status) });
  }

  return options;
}

export function getSuggestedTaskStatus(task: BaseTaskDto): number {
  const finalStatus = TASK_FINAL_STATUS_BY_TYPE[task.taskType];
  if (typeof finalStatus !== 'number') {
    return task.currentStatus;
  }

  return Math.min(task.currentStatus + 1, finalStatus);
}

export function canCloseTaskStatus(currentStatus: number): boolean {
  return currentStatus !== TASK_STATUS.CLOSED;
}

export function hydrateWorkflowFields(task: BaseTaskDto): WorkflowFieldValues {
  const data = safeParseTaskCustomData(task.customDataJson);
  const fields = { ...EMPTY_WORKFLOW_FIELD_VALUES };

  if (task.taskType === 'Procurement') {
    const prices = Array.isArray(data['prices']) ? data['prices'] : [];
    return {
      ...fields,
      priceA: typeof prices[0] === 'string' ? prices[0] : '',
      priceB: typeof prices[1] === 'string' ? prices[1] : '',
      receipt: typeof data['receipt'] === 'string' ? data['receipt'] : ''
    };
  }

  if (task.taskType === 'Development') {
    return {
      ...fields,
      specification: typeof data['specification'] === 'string' ? data['specification'] : '',
      branchName: typeof data['branchName'] === 'string' ? data['branchName'] : '',
      versionNumber:
        typeof data['versionNumber'] === 'string' || typeof data['versionNumber'] === 'number'
          ? String(data['versionNumber'])
          : ''
    };
  }

  return {
    ...fields,
    fallbackJson: JSON.stringify(data, null, 2)
  };
}

export function buildWorkflowPayload(
  taskType: string,
  status: number,
  fields: WorkflowFieldValues
): PayloadBuildResult {
  if (taskType === 'Procurement') {
    if (status === TASK_STATUS.READY_FOR_REVIEW) {
      return {
        payload: { prices: [fields.priceA, fields.priceB] },
        invalidFallbackJson: false
      };
    }

    if (status === TASK_STATUS.DONE) {
      return {
        payload: { receipt: fields.receipt },
        invalidFallbackJson: false
      };
    }

    return { payload: {}, invalidFallbackJson: false };
  }

  if (taskType === 'Development') {
    if (status === TASK_STATUS.READY_FOR_REVIEW) {
      return {
        payload: { specification: fields.specification },
        invalidFallbackJson: false
      };
    }

    if (status === TASK_STATUS.DONE) {
      return {
        payload: { branchName: fields.branchName },
        invalidFallbackJson: false
      };
    }

    if (status === TASK_STATUS.RELEASED) {
      return {
        payload: { versionNumber: fields.versionNumber },
        invalidFallbackJson: false
      };
    }

    return { payload: {}, invalidFallbackJson: false };
  }

  return parseFallbackJson(fields.fallbackJson);
}

export function safeParseTaskCustomData(value: string): TaskCustomData {
  if (!value.trim()) {
    return {};
  }

  try {
    const parsed: unknown = JSON.parse(value);
    return isTaskCustomData(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function parseFallbackJson(value: string): PayloadBuildResult {
  if (!value.trim()) {
    return { payload: {}, invalidFallbackJson: false };
  }

  try {
    const parsed: unknown = JSON.parse(value);
    return {
      payload: isTaskCustomData(parsed) ? parsed : {},
      invalidFallbackJson: false
    };
  } catch {
    return { payload: {}, invalidFallbackJson: true };
  }
}

function isTaskCustomData(value: unknown): value is TaskCustomData {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
