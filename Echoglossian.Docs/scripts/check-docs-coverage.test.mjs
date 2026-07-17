import assert from 'node:assert/strict';
import test from 'node:test';

import {
  buildExpectedLocalizedFiles,
  translatedLocales,
  validateLocalizedCoverage,
} from './check-docs-coverage.mjs';

test('translatedLocales excludes the root locale and keeps configured order', () => {
  assert.deepEqual(translatedLocales(), [
    'da',
    'de',
    'el',
    'es',
    'eu',
    'fr',
    'it',
    'pt',
    'pt-br',
    'ru',
  ]);
});

test('buildExpectedLocalizedFiles expands all published user pages for requested locales', () => {
  assert.deepEqual(
    buildExpectedLocalizedFiles(['da'], ['users']),
    [
      'da/users/index.mdx',
      'da/users/overview.mdx',
      'da/users/installation.mdx',
      'da/users/configuration.mdx',
      'da/users/translation-modes.mdx',
      'da/users/support-matrix.mdx',
      'da/users/troubleshooting.mdx',
    ],
  );
});

test('validateLocalizedCoverage reports missing localized files', () => {
  const result = validateLocalizedCoverage(
    ['da/index.mdx', 'da/users/index.mdx'],
    new Set(['da/index.mdx']),
  );

  assert.deepEqual(result, {
    missingFiles: ['da/users/index.mdx'],
  });
});
