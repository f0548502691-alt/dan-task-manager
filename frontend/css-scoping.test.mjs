import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = dirname(fileURLToPath(import.meta.url));

function readFrontendFile(...segments) {
  return readFileSync(join(frontendRoot, ...segments), 'utf8');
}

test('dynamic task fields own their component-scoped layout styles', () => {
  const component = readFrontendFile('src/app/tasks/dynamic-task-fields.component.ts');
  const template = readFrontendFile('src/app/tasks/dynamic-task-fields.component.html');
  const styles = readFrontendFile('src/app/tasks/dynamic-task-fields.component.css');

  assert.match(
    component,
    /styleUrl:\s*['"]\.\/dynamic-task-fields\.component\.css['"]/,
    'the dynamic field component must load the stylesheet that scopes its own layout rules'
  );
  assert.match(template, /class="dynamic-fields"/);
  assert.match(template, /class="dynamic-fields__checkbox-row"/);
  assert.match(styles, /^\.dynamic-fields\s*\{/m);
  assert.match(styles, /^\.dynamic-fields__checkbox-row\s*\{/m);
  assert.match(styles, /^\.dynamic-fields__checkbox-row input\s*\{/m);
});

test('shared form control styles are global instead of scoped to the workflow board', () => {
  const angularConfig = JSON.parse(readFrontendFile('angular.json'));
  const globalStyles = readFrontendFile('src/styles.css');
  const workflowStyles = readFrontendFile('src/app/tasks/task-workflow-board.component.css');

  assert.deepEqual(
    angularConfig.projects.frontend.architect.build.options.styles,
    ['src/styles.css'],
    'global styles must be included in the Angular build'
  );

  assert.match(globalStyles, /^label\s*\{\s*\n\s*font-weight:\s*600;\s*\n\}/m);
  assert.match(globalStyles, /^input,\s*\ntextarea,\s*\nselect,\s*\nbutton\s*\{\s*\n\s*font:\s*inherit;\s*\n\}/m);
  assert.match(globalStyles, /^input,\s*\ntextarea,\s*\nselect\s*\{/m);
  assert.match(globalStyles, /^small\s*\{\s*\n\s*color:\s*#b00020;\s*\n\}/m);

  assert.doesNotMatch(workflowStyles, /^label\s*\{/m);
  assert.doesNotMatch(workflowStyles, /^input,\s*\ntextarea,\s*\nselect,\s*\nbutton\s*\{/m);
  assert.doesNotMatch(workflowStyles, /^input,\s*\ntextarea,\s*\nselect\s*\{/m);
  assert.doesNotMatch(workflowStyles, /^small\s*\{/m);
});
