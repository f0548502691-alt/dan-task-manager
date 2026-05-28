const assert = require('node:assert/strict');
const test = require('node:test');

const { TASK_STATUS } = require('./out-tsc/spec/app/tasks/task.interfaces.js');
const {
  buildStatusOptions,
  getStatusLabel
} = require('./out-tsc/spec/app/tasks/task-status-options.utils.js');

test('labels a two-state workflow final status as CLOSED in the dropdown', () => {
  assert.deepStrictEqual(buildStatusOptions(TASK_STATUS.CREATED, 2), [
    { value: TASK_STATUS.CREATED, label: 'CREATED' },
    { value: 2, label: 'CLOSED' }
  ]);
});

test('uses numbered fallback labels for intermediate multi-step statuses', () => {
  assert.deepStrictEqual(buildStatusOptions(TASK_STATUS.CREATED, 4), [
    { value: TASK_STATUS.CREATED, label: 'CREATED' },
    { value: 2, label: 'Status 2' },
    { value: 3, label: 'Status 3' },
    { value: 4, label: 'Status 4' }
  ]);
});

test('keeps closed tasks pinned to the canonical CLOSED option', () => {
  assert.deepStrictEqual(buildStatusOptions(TASK_STATUS.CLOSED, TASK_STATUS.CLOSED), [
    { value: TASK_STATUS.CLOSED, label: 'CLOSED' }
  ]);
});

test('resolves default and fallback status labels consistently', () => {
  assert.equal(getStatusLabel(TASK_STATUS.CREATED), 'CREATED');
  assert.equal(getStatusLabel(TASK_STATUS.CLOSED), 'CLOSED');
  assert.equal(getStatusLabel(42), 'Status 42');
});
