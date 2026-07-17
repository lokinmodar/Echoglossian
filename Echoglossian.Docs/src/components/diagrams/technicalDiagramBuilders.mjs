import { technicalDiagramLabels } from './technicalDiagramLabels.mjs';

function labelsFor(locale) {
  const normalizedLocale = (locale ?? 'root').toLowerCase();

  return technicalDiagramLabels[normalizedLocale] ?? technicalDiagramLabels.root;
}

export function buildArchitectureDiagram(locale) {
  const labels = labelsFor(locale).architecture;

  return `
flowchart TD
    A["${labels.capture}\\nNativeUI/AddonHandlers"] --> B["${labels.sharedHelpers}\\nNativeUI/Handlers"]
    B --> C["${labels.translation}\\nTranslators/TranslationService"]
    C --> D["${labels.overlay}\\nUIOverlays/TranslationOverlay"]
    C --> E["${labels.persistence}\\nDBHelpers / EF entities"]
    C --> F["${labels.languageMetadata}\\nLanguagesHandling"]
  `.trim();
}

export function buildPipelineDiagram(locale) {
  const labels = labelsFor(locale).pipeline;

  return `
flowchart LR
    A["${labels.capture}"] --> B["${labels.scopeReuse}"]
    B --> C["${labels.translation}\\nTranslationService"]
    C --> D["${labels.overlay}"]
    C --> E["${labels.nativeMode}"]
    C --> F["${labels.swapMode}"]
  `.trim();
}

export function buildTranslatorArchitectureDiagram(locale) {
  const labels = labelsFor(locale).translatorArchitecture;

  return `
flowchart LR
    A["${labels.sharedRuntime}\\nTranslationService / ITranslator"] --> B["${labels.selection}"]
    A --> C["${labels.orchestration}"]
    A --> D["${labels.persistenceHandoff}"]
    A --> E["${labels.engineAdapters}"]
    E --> F["${labels.clientSetup}"]
    E --> G["${labels.payloadShape}"]
    E --> H["${labels.responseParsing}"]
  `.trim();
}

export function buildNativeUiAndOverlaysDiagram(locale) {
  const labels = labelsFor(locale).nativeUiAndOverlays;

  return `
flowchart TD
    A["${labels.translationFinished}"] --> B{"${labels.presentationMode}"}
    B --> C["${labels.overlayOnly}"]
    B --> D["${labels.nativeMode}"]
    B --> E["${labels.swapMode}"]
  `.trim();
}

export function buildCacheAndPersistenceDiagram(locale) {
  const labels = labelsFor(locale).cacheAndPersistence;

  return `
flowchart LR
    A["${labels.sourceOfTruth}\\nDB"] --> B["${labels.reuseLayer}\\nMemory cache"]
    B --> C["${labels.visibleState}"]
    D["${labels.scopeRules}"] --> B
  `.trim();
}

export function buildDiagnosticsDiagram(locale) {
  const labels = labelsFor(locale).diagnostics;

  return `
flowchart TD
    A["${labels.issueObserved}"] --> B["${labels.probe}\\n/egloaddonprobe <addon>"]
    B --> C{"${labels.classifyByStage}"}
    C --> D["${labels.capture}"]
    C --> E["${labels.cachePersistence}"]
    C --> F["${labels.overlay}"]
    C --> G["${labels.nativeMutation}"]
  `.trim();
}

export function buildDevelopmentWorkflowDiagram(locale) {
  const labels = labelsFor(locale).development;

  return `
flowchart TD
    A["${labels.scopedChange}"] --> B["${labels.docsValidation}"]
    A --> C["${labels.pluginValidation}"]
    B --> D["${labels.pullRequest}"]
    C --> D
    D --> E["${labels.mergeToV4Series}"]
  `.trim();
}
