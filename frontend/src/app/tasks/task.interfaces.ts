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

export const TASK_FINAL_STATUS_BY_TYPE: Readonly<Record<string, number>> = {
  Procurement: TASK_STATUS.STATUS_3,
  Development: TASK_STATUS.STATUS_4
};

export type TaskCustomData = Record<string, unknown>;

export interface UserBriefDto {
  id: number;
  name: string;
  email: string;
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

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
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
