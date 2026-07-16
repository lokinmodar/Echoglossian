import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const docsRoot = path.join(__dirname, '..', 'src', 'content', 'docs');

test('docs content avoids root-absolute internal links that bypass the Pages base path', async () => {
  const violations = await collectViolations(docsRoot);

  assert.deepEqual(violations, []);
});

async function collectViolations(directoryPath) {
  const violations = [];
  const entries = await fs.readdir(directoryPath, { withFileTypes: true });

  for (const entry of entries) {
    const entryPath = path.join(directoryPath, entry.name);

    if (entry.isDirectory()) {
      violations.push(...await collectViolations(entryPath));
      continue;
    }

    if (!entry.isFile() || !entry.name.endsWith('.mdx')) {
      continue;
    }

    const content = await fs.readFile(entryPath, 'utf8');
    const patterns = [
      { type: 'card-or-html href', regex: /href="\/(?!\/)/g },
      { type: 'frontmatter hero link', regex: /link:\s*\/(?!\/)/g },
      { type: 'markdown link', regex: /\]\(\/(?!\/)/g },
    ];

    for (const pattern of patterns) {
      for (const match of content.matchAll(pattern.regex)) {
        const lineNumber = getLineNumber(content, match.index ?? 0);
        violations.push(`${path.relative(docsRoot, entryPath)}:${lineNumber} ${pattern.type}`);
      }
    }
  }

  return violations.sort();
}

function getLineNumber(content, index) {
  return content.slice(0, index).split('\n').length;
}
