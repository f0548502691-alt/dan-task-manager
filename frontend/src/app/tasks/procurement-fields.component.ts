import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TASK_STATUS } from './task.interfaces';
import { syncControlState } from './task-form.utils';

@Component({
  selector: 'app-procurement-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './procurement-fields.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProcurementFieldsComponent implements OnChanges {
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
    syncControlState(this.form.get('priceA'), this.status === TASK_STATUS.STATUS_2, [Validators.required]);
    syncControlState(this.form.get('priceB'), this.status === TASK_STATUS.STATUS_2, [Validators.required]);
    syncControlState(this.form.get('receipt'), this.status === TASK_STATUS.STATUS_3, [Validators.required]);
  }
}
