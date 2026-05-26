import { FormControl, FormGroup } from '@angular/forms';

export interface TaskWorkflowFormControls {
  newStatus: FormControl<number | null>;
  priceA: FormControl<string>;
  priceB: FormControl<string>;
  receipt: FormControl<string>;
  specification: FormControl<string>;
  branchName: FormControl<string>;
  versionNumber: FormControl<string>;
  fallbackJson: FormControl<string>;
}

export type TaskWorkflowForm = FormGroup<TaskWorkflowFormControls>;
export type TaskWorkflowDynamicControlName = Exclude<keyof TaskWorkflowFormControls, 'newStatus'>;
