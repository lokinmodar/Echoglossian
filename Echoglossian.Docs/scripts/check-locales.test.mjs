import assert from 'node:assert/strict';
import test from 'node:test';

import {
  buildConfiguredLocales,
  collectResourceCultures,
  validateLocales,
} from './check-locales.mjs';

test('collectResourceCultures maps neutral and regional resources', () => {
  const cultures = collectResourceCultures([
    'Resources.resx',
    'Resources.pt-PT.resx',
    'Resources.pt-BR.resx',
    'Resources.ru-RU.resx',
  ]);

  assert.deepEqual(cultures, ['pt', 'pt-br', 'root', 'ru']);
});

test('buildConfiguredLocales reflects the shared site locale list', () => {
  assert.deepEqual(buildConfiguredLocales(), [
    'da',
    'de',
    'el',
    'es',
    'eu',
    'fr',
    'it',
    'pt',
    'pt-br',
    'root',
    'ru',
  ]);
});

test('validateLocales reports missing and unexpected locales separately', () => {
  const result = validateLocales(['pt-br', 'root', 'ru'], ['root', 'ru', 'vi']);

  assert.deepEqual(result.missingInConfig, ['pt-br']);
  assert.deepEqual(result.unexpectedInConfig, ['vi']);
});
