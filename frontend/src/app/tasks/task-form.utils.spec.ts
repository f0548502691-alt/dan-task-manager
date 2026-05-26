import test from 'node:test';
import assert from 'node:assert/strict';
import { parseTaskCustomDataJson, syncControlState } from './task-form.utils';

test('parseTaskCustomDataJson accepts only JSON objects as workflow custom data', () => {
  assert.deepEqual(parseTaskCustomDataJson('  '), { data: {}, isValid: true });
  assert.deepEqual(parseTaskCustomDataJson('{"risk":"low","count":2}'), {
    data: { risk: 'low', count: 2 },
    isValid: true
  });
  assert.deepEqual(parseTaskCustomDataJson('[1,2]'), { data: {}, isValid: true });
  assert.deepEqual(parseTaskCustomDataJson('{"risk":'), { data: {}, isValid: false });
});

test('syncControlState enables validators without clearing existing field state', () => {
  const validator = () => null;
  const control = createControlFake('keep-me');

  syncControlState(control, true, [validator]);

  assert.equal(control.value, 'keep-me');
  assert.equal(control.setValidatorsCalls, 1);
  assert.deepEqual(control.validators, [validator]);
  assert.equal(control.clearValidatorsCalls, 0);
  assert.equal(control.setValueCalls, 0);
  assert.deepEqual(control.validityUpdateOptions, [{ emitEvent: false }]);
});

test('syncControlState disables controls by resetting values, errors, and validators silently', () => {
  const control = createControlFake('stale');

  syncControlState(control, false, []);

  assert.equal(control.value, '');
  assert.equal(control.clearValidatorsCalls, 1);
  assert.equal(control.setValueCalls, 1);
  assert.equal(control.errors, null);
  assert.equal(control.markAsPristineCalls, 1);
  assert.equal(control.markAsUntouchedCalls, 1);
  assert.deepEqual(control.setValueOptions, [{ emitEvent: false }]);
  assert.deepEqual(control.validityUpdateOptions, [{ emitEvent: false }]);
});

function createControlFake(initialValue: unknown) {
  return {
    value: initialValue,
    validators: [] as unknown[],
    errors: { stale: true } as Record<string, unknown> | null,
    setValidatorsCalls: 0,
    clearValidatorsCalls: 0,
    setValueCalls: 0,
    markAsPristineCalls: 0,
    markAsUntouchedCalls: 0,
    setValueOptions: [] as unknown[],
    validityUpdateOptions: [] as unknown[],
    setValidators(validators: unknown[]) {
      this.validators = validators;
      this.setValidatorsCalls += 1;
    },
    clearValidators() {
      this.validators = [];
      this.clearValidatorsCalls += 1;
    },
    updateValueAndValidity(options?: unknown) {
      this.validityUpdateOptions.push(options);
    },
    setValue(value: unknown, options?: unknown) {
      this.value = value;
      this.setValueOptions.push(options);
      this.setValueCalls += 1;
    },
    setErrors(errors: Record<string, unknown> | null) {
      this.errors = errors;
    },
    markAsPristine() {
      this.markAsPristineCalls += 1;
    },
    markAsUntouched() {
      this.markAsUntouchedCalls += 1;
    }
  };
}
