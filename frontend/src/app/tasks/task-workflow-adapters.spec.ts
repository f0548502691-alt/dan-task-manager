import test from 'node:test';
import assert from 'node:assert/strict';
import { TASK_STATUS } from './task.interfaces';
import { getTaskWorkflowAdapter } from './task-workflow-adapters';

test('procurement adapter hydrates only supported string fields from custom data', () => {
  const adapter = getTaskWorkflowAdapter('Procurement');
  const form = createFormFake({
    priceA: 'old-a',
    priceB: 'old-b',
    receipt: 'old-receipt'
  });

  adapter?.hydrate(form, {
    prices: ['5000', 4800],
    receipt: 'REC-001'
  });

  assert.deepEqual(form.values(), {
    priceA: '5000',
    priceB: '',
    receipt: 'REC-001'
  });
  assert.deepEqual(form.patchOptions, [{ emitEvent: false }]);
});

test('procurement adapter builds status-specific payloads required by backend workflow rules', () => {
  const adapter = getTaskWorkflowAdapter('Procurement');
  const form = createFormFake({
    priceA: '5000',
    priceB: '4800',
    receipt: 'REC-001'
  });

  assert.deepEqual(adapter?.buildPayload(form, TASK_STATUS.READY_FOR_REVIEW), {
    prices: ['5000', '4800']
  });
  assert.deepEqual(adapter?.buildPayload(form, TASK_STATUS.DONE), {
    receipt: 'REC-001'
  });
  assert.deepEqual(adapter?.buildPayload(form, TASK_STATUS.IN_PROGRESS), {});
});

test('development adapter hydrates mixed custom data without leaking invalid values', () => {
  const adapter = getTaskWorkflowAdapter('Development');
  const form = createFormFake({
    specification: '',
    branchName: '',
    versionNumber: ''
  });

  adapter?.hydrate(form, {
    specification: 'Build checkout',
    branchName: 123,
    versionNumber: 42
  });

  assert.deepEqual(form.values(), {
    specification: 'Build checkout',
    branchName: '',
    versionNumber: '42'
  });
});

test('development adapter builds payloads for each configured development transition', () => {
  const adapter = getTaskWorkflowAdapter('Development');
  const form = createFormFake({
    specification: 'Build checkout flow',
    branchName: 'feature/checkout',
    versionNumber: '1.2.3'
  });

  assert.deepEqual(adapter?.buildPayload(form, TASK_STATUS.READY_FOR_REVIEW), {
    specification: 'Build checkout flow'
  });
  assert.deepEqual(adapter?.buildPayload(form, TASK_STATUS.DONE), {
    branchName: 'feature/checkout'
  });
  assert.deepEqual(adapter?.buildPayload(form, TASK_STATUS.RELEASED), {
    versionNumber: '1.2.3'
  });
});

test('unknown task types do not use a specialized workflow adapter', () => {
  assert.equal(getTaskWorkflowAdapter('Custom'), undefined);
});

function createFormFake(initialValues: Record<string, unknown>) {
  const controls = Object.fromEntries(
    Object.entries(initialValues).map(([name, value]) => [name, { value }])
  );

  return {
    controls,
    patchOptions: [] as unknown[],
    patchValue(values: Record<string, unknown>, options?: unknown) {
      for (const [name, value] of Object.entries(values)) {
        if (!this.controls[name]) {
          this.controls[name] = { value: '' };
        }
        this.controls[name].value = value;
      }
      this.patchOptions.push(options);
    },
    values() {
      return Object.fromEntries(Object.entries(this.controls).map(([name, control]) => [name, control.value]));
    }
  };
}
