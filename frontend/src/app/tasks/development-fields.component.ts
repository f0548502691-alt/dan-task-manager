import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { TASK_STATUS } from './task.interfaces';
import { TaskWorkflowForm } from './task-workflow-form.types';

type DevelopmentControlName = 'specification' | 'branchName' | 'versionNumber';

@Component({
  selector: 'app-development-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './development-fields.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DevelopmentFieldsComponent implements OnChanges {
  @Input({ required: true }) form!: TaskWorkflowForm;
  @Input({ required: true }) status!: number;

  ngOnChanges(changes: SimpleChanges): void {
    if ('form' in changes || 'status' in changes) {
      this.syncValidators();
    }
  }

  isInvalid(controlName: DevelopmentControlName): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  private syncValidators(): void {
    this.setControlState('specification', this.status === TASK_STATUS.READY_FOR_REVIEW, [
      Validators.required,
      Validators.minLength(10)
    ]);
    this.setControlState('branchName', this.status === TASK_STATUS.DONE, [
      Validators.required,
      Validators.pattern(/^\S+$/)
    ]);
    this.setControlState('versionNumber', this.status === TASK_STATUS.RELEASED, [Validators.required]);
  }

  private setControlState(controlName: DevelopmentControlName, enabled: boolean, validators: ValidatorFn[]): void {
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
