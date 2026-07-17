# GitHub Pages Documentation Site Design

## Status

Approved on 2026-07-15.

## Goal

Add a public GitHub Pages documentation site for Echoglossian without adding
the site to `Echoglossian.sln` or coupling its build and deployment to the
plugin or the DalamudPluginsD17 release flow.

The site will provide two curated areas:

- user documentation for installing, configuring, and troubleshooting the
  plugin
- technical documentation for understanding and contributing to the plugin

## Constraints

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

## Selected Approach

Use Astro Starlight in a new `Echoglossian.Docs` directory. Starlight is a
documentation-focused static site generator with built-in navigation, search,
internationalization, neutral-locale fallback content, and notices for pages
that have not yet been translated.

VitePress was not selected because neutral-page fallback would require custom
behavior. Docusaurus supports a broad documentation feature set but adds React
and more project complexity than the initial Echoglossian site needs.

## Repository Layout

The documentation project will be self-contained:

```text
Echoglossian/
|-- Echoglossian.csproj
|-- Echoglossian.sln
|-- Echoglossian.Tests/
|-- Echoglossian.Docs/
|   |-- package.json
|   |-- package-lock.json
|   |-- astro.config.mjs
|   |-- tsconfig.json
|   |-- scripts/
|   |-- public/
|   `-- src/
|       |-- content.config.ts
|       `-- content/
|           `-- docs/
|               |-- index.md
|               |-- users/
|               |-- technical/
|               `-- <localized content directories>/
`-- .github/
    `-- workflows/
        `-- pages.yml
```

`Echoglossian.Docs` will have its own Node dependencies, scripts, and lockfile.
It will not be added to `Echoglossian.sln` and will have no project reference
to the plugin or tests.

Because `Echoglossian.csproj` is at the repository root and SDK projects use
recursive item globs, it will explicitly remove `Echoglossian.Docs/**` from
`Compile`, `EmbeddedResource`, and `None` items. This mirrors the existing
isolation for `Echoglossian.Tests` and prevents a future standalone `.cs`
example from entering the plugin build.

## Information Architecture

The landing page will present two primary destinations.

### User Documentation

The initial user section will contain:

- Echoglossian overview
- installation and first-run guidance
- general configuration
- translator selection and engine-specific configuration
- native, overlay, and swap translation modes
- supported translation surfaces and the support matrix
- troubleshooting and frequently asked questions

Configuration tutorials may show fictional API-key placeholders but will not
contain or request repository secrets. Users will continue to enter their own
provider credentials in the plugin.

### Technical Documentation

The initial technical section will contain:

- architecture overview
- capture, translation, and presentation pipeline
- translator architecture
- native UI handlers and translation overlays
- cache, database, and persistence behavior
- diagnostic commands and investigation tools
- build, test, and contribution guidance

Release-maintainer procedures, handoffs, backlogs, investigation logs, and
implementation plans will remain outside the initial public navigation.

## Curation Boundary

Only files deliberately authored or adapted under `Echoglossian.Docs` will be
published. The existing repository `docs` directory will remain the source for
internal engineering material and will not be copied or traversed by the site
build.

Existing material may be adapted into public pages in focused changes. Public
pages will be written for their target audience instead of exposing internal
documents wholesale. Existing localized content such as the translation
surface support matrix may be reused where it matches the public structure.

## Internationalization

English will be the neutral and canonical content locale. The English routes
will live at the site root. Other locales will use lowercase URL segments and
BCP 47 language metadata where applicable.

| Resource culture | URL segment | HTML language |
| --- | --- | --- |
| neutral | root | `en` |
| `da` | `da` | `da` |
| `de` | `de` | `de` |
| `el` | `el` | `el` |
| `es` | `es` | `es` |
| `eu` | `eu` | `eu` |
| `fr` | `fr` | `fr` |
| `it` | `it` | `it` |
| `pt-BR` | `pt-br` | `pt-BR` |
| `pt` | `pt` | `pt` |
| `ru` | `ru` | `ru` |

Localized pages will use the same relative path as their English counterparts.
When a localized page does not exist, Starlight will render the neutral English
page and display its fallback-content notice. Translation can therefore
progress independently page by page without producing broken navigation.

A small site-owned validation script will compare the configured locales with
the filenames in `../Properties/Resources*.resx`. A mismatch will fail only
the documentation check. It will never participate in the `.NET` build or D17
release job.

## GitHub Pages Deployment

The site will be a project Pages site at the default URL:

```text
https://lokinmodar.github.io/Echoglossian/
```

Astro will therefore use `/Echoglossian/` as its base path.

`.github/workflows/pages.yml` will be the only site deployment workflow. It
will support:

- pull requests targeting `v4-series`, where it validates and builds the site
  without deploying
- pushes to `v4-series`, where it builds and deploys the site
- manual runs through `workflow_dispatch`

Automatic triggers will be path-filtered to the documentation project, the
Pages workflow, and the resource files used by locale validation. A normal
plugin-only change will not run the Pages workflow.

The workflow will:

1. check out the repository
2. install Node.js 24 LTS
3. run `npm ci` in `Echoglossian.Docs`
4. validate the resource-locale mapping
5. run Astro/Starlight checks
6. build the static site
7. upload `Echoglossian.Docs/dist` as a Pages artifact
8. deploy only when the run targets `refs/heads/v4-series`; manual runs from
   other refs validate and build without deploying

Deployment will use the official `actions/upload-pages-artifact` and
`actions/deploy-pages` actions. The deployment job will use the
GitHub-provided token with `pages: write` and `id-token: write` permissions and
the `github-pages` environment. No PAT or manually configured secret is
required.

The Pages check must not be configured as a required D17 release check. A
documentation failure may block a new website deployment, but it must not
alter the plugin artifact, release commit verification, or official manifest
submission.

## Failure Behavior

- Missing localized page: render the neutral English page with a fallback
  notice.
- Invalid frontmatter, configuration, or broken site build: fail the
  documentation workflow before deployment.
- Failed deployment: retain the previously published site and report the
  failure only in the Pages workflow.
- Locale mismatch with plugin resources: fail the documentation validation
  with the missing or unexpected cultures listed.
- Plugin build or test failure: handle through the existing `.NET` validation;
  the site introduces no alternate plugin build path.

## Validation

The initial implementation changes `Echoglossian.csproj` only to add the site
exclusions, so it must run the standard repository validation:

```powershell
dotnet build .\Echoglossian.sln -c Debug --no-restore
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

The documentation project must pass:

```powershell
Set-Location .\Echoglossian.Docs
npm.cmd ci
npm.cmd run check:locales
npm.cmd run check
npm.cmd run build
```

The generated site will also be inspected for:

- correct `/Echoglossian/` asset and navigation URLs
- clear separation between user and technical navigation
- all configured locale selectors
- neutral fallback rendering for at least one untranslated localized route
- usable desktop and narrow-screen navigation
- absence of real secrets, credentials, or private material

## Non-Goals

The initial implementation will not:

- add the documentation project to `Echoglossian.sln`
- build the plugin from the Pages workflow
- publish all files under the repository `docs` directory
- automate translation of documentation content
- add external analytics, hosted search, databases, server-side code, PATs, or
  third-party secrets
- change the existing plugin release or DalamudPluginsD17 submission process
- add a custom domain

## Risks and Mitigations

- **Root-project glob leakage:** explicitly exclude the site tree from the
  plugin project items.
- **Documentation locale drift:** validate the Starlight locale configuration
  against the resource filenames in a site-only check.
- **Internal material published accidentally:** publish only the site content
  tree and do not auto-import `docs`.
- **Broken GitHub Pages subpath:** configure and verify Astro's
  `/Echoglossian/` base path.
- **Documentation work affecting D17:** keep the solution unchanged and use an
  independent, path-filtered Pages workflow that is not a D17 requirement.
- **Translation coverage lag:** use English fallback content and allow each
  locale to advance page by page.

## Acceptance Criteria

- `Echoglossian.Docs` exists as an independent Astro Starlight project.
- `Echoglossian.sln` remains unchanged.
- The plugin project explicitly excludes the documentation tree.
- The site has distinct user and technical areas with the agreed initial
  navigation.
- The configured site locales match `Properties/Resources*.resx`.
- Missing translations render the English neutral page with a notice.
- The site builds locally with the documented `npm.cmd` commands.
- Standard plugin build and test commands still pass.
- Pull requests validate the site without deploying it.
- Eligible `v4-series` pushes deploy to GitHub Pages using only GitHub-native
  authentication.
- The Pages workflow does not build, package, or submit the plugin.

## References

- [Starlight internationalization](https://starlight.astro.build/guides/i18n/)
- [Configuring a GitHub Pages publishing source](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site)
- [GitHub Actions workflow path filters](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#onpushpull_requestpull_request_targetpathspaths-ignore)
- [Official GitHub Pages deployment action](https://github.com/actions/deploy-pages)
- [Node.js release schedule](https://nodejs.org/en/about/previous-releases)
