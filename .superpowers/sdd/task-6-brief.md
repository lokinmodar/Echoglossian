### Task 6: Validate End-To-End Safety, Update Docs, And Lock In The No-Plugin-Break Regression Guard

**Files:**
- Modify: `Echoglossian.Previewer/README.md`
- Modify: `docs/handoffs/unified-imgui-previewer-next-step.md`
- Modify: `docs/superpowers/specs/2026-07-17-preview-hybrid-dalamock-font-backend-design.md` only if the implementation changes the approved design materially
- Modify: `Echoglossian.xml` if regenerated

**Interfaces:**
- Consumes:
  - all prior task outputs
- Produces:
  - updated operator docs for CLI and shell backend selection
  - recorded validation evidence and remaining debt

- [ ] **Step 1: Document CLI and shell backend selection in the previewer README**

```md
### Plugin window backend selection

Use `--plugin-window-backend auto|standalone|dalamock` to choose how
`Config`, `DB Manager`, and `Translator Metrics / Debugger` are hosted.

- `auto`: try DalaMock first, then fall back to standalone
- `standalone`: always use the previewer's existing plugin-window runtime
- `dalamock`: require the DalaMock-hosted runtime
```

- [ ] **Step 2: Update the handoff with what shipped and what still remains**

```md
## Hosted plugin-window backend

Phase 2 added a hybrid plugin-window backend with:

- CLI and shell selection for `auto`, `standalone`, and `dalamock`
- DalaMock-hosted runtime for `Config`, `DB Manager`, and `Translator Metrics / Debugger`
- explicit fallback diagnostics in the shell and screenshot manifest
```

- [ ] **Step 3: Run the production-safe build validation**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
```

Expected: PASS with 0 errors. Previewer and mock projects must remain outside the solution.

- [ ] **Step 4: Run the main production test suite**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

Expected: PASS. Any failure here is a plugin regression and blocks completion.

- [ ] **Step 5: Run the previewer tests**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1
```

Expected: PASS.

- [ ] **Step 6: Run the DalaMock rail**

Run:

```powershell
dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

Expected: PASS.

- [ ] **Step 7: Run the previewer host smoke and one hosted-backend smoke path**

Run:

```powershell
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --plugin-window-backend auto --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080 --output artifacts\previewer\hosted-backend-validation
```

Expected: host smoke exits `0`; screenshot command writes `manifest.json` and one PNG, and the manifest records the requested/effective backend.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Previewer\README.md docs\handoffs\unified-imgui-previewer-next-step.md docs\superpowers\specs\2026-07-17-preview-hybrid-dalamock-font-backend-design.md Echoglossian.xml
git commit -m "docs: validate hybrid previewer hosted backend workflow"
```
