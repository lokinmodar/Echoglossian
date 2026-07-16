import assert from 'node:assert/strict';
import test from 'node:test';

import {
  buildArchitectureDiagram,
  buildDevelopmentWorkflowDiagram,
  buildPipelineDiagram,
} from '../src/components/diagrams/technicalDiagramBuilders.mjs';

test('buildArchitectureDiagram keeps code-facing identifiers in English', () => {
  const diagram = buildArchitectureDiagram('root');

  assert.ok(diagram.includes('NativeUI/AddonHandlers'));
  assert.ok(diagram.includes('TranslationService'));
});

test('buildPipelineDiagram localizes prose labels while keeping runtime names', () => {
  const diagram = buildPipelineDiagram('pt-br');

  assert.ok(diagram.includes('Capture source text'));
  assert.ok(diagram.includes('TranslationService'));
  assert.ok(diagram.includes('Overlay publication'));
});

test('buildDevelopmentWorkflowDiagram falls back to root labels for unknown locales', () => {
  const diagram = buildDevelopmentWorkflowDiagram('xx');

  assert.ok(diagram.includes('Docs validation'));
  assert.ok(diagram.includes('Plugin validation'));
});
