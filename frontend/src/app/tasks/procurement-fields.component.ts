import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { TASK_STATUS } from './task.interfaces';
import { TaskWorkflowForm } from './task-workflow-form.types';

type ProcurementControlName = 'priceA' | 'priceB' | 'receipt';

@Component({
  selector: 'app-procurement-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './procurement-fields.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProcurementFieldsComponent implements OnChanges {
  @Input({ required: true }) form!: TaskWorkflowForm;
  @Input({ required: true }) status!: number;

  ngOnChanges(changes: SimpleChanges): void {
    if ('form' in changes || 'status' in changes) {
      this.syncValidators();
    }
  }

  isInvalid(controlName: ProcurementControlName): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  private syncValidators(): void {
    this.setControlState('priceA', this.status === TASK_STATUS.READY_FOR_REVIEW, [Validators.required]);
    this.setControlState('priceB', this.status === TASK_STATUS.READY_FOR_REVIEW, [Validators.required]);
    this.setControlState('receipt', this.status === TASK_STATUS.DONE, [Validators.required]);
  }

  private setControlState(controlName: ProcurementControlName, enabled: boolean, validators: ValidatorFn[]): void {
    const control = this.form.controls[controlName];
    if (enabled) {
      control.setValidators(validators);
    } else {
      control.setValue('');
      control.clearValidators();
      control.markAsPristine();
      control.markAsUntouched();
    }

    control.updateValueAndValidity({ emitEvent: false });
  }
}
