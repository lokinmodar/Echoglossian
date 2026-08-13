# Task 7 verification follow-up report

## Scope

- Normalized live-actor customize-sex values `"0"` to `"male"` and `"1"` to
  `"female"`, retaining the existing textual mappings and null fallback.
- Set structured ChatGPT tool requests to
  `ChatReasoningEffortLevel.None` and recorded reasoning-effort provider
  failures through the exact model capability scope.
- Preserved pre-existing pending diagnostics edits in `ChatGPTTranslator.cs`.

## TDD evidence

RED command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DialogueInterlocutorMetadataResolverTests|FullyQualifiedName~LlmCapabilityPolicyServiceTests"
```

RED result: 3 failed, 19 passed, 22 total. The expected failures were missing
numeric sex mappings, missing structured reasoning-effort configuration and
learning call, and no promoted reasoning-effort rule for the raw provider
message. The last assertion was revised to verify the existing observation-only
feedback behavior for that message because it does not meet the classifier's
explicit-unsupported promotion criteria.

GREEN command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DialogueInterlocutorMetadataResolverTests|FullyQualifiedName~LlmCapabilityPolicyServiceTests"
```

GREEN result: 22 passed, 0 failed, 0 skipped. The build emitted pre-existing
project warnings, including the unavailable Multilingual App Toolkit warning.

## Remaining runtime verification

Confirm in-game that live NPC speaker metadata logs `speakerGender=true` for
numeric customize-sex values, and that `gpt-5.6-terra` accepts structured tool
requests with `reasoning_effort: none`.
