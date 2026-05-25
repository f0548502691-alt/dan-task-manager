import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TASK_DYNAMIC_FIELDS_CONTEXT } from './task-dynamic-fields-context';

@Component({
  selector: 'app-development-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './development-fields.component.html'
})
export class DevelopmentFieldsComponent implements OnInit {
  private readonly context = inject(TASK_DYNAMIC_FIELDS_CONTEXT);
  private readonly destroyRef = inject(DestroyRef);

  readonly detailsGroup = this.context.detailsGroup;
  readonly statusControl = this.context.newStatusControl;

  ngOnInit(): void {
    this.ensureControls();
    this.patchInitialValues();
    this.updateValidatorsForStatus(this.statusControl.value);

    this.statusControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((status) => {
      this.updateValidatorsForStatus(status ?? 0);
    });
  }

  get newStatus(): number {
    return this.statusControl.value ?? 0;
  }

  private ensureControls(): void {
    this.addTextControl('featureBranch');
    this.addTextControl('pullRequestUrl');
    this.addTextControl('qaOwner');
    this.addTextControl('releaseNotes');
  }

  private patchInitialValues(): void {
    this.detailsGroup.patchValue(
      {
        featureBranch: this.toText(this.context.initialData['featureBranch']),
        pullRequestUrl: this.toText(this.context.initialData['pullRequestUrl']),
        qaOwner: this.toText(this.context.initialData['qaOwner']),
        releaseNotes: this.toText(this.context.initialData['releaseNotes'])
      },
      { emitEvent: false }
    );
  }

  private updateValidatorsForStatus(newStatus: number): void {
    this.setRequired('featureBranch', newStatus >= 0);
    this.setRequired('pullRequestUrl', newStatus >= 1);
    this.setRequired('qaOwner', newStatus >= 2);
    this.setRequired('releaseNotes', newStatus >= 3);
  }

  private addTextControl(name: string): void {
    if (!this.detailsGroup.contains(name)) {
      this.detailsGroup.addControl(name, new FormControl<string>('', { nonNullable: true }));
    }
  }

  private setRequired(controlName: string, isRequired: boolean): void {
    const control = this.detailsGroup.get(controlName);
    if (!control) {
      return;
    }

    control.setValidators(isRequired ? [Validators.required] : []);
    control.updateValueAndValidity({ emitEvent: false });
  }

  private toText(value: unknown): string {
    return typeof value === 'string' ? value : '';
  }
}
