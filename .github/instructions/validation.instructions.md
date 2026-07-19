---
description: "Use when validating Echoglossian changes that may depend on Dalamud runtime services or hosted plugin behavior."
applyTo:
  - "**/*.cs"
  - "**/*.csproj"
  - "AGENTS.md"
  - ".github/instructions/**"
---

# Runtime validation

- Keep the standard validation path as the baseline:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Use `Echoglossian.Mock` and/or DalaMock when a change depends on Dalamud service wiring, plugin startup/shutdown, configuration paths, plugin-window hosting, font/ImGui behavior, or other runtime integration that pure unit tests cannot model.
- For behavior that reads real game data, Lumina sheets, FFXIVClientStructs-backed state, addon lifecycle events, `AtkValue`, `AtkUnitBase`, or native UI payload capture/application, validate with `Echoglossian.Mock`/DalaMock whenever feasible before claiming the behavior is covered.
- If the current harness cannot drive the needed game-data or native UI payload, extend `Echoglossian.Mock` or DalaMock first when the extension is practical; otherwise document the gap and keep the required in-game verification explicit.
- Do not claim `.Mock` validates capture/application unless the test actually drives the relevant mocked game-data, addon lifecycle, or native UI payload. Startup-only Mock tests prove wiring/load, not text capture or translation application.
- Prefer the DalaMock-backed `Echoglossian.Mock.Tests` harness for hosted startup/session validation:
  - `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`
  - `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1`
- Keep mock validation scoped to behavior that DalaMock can actually represent. If the target requires live game addon state, state the DalaMock limitation and list the required in-game check.
- Include focused tests with the change whenever a bug fix or feature changes observable behavior.
