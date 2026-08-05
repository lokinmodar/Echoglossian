import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { locales } from '../src/site-config.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repositoryRoot = path.resolve(__dirname, '..', '..');
const resourcesDirectory = path.join(repositoryRoot, 'Properties');

export function collectResourceCultures(resourceFileNames) {
  return [...new Set(resourceFileNames.map((fileName) => (
    normalizeResourceLocaleKey(toCultureSegment(fileName))
  )))].sort();
}

export function buildConfiguredLocales() {
  return Object.keys(locales).sort();
}

export function validateLocales(resourceCultures, configuredLocales) {
  const resourceCultureSet = new Set(resourceCultures);
  const configuredLocaleSet = new Set(configuredLocales);

  const unpublishedResourceLocales = resourceCultures.filter(
    (culture) => !configuredLocaleSet.has(culture));
  const configuredWithoutResourceLocales = configuredLocales.filter(
    (culture) => !resourceCultureSet.has(culture));

  return {
    unpublishedResourceLocales,
    configuredWithoutResourceLocales,
  };
}

async function main() {
  const resourceFileNames = await getResourceFileNames(resourcesDirectory);
  const resourceCultures = collectResourceCultures(resourceFileNames);
  const configuredLocales = buildConfiguredLocales();
  const result = validateLocales(resourceCultures, configuredLocales);

  if (result.unpublishedResourceLocales.length > 0) {
    console.warn(
      `Plugin resources include locales not yet published by docs: ${result.unpublishedResourceLocales.join(', ')}`,
    );
  }

  if (result.configuredWithoutResourceLocales.length > 0) {
    console.error(
      `Unexpected locale config entries: ${result.configuredWithoutResourceLocales.join(', ')}`,
    );
    process.exitCode = 1;
    return;
  }

  console.log(`Locale config is compatible with plugin resources: ${configuredLocales.join(', ')}`);
}

async function getResourceFileNames(directoryPath) {
  const entries = await fs.readdir(directoryPath, { withFileTypes: true });

  return entries
    .filter((entry) => entry.isFile())
    .map((entry) => entry.name)
    .filter((entry) => entry === 'Resources.resx' || (
      entry.startsWith('Resources.') &&
      entry.endsWith('.resx')
    ));
}

function toCultureSegment(fileName) {
  if (fileName === 'Resources.resx') {
    return 'root';
  }

  return fileName
    .slice('Resources.'.length, -'.resx'.length)
    .toLowerCase();
}

function normalizeResourceLocaleKey(cultureSegment) {
  if (cultureSegment === 'root' || Object.hasOwn(locales, cultureSegment)) {
    return cultureSegment;
  }

  if (cultureSegment === 'en' || cultureSegment.startsWith('en-')) {
    return 'root';
  }

  return cultureSegment.split('-')[0];
}

if (process.argv[1] && path.resolve(process.argv[1]) === __filename) {
  await main();
}
