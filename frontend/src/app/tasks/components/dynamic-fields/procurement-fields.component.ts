import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TASK_DYNAMIC_FIELDS_CONTEXT } from './task-dynamic-fields-context';

@Component({
  selector: 'app-procurement-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './procurement-fields.component.html'
})
export class ProcurementFieldsComponent implements OnInit {
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
    this.addTextControl('supplierName');
    this.addTextControl('requesterName');
    this.addNumberControl('estimatedCost');
    this.addTextControl('quoteNumber');
    this.addTextControl('purchaseOrderNumber');
    this.addTextControl('deliveryDate');
  }

  private patchInitialValues(): void {
    this.detailsGroup.patchValue(
      {
        supplierName: this.toText(this.context.initialData['supplierName']),
        requesterName: this.toText(this.context.initialData['requesterName']),
        estimatedCost: this.toNumber(this.context.initialData['estimatedCost']),
        quoteNumber: this.toText(this.context.initialData['quoteNumber']),
        purchaseOrderNumber: this.toText(this.context.initialData['purchaseOrderNumber']),
        deliveryDate: this.toText(this.context.initialData['deliveryDate'])
      },
      { emitEvent: false }
    );
  }

  private updateValidatorsForStatus(newStatus: number): void {
    this.setRequired('supplierName', newStatus >= 0);
    this.setRequired('requesterName', newStatus >= 0);
    this.setRequired('estimatedCost', newStatus >= 1);
    this.setRequired('quoteNumber', newStatus >= 1);
    this.setRequired('purchaseOrderNumber', newStatus >= 2);
    this.setRequired('deliveryDate', newStatus >= 3);
  }

  private addTextControl(name: string): void {
    if (!this.detailsGroup.contains(name)) {
      this.detailsGroup.addControl(name, new FormControl<string>('', { nonNullable: true }));
    }
  }

  private addNumberControl(name: string): void {
    if (!this.detailsGroup.contains(name)) {
      this.detailsGroup.addControl(name, new FormControl<number | null>(null));
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

  private toNumber(value: unknown): number | null {
    return typeof value === 'number' ? value : null;
  }
}
