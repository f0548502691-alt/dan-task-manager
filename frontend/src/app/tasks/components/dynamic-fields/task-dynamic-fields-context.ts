import { FormControl, FormGroup } from '@angular/forms';
import { InjectionToken } from '@angular/core';
import { BaseTaskDto } from '../../task.interfaces';

export interface TaskDynamicFieldsContext {
  task: BaseTaskDto;
  detailsGroup: FormGroup;
  newStatusControl: FormControl<number>;
  initialData: Record<string, unknown>;
}

export const TASK_DYNAMIC_FIELDS_CONTEXT = new InjectionToken<TaskDynamicFieldsContext>(
  'TASK_DYNAMIC_FIELDS_CONTEXT'
);
