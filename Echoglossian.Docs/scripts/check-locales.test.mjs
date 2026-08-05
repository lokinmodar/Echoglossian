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

test('collectResourceCultures folds English regional resources into the root locale', () => {
  const cultures = collectResourceCultures([
    'Resources.resx',
    'Resources.en-US.resx',
    'Resources.en-GB.resx',
  ]);

  assert.deepEqual(cultures, ['root']);
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

test('validateLocales reports unpublished plugin locales separately from invalid docs locales', () => {
  const result = validateLocales(['pt-br', 'root', 'ru'], ['root', 'ru', 'vi']);

  assert.deepEqual(result.unpublishedResourceLocales, ['pt-br']);
  assert.deepEqual(result.configuredWithoutResourceLocales, ['vi']);
});

test('validateLocales allows plugin locales that are not published by docs yet', () => {
  const result = validateLocales(
    ['ca', 'de', 'nl', 'root'],
    ['de', 'root'],
  );

  assert.deepEqual(result.unpublishedResourceLocales, ['ca', 'nl']);
  assert.deepEqual(result.configuredWithoutResourceLocales, []);
});
