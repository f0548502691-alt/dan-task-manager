import * as assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import {
  getDropdownStatusLabel,
  getFinalStatus,
  getTaskStatusLabel
} from './task-status-labels';
import { TASK_STATUS, TaskTypeSchemaDto } from './task.interfaces';

const schema = {
  taskType: 'Custom',
  finalStatus: 4,
  isActive: true,
  version: 1,
  fields: [
    {
      field: 'managerReview',
      type: 'string',
      required: true,
      appliesFromStatus: 2,
      appliesToStatus: 2
    },
    {
      field: 'launch_date',
      type: 'string',
      required: true,
      appliesFromStatus: 2,
      appliesToStatus: 3
    },
    {
      field: 'post-launch-check',
      type: 'string',
      required: true,
      appliesFromStatus: 3,
      appliesToStatus: 3
    }
  ]
} satisfies TaskTypeSchemaDto;

describe('task status labels', () => {
  it('uses stable labels for default statuses', () => {
    assert.equal(getTaskStatusLabel(TASK_STATUS.CREATED, 'Unknown'), 'Created');
    assert.equal(getTaskStatusLabel(TASK_STATUS.CLOSED, 'Unknown'), 'Closed');
  });

  it('matches task-type-specific labels case-insensitively before schema labels', () => {
    assert.equal(getTaskStatusLabel(3, ' development ', schema), 'Branch ready');
  });

  it('falls back to applicable schema field labels for custom statuses', () => {
    assert.equal(getTaskStatusLabel(2, 'Custom', schema), 'Manager Review + Launch date');
    assert.equal(getTaskStatusLabel(3, 'Custom', schema), 'Launch date + Post launch check');
  });

  it('uses numeric and ready-to-close fallbacks only when no label exists', () => {
    assert.equal(getDropdownStatusLabel(4, 'Custom', 4, schema), 'Ready to close');
    assert.equal(getDropdownStatusLabel(3, 'Custom', 4, schema), 'Launch date + Post launch check');
    assert.equal(getDropdownStatusLabel(2, 'Development', 4, schema), 'Specification ready');
    assert.equal(getTaskStatusLabel(7, 'Custom', schema), 'Status 7');
  });

  it('uses schema final statuses when available and otherwise preserves the current status fallback', () => {
    assert.equal(getFinalStatus(schema, 2), 4);
    assert.equal(getFinalStatus(null, 2), 2);
  });
});
