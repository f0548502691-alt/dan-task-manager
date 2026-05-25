import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { TASK_DYNAMIC_FIELDS_CONTEXT } from './task-dynamic-fields-context';

@Component({
  selector: 'app-default-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './default-fields.component.html'
})
export class DefaultFieldsComponent implements OnInit {
  private readonly context = inject(TASK_DYNAMIC_FIELDS_CONTEXT);

  readonly detailsGroup = this.context.detailsGroup;

  ngOnInit(): void {
    if (!this.detailsGroup.contains('notes')) {
      this.detailsGroup.addControl('notes', new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }));
    }

    this.detailsGroup.patchValue(
      {
        notes: typeof this.context.initialData['notes'] === 'string' ? this.context.initialData['notes'] : ''
      },
      { emitEvent: false }
    );
  }
}
