export const CLOSED_TASK_STATUS = 99;

export const TASK_STATUS = {
  CREATED: 1,
  STATUS_2: 2,
  STATUS_3: 3,
  STATUS_4: 4,
  CLOSED: CLOSED_TASK_STATUS
} as const;

export const DEFAULT_STATUS_LABELS: Readonly<Record<number, string>> = {
  [TASK_STATUS.CREATED]: 'Created',
  [TASK_STATUS.STATUS_2]: 'Status 2',
  [TASK_STATUS.STATUS_3]: 'Status 3',
  [TASK_STATUS.STATUS_4]: 'Status 4',
  [TASK_STATUS.CLOSED]: 'Closed'
};

export const TASK_STATUS_LABELS_BY_TYPE: Readonly<Record<string, Readonly<Record<number, string>>>> = {
  Procurement: {
    [TASK_STATUS.CREATED]: 'Created',
    [TASK_STATUS.STATUS_2]: 'Supplier offers received',
    [TASK_STATUS.STATUS_3]: 'Purchase completed',
    [TASK_STATUS.CLOSED]: 'Closed'
  },
  Development: {
    [TASK_STATUS.CREATED]: 'Created',
    [TASK_STATUS.STATUS_2]: 'Specification completed',
    [TASK_STATUS.STATUS_3]: 'Development completed',
    [TASK_STATUS.STATUS_4]: 'Distribution completed',
    [TASK_STATUS.CLOSED]: 'Closed'
  }
};

export const DEFAULT_TASK_FINAL_STATUS_BY_TYPE: Readonly<Record<string, number>> = {
  Procurement: TASK_STATUS.STATUS_3,
  Development: TASK_STATUS.STATUS_4
};

export type TaskCustomData = Record<string, unknown>;

export interface UserBriefDto {
  id: number;
  name: string;
  email: string;
}

export interface TaskFieldSchemaDto {
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
  allowedValues?: string[] | null;
  isIndexed: boolean;
}

export interface TaskTypeSchemaDto {
  taskType: string;
  displayName: string;
  finalStatus?: number | null;
  isActive: boolean;
  version: number;
  fields: TaskFieldSchemaDto[];
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
  assignedToUser?: UserBriefDto | null;
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

export interface UpdateTaskRequest {
  description?: string;
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
  success: boolean;
  message: string;
  newStatus: number;
  task: BaseTaskDto;
}

export interface CloseTaskResponse {
  success: boolean;
  message: string;
  task: BaseTaskDto;
}

export interface ApiErrorResponse {
  error: string;
}
