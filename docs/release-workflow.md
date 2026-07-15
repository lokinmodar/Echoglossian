# Release Workflow

This guide publishes one exact Echoglossian commit through the project release
and the official Dalamud plugin repository.

## Release States

- **Submitted**: the Echoglossian GitHub release exists and the
  `DalamudPluginsD17` pull request is open.
- **Published**: the official pull request has merged and its manifest is live
  in the Dalamud feed.

Do not close user-facing issues as published until the official pull request
has merged.

## Prerequisites

- The release content is integrated into `v4-series`.
- `gh auth status` succeeds for the maintainer account.
- The local official-repository clone exists at
  `C:\Dante\_dalamud\DalamudPluginsD17` with `origin` pointing to the fork and
  `upstream` pointing to `goatcorp/DalamudPluginsD17`.
- The worktree is clean before switching branches or preparing manifests.

## 1. Prepare The Integration Pull Request

1. Update the version in `Echoglossian.csproj`. The release format is
   `4.yy01.mmdd.HHmm`.
2. Add a concise submitted-release entry to `CHANGELOG.md`.
3. Run the complete validation set from the Echoglossian repository:

   ```powershell
   dotnet build .\Echoglossian.sln -c Debug --no-restore
   dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
   dotnet build .\Echoglossian.csproj -c Release --no-restore
   ```

4. Commit and push the release preparation on the issue branch.
5. Open a ready pull request against `v4-series` with a high-level change
   summary, validation results, related issues, and the required AI disclosure.
6. Wait for required checks and merge the pull request. GitHub does not permit
   authors to approve their own pull requests; use the repository's permitted
   merge path after checks pass rather than representing self-review as an
   approval.

## 2. Verify The Release Commit

After integration, fetch `v4-series` and resolve its exact remote hash. Never
type or infer a release hash manually.

```powershell
git fetch origin v4-series
$releaseCommit = git rev-parse origin/v4-series
$remoteBranch = git ls-remote origin refs/heads/v4-series
if (-not $remoteBranch.StartsWith($releaseCommit)) {
    throw "origin/v4-series does not match the resolved release commit."
}
```

Check out the integrated branch and rerun all three validation commands from
the exact commit. Commit generated `Echoglossian.xml` changes when they are a
real part of the validated release.

## 3. Create The Echoglossian GitHub Release

Create the tag and GitHub release with the exact version and `$releaseCommit`.
Use the submitted changelog entry as release notes. Confirm that the resulting
tag targets the expected commit before continuing.

## 4. Update DalamudPluginsD17

In `C:\Dante\_dalamud\DalamudPluginsD17`:

1. Fetch `origin` and `upstream`.
2. Fast-forward local `main` from `upstream/main` and push it to the fork.
3. Create `update-echoglossian-<version>` from the synchronized `main`.
4. Update only `stable\Echoglossian\manifest.toml` with the exact version,
   `$releaseCommit`, and a compact TOML-safe changelog.
5. Verify that the release commit is reachable from the Echoglossian remote
   before committing the manifest.
6. Commit and push the dedicated branch.
7. Open a pull request against `goatcorp/DalamudPluginsD17:main` using the
   maintainer fork branch as its head.

The official pull request must include the disclosure defined in
`docs/official-plugin-repo-ai-usage-disclosure.md`. Runtime-generated text
textures are not AI-generated repository assets.

## 5. Finish Publication

While the official pull request is open, keep the changelog entry marked as
submitted. After it merges:

1. Change the entry to published and link the official pull request and date.
2. Refresh `docs/github-issue-backlog.md`.
3. Close resolved issues with a comment naming the published version.
4. Commit and push the publication bookkeeping separately.

## Failure Rules

- Stop if build or tests fail for a product-code reason.
- Retry a known transient Release-build file lock once, then investigate.
- Stop if the local and remote release hashes differ.
- Do not reuse an old manifest branch or mix unrelated plugin updates into it.
- Do not claim publication while the official pull request remains open.
