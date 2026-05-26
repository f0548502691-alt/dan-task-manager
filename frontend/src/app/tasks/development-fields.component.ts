import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TASK_STATUS } from './task.interfaces';
import { syncControlState } from './task-form.utils';

@Component({
  selector: 'app-development-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './development-fields.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DevelopmentFieldsComponent implements OnChanges {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) status!: number;
  readonly TASK_STATUS = TASK_STATUS;

  ngOnChanges(changes: SimpleChanges): void {
    if ('form' in changes || 'status' in changes) {
      this.syncValidators();
    }
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  private syncValidators(): void {
    syncControlState(this.form.get('specification'), this.status === TASK_STATUS.READY_FOR_REVIEW, [
      Validators.required,
      Validators.minLength(10)
    ]);
    syncControlState(this.form.get('branchName'), this.status === TASK_STATUS.DONE, [
      Validators.required,
      Validators.pattern(/^\S+$/)
    ]);
    syncControlState(this.form.get('versionNumber'), this.status === TASK_STATUS.RELEASED, [Validators.required]);
  }
}
