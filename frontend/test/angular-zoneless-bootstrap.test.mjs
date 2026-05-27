import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

async function readJson(relativePath) {
  return JSON.parse(await readFile(path.join(frontendRoot, relativePath), 'utf8'));
}

async function parseTypeScript(relativePath) {
  const sourceText = await readFile(path.join(frontendRoot, relativePath), 'utf8');

  return ts.createSourceFile(relativePath, sourceText, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
}

function importedNamesFrom(sourceFile, moduleSpecifier) {
  const names = new Set();

  for (const statement of sourceFile.statements) {
    if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)) {
      continue;
    }

    if (statement.moduleSpecifier.text !== moduleSpecifier) {
      continue;
    }

    const namedBindings = statement.importClause?.namedBindings;
    if (!namedBindings || !ts.isNamedImports(namedBindings)) {
      continue;
    }

    for (const element of namedBindings.elements) {
      names.add(element.name.text);
    }
  }

  return names;
}

function hasImportFrom(sourceFile, moduleSpecifier) {
  return sourceFile.statements.some(
    (statement) =>
      ts.isImportDeclaration(statement) &&
      ts.isStringLiteral(statement.moduleSpecifier) &&
      statement.moduleSpecifier.text === moduleSpecifier
  );
}

function bootstrapApplicationCall(sourceFile) {
  let bootstrapCall;

  function visit(node) {
    if (
      ts.isCallExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === 'bootstrapApplication'
    ) {
      bootstrapCall = node;
      return;
    }

    ts.forEachChild(node, visit);
  }

  visit(sourceFile);
  return bootstrapCall;
}

function bootstrapProviderCalls(callExpression) {
  const optionsArgument = callExpression.arguments[1];

  assert.ok(optionsArgument && ts.isObjectLiteralExpression(optionsArgument), 'bootstrap options object is required');

  const providersProperty = optionsArgument.properties.find(
    (property) =>
      ts.isPropertyAssignment(property) &&
      ts.isIdentifier(property.name) &&
      property.name.text === 'providers'
  );

  assert.ok(providersProperty && ts.isPropertyAssignment(providersProperty), 'providers array is required');
  assert.ok(ts.isArrayLiteralExpression(providersProperty.initializer), 'providers must be an array literal');

  return providersProperty.initializer.elements
    .filter(ts.isCallExpression)
    .map((providerCall) => (ts.isIdentifier(providerCall.expression) ? providerCall.expression.text : undefined))
    .filter(Boolean);
}

test('Angular build configuration does not load Zone.js polyfills', async () => {
  const angularConfig = await readJson('angular.json');
  const buildOptions = angularConfig.projects.frontend.architect.build.options;

  assert.deepEqual(buildOptions.polyfills, []);
});

test('frontend dependencies do not reintroduce zone-backed bootstrap packages', async () => {
  const packageJson = await readJson('package.json');

  assert.equal(packageJson.dependencies['zone.js'], undefined);
  assert.equal(packageJson.dependencies['@angular/platform-browser-dynamic'], undefined);
});

test('main.ts bootstraps the standalone app with zoneless change detection', async () => {
  const sourceFile = await parseTypeScript('src/main.ts');
  const angularCoreImports = importedNamesFrom(sourceFile, '@angular/core');
  const platformBrowserImports = importedNamesFrom(sourceFile, '@angular/platform-browser');
  const commonHttpImports = importedNamesFrom(sourceFile, '@angular/common/http');
  const bootstrapCall = bootstrapApplicationCall(sourceFile);

  assert.equal(hasImportFrom(sourceFile, '@angular/platform-browser-dynamic'), false);
  assert.ok(angularCoreImports.has('provideZonelessChangeDetection'));
  assert.ok(platformBrowserImports.has('bootstrapApplication'));
  assert.ok(commonHttpImports.has('provideHttpClient'));
  assert.ok(bootstrapCall, 'bootstrapApplication must be called from main.ts');
  assert.equal(bootstrapCall.arguments[0].getText(sourceFile), 'AppComponent');

  assert.deepEqual(bootstrapProviderCalls(bootstrapCall), ['provideHttpClient', 'provideZonelessChangeDetection']);
});
