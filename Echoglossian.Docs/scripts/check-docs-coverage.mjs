import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { locales } from '../src/site-config.mjs';
import {
  publishedSections,
  technicalDiagramRoutes,
} from './docs-published-manifest.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const docsRoot = path.join(__dirname, '..', 'src', 'content', 'docs');

export function translatedLocales() {
  return Object.keys(locales).filter((locale) => locale !== 'root');
}

export function buildExpectedLocalizedFiles(
  localesFilter = translatedLocales(),
  sectionsFilter = ['root', 'users', 'technical'],
) {
  const sections = publishedSections();
  const activeRoutes = sectionsFilter.flatMap((section) => sections[section]);

  return localesFilter.flatMap((locale) => activeRoutes.map((route) => {
    const relativePath = route === 'index' ? 'index.mdx' : `${route}.mdx`;
    return path.posix.join(locale, relativePath);
  }));
}

export function validateLocalizedCoverage(expectedFiles, actualFiles) {
  return {
    missingFiles: expectedFiles.filter((filePath) => !actualFiles.has(filePath)),
  };
}

export async function validateTechnicalDiagramMarkers(
  rootDirectory,
  localesFilter = translatedLocales(),
) {
  const missingMarkers = [];

  for (const locale of localesFilter) {
    for (const route of technicalDiagramRoutes) {
      const filePath = path.join(rootDirectory, locale, `${route}.mdx`);
      const content = await fs.readFile(filePath, 'utf8');
      const markerName = diagramMarkerNameForRoute(route);

      if (!content.includes('MermaidDiagram') || !content.includes(markerName)) {
        missingMarkers.push(path.relative(rootDirectory, filePath).replaceAll('\\', '/'));
      }
    }
  }

  return missingMarkers.sort();
}

function diagramMarkerNameForRoute(route) {
  switch (route) {
    case 'technical/architecture':
      return 'buildArchitectureDiagram';
    case 'technical/pipeline':
      return 'buildPipelineDiagram';
    case 'technical/translator-architecture':
      return 'buildTranslatorArchitectureDiagram';
    case 'technical/native-ui-and-overlays':
      return 'buildNativeUiAndOverlaysDiagram';
    case 'technical/cache-and-persistence':
      return 'buildCacheAndPersistenceDiagram';
    case 'technical/diagnostics':
      return 'buildDiagnosticsDiagram';
    case 'technical/development':
      return 'buildDevelopmentWorkflowDiagram';
    default:
      throw new Error(`No diagram marker defined for route: ${route}`);
  }
}

async function main() {
  const args = new Map(
    process.argv.slice(2).map((argument) => {
      const [key, value] = argument.split('=');
      return [key, value];
    }),
  );

  const localesFilter = args.has('--locales')
    ? args.get('--locales').split(',').filter(Boolean)
    : translatedLocales();
  const sectionsFilter = args.has('--sections')
    ? args.get('--sections').split(',').filter(Boolean)
    : ['root', 'users', 'technical'];

  const expectedFiles = buildExpectedLocalizedFiles(localesFilter, sectionsFilter);
  const actualFiles = await collectLocalizedDocFiles(docsRoot);
  const coverage = validateLocalizedCoverage(expectedFiles, actualFiles);

  if (coverage.missingFiles.length > 0) {
    console.error(`Missing localized docs files:\n${coverage.missingFiles.join('\n')}`);
    process.exitCode = 1;
  }

  if (sectionsFilter.includes('technical')) {
    const missingMarkers = await validateTechnicalDiagramMarkers(docsRoot, localesFilter);

    if (missingMarkers.length > 0) {
      console.error(`Technical docs missing Mermaid markers:\n${missingMarkers.join('\n')}`);
      process.exitCode = 1;
    }
  }

  if (process.exitCode !== 1) {
    console.log(`Localized docs coverage looks complete for locales: ${localesFilter.join(', ')}`);
  }
}

async function collectLocalizedDocFiles(rootDirectory) {
  const results = new Set();

  await walk(rootDirectory, results);

  return results;
}

async function walk(directoryPath, results) {
  const entries = await fs.readdir(directoryPath, { withFileTypes: true });

  for (const entry of entries) {
    const entryPath = path.join(directoryPath, entry.name);

    if (entry.isDirectory()) {
      await walk(entryPath, results);
      continue;
    }

    if (!entry.isFile() || !entry.name.endsWith('.mdx')) {
      continue;
    }

    const relativePath = path.relative(docsRoot, entryPath).replaceAll('\\', '/');

    if (
      relativePath.startsWith('users/') ||
      relativePath.startsWith('technical/') ||
      relativePath === 'index.mdx'
    ) {
      continue;
    }

    results.add(relativePath);
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === __filename) {
  await main();
}
