export const CLOSED_TASK_STATUS = 99;

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
  newDataJson: string;
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
