declare module '@angular/forms' {
  export type ValidatorFn = (control: AbstractControl) => unknown;

  export interface AbstractControl {
    setValidators(validators: ValidatorFn[]): void;
    clearValidators(): void;
    updateValueAndValidity(options?: { emitEvent?: boolean }): void;
    setValue(value: unknown, options?: { emitEvent?: boolean }): void;
    setErrors(errors: Record<string, unknown> | null): void;
    markAsPristine(): void;
    markAsUntouched(): void;
  }

  export interface FormControlLike {
    value: unknown;
  }

  export interface FormGroup {
    controls: Record<string, FormControlLike>;
    patchValue(value: Record<string, unknown>, options?: { emitEvent?: boolean }): void;
  }
}
