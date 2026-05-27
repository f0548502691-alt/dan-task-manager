import {
  BaseTaskDto,
  DEFAULT_STATUS_LABELS,
  DEFAULT_TASK_FINAL_STATUS_BY_TYPE,
  TASK_STATUS,
  TaskTypeSchemaDto
} from './task.interfaces';

export interface StatusOption {
  value: number;
  label: string;
}

export interface TaskTypeMetadataState {
  taskTypeOptions: readonly string[];
  finalStatusByType: Readonly<Record<string, number>>;
}

export const FALLBACK_TASK_TYPE_OPTIONS = ['Procurement', 'Development'] as const;

export function normalizeTaskTypes(taskTypes: readonly TaskTypeSchemaDto[]): TaskTypeSchemaDto[] {
  return taskTypes
    .filter(
      (taskType) =>
        taskType.isActive && typeof taskType.taskType === 'string' && taskType.taskType.trim().length > 0
    )
    .sort((left, right) => left.taskType.localeCompare(right.taskType));
}

export function buildTaskTypeMetadataState(taskTypes: readonly TaskTypeSchemaDto[]): TaskTypeMetadataState {
  if (taskTypes.length === 0) {
    return {
      taskTypeOptions: FALLBACK_TASK_TYPE_OPTIONS,
      finalStatusByType: DEFAULT_TASK_FINAL_STATUS_BY_TYPE
    };
  }

  const finalStatusByType: Record<string, number> = { ...DEFAULT_TASK_FINAL_STATUS_BY_TYPE };
  for (const taskType of taskTypes) {
    if (typeof taskType.finalStatus === 'number') {
      finalStatusByType[taskType.taskType] = taskType.finalStatus;
    }
  }

  return {
    taskTypeOptions: taskTypes.map((taskType) => taskType.taskType),
    finalStatusByType
  };
}

export function buildStatusOptions(
  task: Pick<BaseTaskDto, 'currentStatus' | 'taskType'>,
  finalStatusByType: Readonly<Record<string, number>>
): StatusOption[] {
  if (task.currentStatus === TASK_STATUS.CLOSED) {
    return [{ value: TASK_STATUS.CLOSED, label: getStatusLabel(TASK_STATUS.CLOSED) }];
  }

  const finalStatus = getFinalStatus(task.taskType, task.currentStatus, finalStatusByType);
  const maxStatus = Math.max(finalStatus, task.currentStatus);
  const options: StatusOption[] = [];
  const minStatus = TASK_STATUS.IN_PROGRESS;

  for (let status = minStatus; status <= maxStatus; status += 1) {
    options.push({ value: status, label: getStatusLabel(status) });
  }

  return options;
}

export function getSuggestedStatus(
  task: Pick<BaseTaskDto, 'currentStatus' | 'taskType'>,
  finalStatusByType: Readonly<Record<string, number>>
): number {
  const finalStatus = getFinalStatus(task.taskType, task.currentStatus, finalStatusByType);
  return Math.min(task.currentStatus + 1, finalStatus);
}

export function getStatusLabel(status: number): string {
  return DEFAULT_STATUS_LABELS[status] ?? `Status ${status}`;
}

function getFinalStatus(
  taskType: string,
  fallbackStatus: number,
  finalStatusByType: Readonly<Record<string, number>>
): number {
  return finalStatusByType[taskType] ?? fallbackStatus;
}
