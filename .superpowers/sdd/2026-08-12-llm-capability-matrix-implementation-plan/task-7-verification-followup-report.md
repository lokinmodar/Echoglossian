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

## Review fix round

### Root cause

The initial follow-up assigned `ChatReasoningEffortLevel.None` directly in the
structured ChatGPT request. This bypassed the effective capability snapshot,
and the installed OpenAI SDK documents only `Low`, `Medium`, and `High` as
supported values. The source-contract test did not exercise the runtime policy
decision.

### Fix

- Removed the unconditional `None` assignment.
- Added `ApplyStructuredReasoningEffortPolicy`, which reads the shared
  capability snapshot, clears any unsupported/default-only effort, and only
  leaves a configured value eligible when the resolved policy supports it.
- Structured exception learning now runs only when an effort value was eligible
  for transmission.
- Replaced the source-text assertion with behavior tests for unsupported
  omission and an exact-model supported overlay retaining an SDK-supported
  `Low` value.

### Review-fix TDD evidence

RED command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityPolicyServiceTests"
```

RED result: compilation failed as expected because
`ApplyStructuredReasoningEffortPolicy` did not exist. After adding the method,
the first test run exposed a temporary test cleanup file lock; the supported
overlay test was changed to use the existing in-memory capability cache seam.

GREEN command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DialogueInterlocutorMetadataResolverTests|FullyQualifiedName~LlmCapabilityPolicyServiceTests"
```

GREEN result: 23 passed, 0 failed, 0 skipped. The build emitted only the
existing Multilingual App Toolkit warning.

### Updated runtime verification

Verify in-game that `gpt-5.6-terra` structured tool requests omit
`reasoning_effort`. A supported exact-model rule may retain a valid configured
SDK effort value; the plugin does not invent one.
