import '@angular/compiler';

import assert from 'node:assert/strict';
import test from 'node:test';

import { FormArray, FormControl, FormGroup } from '@angular/forms';

import type { TaskFieldRuleDto, TaskTypeSchemaDto } from './task.interfaces';
import {
  buildPayloadFromGroup,
  getApplicableFields,
  hydrateCustomFieldsGroup,
  rebuildCustomFieldsGroup,
  resolveFieldRule
} from './task-schema.utils';

function field(overrides: Partial<TaskFieldRuleDto>): TaskFieldRuleDto {
  return {
    field: 'field',
    type: 'string',
    required: false,
    ...overrides
  };
}

function schema(fields: readonly TaskFieldRuleDto[]): TaskTypeSchemaDto {
  return {
    taskType: 'Dynamic',
    isActive: true,
    version: 1,
    fields
  };
}

test('filters schema fields by inclusive status ranges', () => {
  const fields = [
    field({ field: 'first', appliesToStatus: 2 }),
    field({ field: 'second', appliesFromStatus: 2, appliesToStatus: 3 }),
    field({ field: 'third', appliesFromStatus: 4 })
  ];

  assert.deepEqual(
    getApplicableFields(schema(fields), 2).map((rule) => rule.field),
    ['first', 'second']
  );
  assert.deepEqual(
    getApplicableFields(schema(fields), 4).map((rule) => rule.field),
    ['third']
  );
});

test('rebuilds the custom-fields group from applicable schema rules', () => {
  const group: FormGroup = new FormGroup({
    stale: new FormControl('remove me')
  });

  const resolved = rebuildCustomFieldsGroup(
    group,
    schema([
      field({ field: 'notes', type: 'string', maxLength: 200, appliesToStatus: 2 }),
      field({ field: 'estimate', type: 'number', minValue: 1, appliesFromStatus: 2 }),
      field({ field: 'futureOnly', type: 'boolean', appliesFromStatus: 3 }),
      field({ field: '', type: 'string', required: true })
    ]),
    2
  );

  assert.deepEqual(Object.keys(group.controls), ['notes', 'estimate']);
  assert.deepEqual(
    resolved.map((entry) => [entry.rule.field, entry.kind]),
    [
      ['notes', 'textarea'],
      ['estimate', 'number']
    ]
  );
  assert.equal(group.get('stale'), null);
});

test('hydrates schema-generated controls with backend data using safe scalar coercion', () => {
  const group: FormGroup = new FormGroup({});
  const resolved = rebuildCustomFieldsGroup(
    group,
    schema([
      field({ field: 'estimate', type: 'number' }),
      field({ field: 'approved', type: 'boolean' }),
      field({ field: 'scores', type: 'array', elementType: 'number', arrayLength: 3 }),
      field({ field: 'summary', type: 'string' })
    ]),
    1
  );

  hydrateCustomFieldsGroup(group, resolved, {
    estimate: '8',
    approved: 'true',
    scores: ['1', 'not-a-number', 3],
    summary: 42
  });

  const scores = group.get('scores');
  assert.ok(scores instanceof FormArray);
  assert.equal(group.get('estimate')?.value, 8);
  assert.equal(group.get('approved')?.value, false);
  assert.deepEqual(scores.controls.map((control) => control.value), [1, null, 3]);
  assert.equal(group.get('summary')?.value, '42');
  assert.equal(group.pristine, true);
  assert.equal(group.untouched, true);
});

test('builds workflow payloads with schema-aware scalar coercion', () => {
  const group: FormGroup = new FormGroup({});
  const resolved = rebuildCustomFieldsGroup(
    group,
    schema([
      field({ field: 'estimate', type: 'number' }),
      field({ field: 'approved', type: 'boolean' }),
      field({ field: 'scores', type: 'array', elementType: 'number', arrayLength: 3 }),
      field({ field: 'summary', type: 'string' })
    ]),
    1
  );

  group.get('estimate')?.setValue('12.5');
  group.get('approved')?.setValue(true);

  const scores = group.get('scores');
  assert.ok(scores instanceof FormArray);
  scores.at(0).setValue('4');
  scores.at(1).setValue('');
  scores.at(2).setValue('not-a-number');

  group.get('summary')?.setValue(false);

  assert.deepEqual(buildPayloadFromGroup(group, resolved), {
    estimate: 12.5,
    approved: true,
    scores: [4, null, 'not-a-number'],
    summary: 'false'
  });
});

test('resolves array scalar controls from min item constraints when no fixed length is provided', () => {
  assert.deepEqual(resolveFieldRule(field({ field: 'serials', type: 'array', minItems: 2 })), {
    rule: field({ field: 'serials', type: 'array', minItems: 2 }),
    kind: 'array-scalar',
    itemCount: 2
  });
});
