import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const frontendDir = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(frontendDir, '..');

function readWorkspaceFile(relativePath) {
  return readFileSync(path.join(rootDir, relativePath), 'utf8');
}

function readJson(relativePath) {
  return JSON.parse(readWorkspaceFile(relativePath));
}

function getYamlBlock(yaml, sectionName) {
  const lines = yaml.split(/\r?\n/);
  const start = lines.findIndex((line) => line.trim() === `${sectionName}:`);

  assert.notEqual(start, -1, `Expected docker-compose.yml to contain ${sectionName}:`);

  const indent = lines[start].match(/^\s*/)[0].length;
  const block = [];

  for (const line of lines.slice(start + 1)) {
    if (line.trim() === '') {
      block.push(line);
      continue;
    }

    const currentIndent = line.match(/^\s*/)[0].length;
    if (currentIndent <= indent) {
      break;
    }

    block.push(line);
  }

  return block.join('\n');
}

function findTrimmedLine(lines, expectedLine) {
  const index = lines.findIndex((line) => line.trim() === expectedLine);
  assert.notEqual(index, -1, `Expected to find line: ${expectedLine}`);
  return index;
}

test('docker compose publishes the frontend dev server and starts it after the backend', () => {
  const compose = readWorkspaceFile('docker-compose.yml');
  const frontendService = getYamlBlock(compose, 'frontend');

  assert.match(
    frontendService,
    /build:\n\s+context: \.\/frontend\n\s+dockerfile: Dockerfile/,
  );
  assert.match(frontendService, /ports:\n\s+- "4200:4200"/);
  assert.match(frontendService, /depends_on:\n\s+- backend/);
});

test('frontend Dockerfile startup command stays aligned with the package script', () => {
  const dockerfile = readWorkspaceFile('frontend/Dockerfile');
  const dockerfileLines = dockerfile.split(/\r?\n/);
  const packageJson = readJson('frontend/package.json');

  assert.equal(
    packageJson.scripts['start:docker'],
    'ng serve --host 0.0.0.0 --port 4200 --proxy-config proxy.docker.conf.json',
  );
  assert.match(dockerfile, /EXPOSE 4200/);
  assert.match(dockerfile, /CMD \["npm", "run", "start:docker"\]/);

  const copyPackageIndex = findTrimmedLine(dockerfileLines, 'COPY package*.json ./');
  const npmCiIndex = findTrimmedLine(dockerfileLines, 'RUN npm ci');
  const copySourceIndex = findTrimmedLine(dockerfileLines, 'COPY . .');

  assert.ok(
    copyPackageIndex < npmCiIndex && npmCiIndex < copySourceIndex,
    'Expected npm ci to run after copying package files and before copying the full frontend source.',
  );
});

test('docker proxy sends API traffic to the backend service name inside compose', () => {
  const proxy = readJson('frontend/proxy.docker.conf.json');
  const compose = readWorkspaceFile('docker-compose.yml');
  const backendService = getYamlBlock(compose, 'backend');

  assert.deepEqual(Object.keys(proxy), ['/api']);
  assert.equal(proxy['/api'].target, 'http://backend:8080');
  assert.equal(proxy['/api'].secure, false);
  assert.equal(proxy['/api'].changeOrigin, true);
  assert.match(backendService, /ports:\n\s+- "8080:8080"/);
});

test('frontend docker context excludes generated dependencies and build output', () => {
  const dockerignoreEntries = readWorkspaceFile('frontend/.dockerignore')
    .split(/\r?\n/)
    .map((entry) => entry.trim())
    .filter(Boolean);

  assert.ok(dockerignoreEntries.includes('node_modules'));
  assert.ok(dockerignoreEntries.includes('dist'));
  assert.ok(dockerignoreEntries.includes('.angular'));
});
