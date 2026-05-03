## Summary

- what changed
- why it changed
- any user-facing or reviewer-facing context that matters

## Validation

- [ ] `dotnet build Echoglossian.sln -c Debug --no-restore`
- [ ] `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- [ ] in-game verification was performed when runtime UI behavior changed

## AI Usage Disclosure

Required for submissions to the official Dalamud plugin repository when AI use
went beyond basic autocomplete or inline suggestions.

Select one when applicable:

- [ ] `Assist` — human-led work with AI used for specific tasks
- [ ] `Pair` — active human/AI collaboration throughout
- [ ] `Copilot` — AI did most of the writing while the human planned and reviewed
- [ ] `Auto` — AI acted mostly autonomously with minimal human direction

If you selected one of the levels above, describe the scope briefly:

```text
AI scope:
- tooling used:
- files or areas most affected:
- how the result was reviewed/tested:
```

If no AI was used, or only basic autocomplete / inline suggestions were used,
you do not need to disclose anything here.

## AI-Generated Assets Disclosure

If this PR adds or changes AI-generated icons, images, music, sounds, textures,
or other user-facing assets, disclose them here so the plugin description can be
updated if needed.

```text
AI-generated assets:
- none
```
