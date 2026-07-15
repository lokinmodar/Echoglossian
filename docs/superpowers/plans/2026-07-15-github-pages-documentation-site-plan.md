# GitHub Pages Documentation Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an isolated Astro Starlight documentation site to this
repository, publish it through GitHub Pages, and keep the plugin and D17
release flow untouched.

**Architecture:** Create a standalone `Echoglossian.Docs` Node project beside
`Echoglossian.Tests`, wire Starlight i18n around the existing resource
cultures, and publish only curated public content authored inside the docs
project. Keep all plugin-facing protection in the root `.csproj` and keep the
site deployment in a dedicated path-filtered Pages workflow.

**Tech Stack:** Astro 5, `@astrojs/starlight`, TypeScript, Node.js 24 LTS,
GitHub Actions Pages, existing `.NET` solution and xUnit test suite.

## Global Constraints

- Keep the plugin build, tests, release artifact, and D17 manifest flow
  unchanged.
- Keep the documentation site in this repository as an independent sibling of
  `Echoglossian.Tests`.
- Publish only explicitly curated content. Do not automatically expose the
  existing `docs` tree.
- Support the same locales as `Properties/Resources*.resx`.
- Use English as the neutral locale and fallback content when a localized page
  does not exist.
- Use only GitHub-provided deployment authentication. Do not add PATs, API
  keys, or third-party service secrets.
- Keep `Echoglossian.sln` unchanged.
- Follow the repo StyleCop and narrow-change rules for the `.csproj` edit.
- Validate with:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
  - `npm.cmd ci`
  - `npm.cmd run test:locales`
  - `npm.cmd run check:locales`
  - `npm.cmd run check`
  - `npm.cmd run build`

---

### Task 1: Scaffold The Isolated Docs Project And Protect The Plugin Build

**Files:**

- Create: `Echoglossian.Docs/package.json`
- Create: `Echoglossian.Docs/tsconfig.json`
- Create: `Echoglossian.Docs/astro.config.mjs`
- Create: `Echoglossian.Docs/src/content.config.ts`
- Create: `Echoglossian.Docs/src/styles/custom.css`
- Modify: `Echoglossian.csproj`

**Interfaces:**

- Produces these workspace commands:

```json
{
  "scripts": {
    "dev": "astro dev",
    "check": "astro check",
    "build": "astro build",
    "preview": "astro preview"
  }
}
```

- Produces a root-locale Starlight configuration with `/Echoglossian/` base:

```js
site: 'https://lokinmodar.github.io',
base: '/Echoglossian/',
```

- [ ] **Step 1: Add the isolated docs package manifest**

```json
{
  "name": "echoglossian-docs",
  "private": true,
  "type": "module",
  "version": "0.0.0",
  "engines": {
    "node": ">=24.0.0"
  },
  "scripts": {
    "dev": "astro dev",
    "check": "astro check",
    "build": "astro build",
    "preview": "astro preview"
  },
  "dependencies": {
    "@astrojs/check": "^0.9.4",
    "@astrojs/starlight": "^0.32.0",
    "astro": "^5.12.0",
    "typescript": "^5.9.2"
  }
}
```

- [ ] **Step 2: Add the base Astro and Starlight configuration**

```js
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://lokinmodar.github.io',
  base: '/Echoglossian/',
  integrations: [
    starlight({
      title: 'Echoglossian Docs',
      locales: {
        root: { label: 'English', lang: 'en' },
      },
      social: [
        {
          icon: 'github',
          label: 'GitHub',
          href: 'https://github.com/lokinmodar/Echoglossian',
        },
      ],
      customCss: ['./src/styles/custom.css'],
    }),
  ],
});
```

- [ ] **Step 3: Protect the root plugin project from the docs tree**

```xml
<ItemGroup>
  <Compile Remove="Echoglossian.Docs\**" />
  <EmbeddedResource Remove="Echoglossian.Docs\**" />
  <None Remove="Echoglossian.Docs\**" />
</ItemGroup>
```

- [ ] **Step 4: Install the docs dependencies**

Run:

```powershell
Set-Location .\Echoglossian.Docs
npm.cmd ci
```

Expected: lockfile is generated and install exits `0`.

- [ ] **Step 5: Commit**

```powershell
git add Echoglossian.Docs Echoglossian.csproj
git commit -m "docs: scaffold isolated GitHub Pages site"
```

### Task 2: Add Locale Validation With A Site-Only Test Cycle

**Files:**

- Create: `Echoglossian.Docs/scripts/check-locales.mjs`
- Create: `Echoglossian.Docs/scripts/check-locales.test.mjs`
- Modify: `Echoglossian.Docs/package.json`
- Modify: `Echoglossian.Docs/astro.config.mjs`

**Interfaces:**

- Produces:

```js
export function collectResourceCultures(resourceFileNames) {}
export function buildConfiguredLocales() {}
export function validateLocales(resourceCultures, configuredLocales) {}
```

- Produces these package scripts:

```json
{
  "scripts": {
    "test:locales": "node --test ./scripts/check-locales.test.mjs",
    "check:locales": "node ./scripts/check-locales.mjs"
  }
}
```

- [ ] **Step 1: Write the failing locale-validation test**

```js
import test from 'node:test';
import assert from 'node:assert/strict';

import {
  buildConfiguredLocales,
  collectResourceCultures,
  validateLocales,
} from './check-locales.mjs';

test('collectResourceCultures maps neutral and regional resources', () => {
  const cultures = collectResourceCultures([
    'Resources.resx',
    'Resources.pt-BR.resx',
    'Resources.ru.resx',
  ]);

  assert.deepEqual(cultures, ['root', 'pt-br', 'ru']);
});

test('validateLocales reports missing configured locales', () => {
  const result = validateLocales(['root', 'pt-br', 'ru'], ['root', 'ru']);

  assert.deepEqual(result.missingInConfig, ['pt-br']);
  assert.deepEqual(result.unexpectedInConfig, []);
});
```

- [ ] **Step 2: Run the locale test to confirm it fails**

Run:

```powershell
Set-Location .\Echoglossian.Docs
node --test .\scripts\check-locales.test.mjs
```

Expected: FAIL because `check-locales.mjs` does not exist yet.

- [ ] **Step 3: Implement the locale script and wire the full locale map**

```js
const configuredLocales = [
  'root',
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
];
```

```js
if (result.missingInConfig.length || result.unexpectedInConfig.length) {
  process.exitCode = 1;
}
```

```js
locales: {
  root: { label: 'English', lang: 'en' },
  da: { label: 'Dansk', lang: 'da' },
  de: { label: 'Deutsch', lang: 'de' },
  el: { label: 'Ελληνικά', lang: 'el' },
  es: { label: 'Español', lang: 'es' },
  eu: { label: 'Euskara', lang: 'eu' },
  fr: { label: 'Français', lang: 'fr' },
  it: { label: 'Italiano', lang: 'it' },
  pt: { label: 'Português', lang: 'pt' },
  'pt-br': { label: 'Português (Brasil)', lang: 'pt-BR' },
  ru: { label: 'Русский', lang: 'ru' },
},
```

- [ ] **Step 4: Re-run the locale test and the CLI validator**

Run:

```powershell
Set-Location .\Echoglossian.Docs
npm.cmd run test:locales
npm.cmd run check:locales
```

Expected: both commands exit `0`.

- [ ] **Step 5: Commit**

```powershell
git add Echoglossian.Docs
git commit -m "docs: validate Starlight locales against plugin resources"
```

### Task 3: Build The Public Site Shell, Navigation, And Fallback-Friendly Layout

**Files:**

- Modify: `Echoglossian.Docs/astro.config.mjs`
- Modify: `Echoglossian.Docs/src/styles/custom.css`
- Create: `Echoglossian.Docs/src/content/docs/index.mdx`
- Create: `Echoglossian.Docs/src/content/docs/users/index.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/index.mdx`
- Create: `Echoglossian.Docs/src/content/docs/pt-br/index.mdx`

**Interfaces:**

- Produces a two-section sidebar:

```js
sidebar: [
  {
    label: 'Plugin User Docs',
    items: [{ slug: 'users' }],
  },
  {
    label: 'Technical Docs',
    items: [{ slug: 'technical' }],
  },
],
```

- Produces a localized root page for `pt-br` while deeper pages fall back to
  English.

- [ ] **Step 1: Add the landing page and section index pages**

```md
---
title: Echoglossian Documentation
description: Curated setup, usage, architecture, and contribution guides.
template: splash
hero:
  title: Echoglossian Documentation
  tagline: Public docs for users and contributors, isolated from the plugin build.
  actions:
    - text: User Guides
      link: /users/
      icon: right-arrow
    - text: Technical Docs
      link: /technical/
      variant: minimal
---
```

```md
---
title: Plugin User Docs
description: Installation, configuration, usage modes, support coverage, and troubleshooting.
---
```

```md
---
title: Technical Docs
description: Architecture, pipeline, persistence, diagnostics, and contribution guidance.
---
```

- [ ] **Step 2: Add the curated navigation and site chrome metadata**

```js
starlight({
  title: 'Echoglossian Docs',
  description: 'Curated documentation for Echoglossian users and contributors.',
  sidebar: [
    {
      label: 'Plugin User Docs',
      items: [
        { slug: 'users' },
        { slug: 'users/overview' },
        { slug: 'users/installation' },
        { slug: 'users/configuration' },
        { slug: 'users/translation-modes' },
        { slug: 'users/support-matrix' },
        { slug: 'users/troubleshooting' },
      ],
    },
    {
      label: 'Technical Docs',
      items: [
        { slug: 'technical' },
        { slug: 'technical/architecture' },
        { slug: 'technical/pipeline' },
        { slug: 'technical/translator-architecture' },
        { slug: 'technical/native-ui-and-overlays' },
        { slug: 'technical/cache-and-persistence' },
        { slug: 'technical/diagnostics' },
        { slug: 'technical/development' },
      ],
    },
  ],
})
```

- [ ] **Step 3: Add one localized root page to prove the locale switcher while leaving deeper pages on English fallback**

```md
---
title: Documentacao do Echoglossian
description: Guias publicos curados para usuarios e contribuidores.
template: splash
---
```

- [ ] **Step 4: Run Astro checks**

Run:

```powershell
Set-Location .\Echoglossian.Docs
npm.cmd run check
```

Expected: PASS with valid frontmatter and sidebar references.

- [ ] **Step 5: Commit**

```powershell
git add Echoglossian.Docs
git commit -m "docs: add public Starlight shell and navigation"
```

### Task 4: Author The English User Documentation Set

**Files:**

- Create: `Echoglossian.Docs/src/content/docs/users/overview.mdx`
- Create: `Echoglossian.Docs/src/content/docs/users/installation.mdx`
- Create: `Echoglossian.Docs/src/content/docs/users/configuration.mdx`
- Create: `Echoglossian.Docs/src/content/docs/users/translation-modes.mdx`
- Create: `Echoglossian.Docs/src/content/docs/users/support-matrix.mdx`
- Create: `Echoglossian.Docs/src/content/docs/users/troubleshooting.mdx`

**Interfaces:**

- Produces a curated user-facing doc set with no secret values and no direct
  publication of internal repo docs.

- [ ] **Step 1: Write the user overview and installation pages**

```md
---
title: Overview
description: What Echoglossian does and who the plugin documentation is for.
---

Echoglossian is a Dalamud plugin for translating supported Final Fantasy XIV
text surfaces in real time.
```

```md
---
title: Installation And First Run
description: How to install the plugin and confirm a healthy first launch.
---

1. Install Echoglossian from the plugin source you already use.
2. Open the plugin configuration window.
3. Pick your source and target languages.
4. Configure a translation provider with your own credential.
```

- [ ] **Step 2: Write configuration, modes, and support-matrix pages**

```md
---
title: Translation Modes
description: When to use overlay, native, and swap presentation.
---

## Overlay

Captures text, translates it, and renders the translation outside the native UI.

## Native

Writes translated text into supported native UI surfaces.

## Swap

Shows translated text in the native UI and the original text in the overlay.
```

```md
---
title: Support Matrix
description: Which translation surfaces are publicly documented as supported.
---
```

- [ ] **Step 3: Write the troubleshooting page with safe placeholders only**

```md
---
title: Troubleshooting And FAQ
description: Common setup, provider, and surface coverage issues.
---

## My provider is configured but translation does not start

Verify the selected provider, model, and credential fields. Use placeholder
values such as `sk-example` only in screenshots or examples.
```

- [ ] **Step 4: Re-run Astro checks and build**

Run:

```powershell
Set-Location .\Echoglossian.Docs
npm.cmd run check
npm.cmd run build
```

Expected: PASS and generate `dist`.

- [ ] **Step 5: Commit**

```powershell
git add Echoglossian.Docs
git commit -m "docs: author curated user documentation"
```

### Task 5: Author The English Technical Documentation Set

**Files:**

- Create: `Echoglossian.Docs/src/content/docs/technical/architecture.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/pipeline.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/translator-architecture.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/native-ui-and-overlays.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/cache-and-persistence.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/diagnostics.mdx`
- Create: `Echoglossian.Docs/src/content/docs/technical/development.mdx`

**Interfaces:**

- Produces a contributor-facing doc set centered on current architecture and
  supported workflows, not internal handoffs or release-maintainer steps.

- [ ] **Step 1: Write the architecture and pipeline pages**

```md
---
title: Architecture Overview
description: The major runtime areas used by the plugin today.
---

- `NativeUI/AddonHandlers/...`
- `NativeUI/Handlers/...`
- `UIOverlays/TranslationOverlay/...`
- `Translators/TranslationService`
```

```md
---
title: Capture, Translation, And Presentation Pipeline
description: How text moves from capture to translated output.
---

1. Capture source text from a supported surface.
2. Translate through the configured provider flow.
3. Present through overlay, native, or swap behavior.
```

- [ ] **Step 2: Write translator, native UI, cache, and diagnostics pages**

```md
---
title: Translator Architecture
description: How provider-specific engines fit into the shared translation flow.
---
```

```md
---
title: Diagnostics
description: Publicly documented commands and investigation tools.
---

- `/egloaddonprobe <addon>`
- standard local build and test commands
```

- [ ] **Step 3: Write the development page without release-maintainer flow**

````md
---
title: Build, Test, And Contributing
description: Local validation and contribution expectations for the public docs site and plugin code.
---

```powershell
dotnet build .\Echoglossian.sln -c Debug --no-restore
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```
````

- [ ] **Step 4: Re-run Astro checks and build**

Run:

```powershell
Set-Location .\Echoglossian.Docs
npm.cmd run check
npm.cmd run build
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Echoglossian.Docs
git commit -m "docs: author curated technical documentation"
```

### Task 6: Add The GitHub Pages Workflow And Final Validation

**Files:**

- Create: `.github/workflows/pages.yml`
- Modify: `Echoglossian.Docs/package.json`
- Modify: `Echoglossian.Docs/README.md` if a docs-local bootstrap note becomes necessary

**Interfaces:**

- Produces a Pages workflow with:

```yaml
on:
  pull_request:
    branches: [v4-series]
    paths:
      - '.github/workflows/pages.yml'
      - 'Echoglossian.Docs/**'
      - 'Properties/Resources*.resx'
  push:
    branches: [v4-series]
    paths:
      - '.github/workflows/pages.yml'
      - 'Echoglossian.Docs/**'
      - 'Properties/Resources*.resx'
  workflow_dispatch:
```

- Produces a deploy gate:

```yaml
if: github.event_name != 'pull_request' && github.ref == 'refs/heads/v4-series'
```

- [ ] **Step 1: Add the Pages workflow with explicit build commands**

```yaml
name: GitHub Pages

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: Echoglossian.Docs
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-node@v4
        with:
          node-version: 24
          cache: npm
          cache-dependency-path: Echoglossian.Docs/package-lock.json
      - run: npm ci
      - run: npm run test:locales
      - run: npm run check:locales
      - run: npm run check
      - run: npm run build
      - uses: actions/upload-pages-artifact@v3
        with:
          path: Echoglossian.Docs/dist
```

- [ ] **Step 2: Add the deploy job and concurrency guard**

```yaml
concurrency:
  group: github-pages-${{ github.ref }}
  cancel-in-progress: true

deploy:
  needs: build
  if: github.event_name != 'pull_request' && github.ref == 'refs/heads/v4-series'
  runs-on: ubuntu-latest
  environment:
    name: github-pages
    url: ${{ steps.deployment.outputs.page_url }}
  steps:
    - id: deployment
      uses: actions/deploy-pages@v5
```

- [ ] **Step 3: Run full local validation**

Run:

```powershell
Set-Location C:\Dante\_dalamud\worktrees\Echoglossian\github-pages-docs\Echoglossian.Docs
npm.cmd ci
npm.cmd run test:locales
npm.cmd run check:locales
npm.cmd run check
npm.cmd run build

Set-Location C:\Dante\_dalamud\worktrees\Echoglossian\github-pages-docs
dotnet build .\Echoglossian.sln -c Debug --no-restore
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Expected: docs commands exit `0`; plugin build and tests remain green.

- [ ] **Step 4: Inspect the generated site behavior**

Check:

- `/Echoglossian/` asset links resolve
- locale switcher lists every resource culture
- `/pt-br/users/overview/` renders English fallback content
- user and technical sidebars are clearly separated
- no internal handoff or release documents are published

- [ ] **Step 5: Commit**

```powershell
git add .github/workflows/pages.yml Echoglossian.Docs Echoglossian.csproj
git commit -m "docs: add GitHub Pages documentation workflow"
```

---

Plan complete and saved to `docs/superpowers/plans/2026-07-15-github-pages-documentation-site-plan.md`.
