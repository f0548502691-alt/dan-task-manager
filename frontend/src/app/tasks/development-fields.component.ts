import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';

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

  ngOnChanges(changes: SimpleChanges): void {
    if ('form' in changes || 'status' in changes) {
      this.ensureControls();
      this.syncValidators();
    }
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  private ensureControls(): void {
    this.addControlIfMissing('specification');
    this.addControlIfMissing('branchName');
    this.addControlIfMissing('versionNumber');
  }

  private syncValidators(): void {
    this.setControlState('specification', this.status === 2, [Validators.required, Validators.minLength(10)]);
    this.setControlState('branchName', this.status === 3, [Validators.required, Validators.pattern(/^\S+$/)]);
    this.setControlState('versionNumber', this.status === 4, [Validators.required]);
  }

  private addControlIfMissing(controlName: string): void {
    if (!this.form.contains(controlName)) {
      this.form.addControl(controlName, new FormControl<string>(''));
    }
  }

  private setControlState(controlName: string, enabled: boolean, validators: ValidatorFn[]): void {
    const control = this.form.get(controlName);
    if (!control) {
      return;
    }

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
