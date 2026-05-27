import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const readJson = async (path) => JSON.parse(await readFile(new URL(path, import.meta.url), 'utf8'));
const readText = async (path) => readFile(new URL(path, import.meta.url), 'utf8');

test('frontend package exposes real scaffold commands', async () => {
  const packageJson = await readJson('../package.json');

  assert.equal(packageJson.scripts.start, 'ng serve --proxy-config proxy.conf.json');
  assert.equal(packageJson.scripts.build, 'ng build');
  assert.match(packageJson.scripts.test, /^node --test\b/);
});

test('Angular workspace builds the expected browser entrypoint', async () => {
  const angularJson = await readJson('../angular.json');
  const project = angularJson.projects.frontend;
  const buildOptions = project.architect.build.options;
  const serveTarget = project.architect.serve;

  assert.equal(project.projectType, 'application');
  assert.equal(project.root, '');
  assert.equal(project.sourceRoot, 'src');
  assert.equal(buildOptions.browser, 'src/main.ts');
  assert.equal(buildOptions.index, 'src/index.html');
  assert.equal(buildOptions.tsConfig, 'tsconfig.app.json');
  assert.deepEqual(buildOptions.styles, ['src/styles.css']);
  assert.equal(serveTarget.defaultConfiguration, 'development');
  assert.equal(serveTarget.configurations.development.buildTarget, 'frontend:build:development');
});

test('root component mounts the task workflow board', async () => {
  const appComponent = await readText('../src/app/app.component.ts');
  const indexHtml = await readText('../src/index.html');
  const mainTs = await readText('../src/main.ts');

  assert.match(appComponent, /selector:\s*'app-root'/);
  assert.match(appComponent, /imports:\s*\[\s*TaskWorkflowBoardComponent\s*\]/);
  assert.match(appComponent, /<app-task-workflow-board><\/app-task-workflow-board>/);
  assert.match(indexHtml, /<app-root><\/app-root>/);
  assert.match(mainTs, /bootstrapApplication\(AppComponent/);
  assert.match(mainTs, /provideHttpClient\(\)/);
});
