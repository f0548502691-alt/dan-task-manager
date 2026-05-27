export const CLOSED_TASK_STATUS = 99;

export const TASK_STATUS = {
  IN_PROGRESS: 1,
  READY_FOR_REVIEW: 2,
  DONE: 3,
  RELEASED: 4,
  CLOSED: CLOSED_TASK_STATUS
} as const;

export const DEFAULT_STATUS_LABELS: Readonly<Record<number, string>> = {
  [TASK_STATUS.IN_PROGRESS]: 'In Progress',
  [TASK_STATUS.READY_FOR_REVIEW]: 'Ready for Review',
  [TASK_STATUS.DONE]: 'Done',
  [TASK_STATUS.RELEASED]: 'Released',
  [TASK_STATUS.CLOSED]: 'Closed'
};

export const DEFAULT_TASK_FINAL_STATUS_BY_TYPE: Readonly<Record<string, number>> = {
  Procurement: TASK_STATUS.DONE,
  Development: TASK_STATUS.RELEASED
};

export type TaskCustomData = Record<string, unknown>;

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

export interface AppUserDto {
  id: number;
  name: string;
  email: string;
  createdAt: string;
}

export interface BaseTaskDto {
  id: number;
  taskType: string;
  currentStatus: number;
  assignedToUserId: number;
  assignedToUser?: AppUserDto | null;
  description: string;
  customDataJson: string;
  createdAt: string;
  updatedAt: string;
}

export interface AppUserWithTasksDto extends AppUserDto {
  tasks: BaseTaskDto[];
}

export interface CreateUserRequest {
  name: string;
  email: string;
}

export interface CreateTaskRequest {
  taskType: string;
  description: string;
  assignedToUserId: number;
  customDataJson?: string;
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
  code?: string;
}
