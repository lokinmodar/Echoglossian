# SDD Progress

Task 1: complete (commits 1c4ea1e..feeecd3, review clean)
Task 2: complete (commits feeecd3..9bad56e, review clean)
Task 3: complete (commits 9bad56e..6f641ee, review clean; hosted startup blocked by pre-existing DalaMock/Dalamud incompatibility)
Task 4: complete (commits 6f641ee..24ffadb, review clean; end-to-end hosted frame still blocked by pre-existing DalaMock/Dalamud incompatibility)
Task 5: complete (commits 24ffadb..d015f49, review clean; end-to-end hosted frame still blocked by pre-existing DalaMock/Dalamud incompatibility)

Minor review notes:
- Task 4: add runtime fallback coverage for crop retrieval, capture end, and `CaptureFailed` in `PluginWindowPreviewBackendFactoryTests.cs`.
- Task 5: deepen manifest coverage to assert `SerializeManifest` output or a runner-produced manifest, not only the test helper record.
