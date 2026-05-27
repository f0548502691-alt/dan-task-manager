export const CREATED_TASK_STATUS = 1;
export const CLOSED_TASK_STATUS = 99;

export const TASK_STATUS = {
  CREATED: CREATED_TASK_STATUS,
  CLOSED: CLOSED_TASK_STATUS
} as const;

export const DEFAULT_STATUS_LABELS: Readonly<Record<number, string>> = {
  [TASK_STATUS.CREATED]: 'Created',
  [TASK_STATUS.CLOSED]: 'Closed'
};

export type TaskCustomData = Record<string, unknown>;

export interface TaskFieldRuleDto {
  field: string;
  type: string;
  required: boolean;
  minLength?: number | null;
  maxLength?: number | null;
  minValue?: number | null;
  maxValue?: number | null;
  arrayLength?: number | null;
  minItems?: number | null;
  maxItems?: number | null;
  elementType?: string | null;
  pattern?: string | null;
  appliesFromStatus?: number | null;
  appliesToStatus?: number | null;
  appliesOnClose?: boolean;
  allowedValues?: readonly string[] | null;
  isIndexed?: boolean;
}

export interface TaskTypeSchemaDto {
  taskType: string;
  displayName?: string;
  finalStatus?: number | null;
  isActive: boolean;
  version: number;
  fields?: readonly TaskFieldRuleDto[];
}

export interface PagedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface BaseTaskDto {
  id: number;
  taskType: string;
  currentStatus: number;
  assignedToUserId: number;
  description: string;
  customFields?: TaskCustomData;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTaskRequest {
  taskType: string;
  description: string;
  assignedToUserId: number;
  customFields?: TaskCustomData;
}

export interface ChangeStatusWorkflowRequest {
  newStatus: number;
  nextAssignedToUserId: number;
  customFields: TaskCustomData;
}

export interface CloseTaskRequest {
  nextAssignedToUserId: number;
  finalNotes: string;
}

export interface ChangeStatusWorkflowResponse {
  message: string;
  task: BaseTaskDto;
}

export interface CloseTaskResponse {
  message: string;
  task: BaseTaskDto;
}

export interface ApiErrorResponse {
  error: string;
  code?: string;
}
