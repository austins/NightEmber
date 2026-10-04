# AGENTS.md

`README.md` is the user-facing spec. When behavior, settings, or keyboard shortcuts change, update it in the same
change.

## Verify

- Done means `dotnet test` passes (~15 s). It builds with analyzers as errors, so it is also the lint.
- Run one class with `dotnet test --filter-class <FullName>` (xUnit v3 on Microsoft.Testing.Platform).
- Verify through tests rather than launching `NightEmber.exe`, which tints the real displays and spawns a watchdog.

## Conventions

- XML-document types and interface members with intent and caveats; implementing members stay bare.
- Give every `#pragma warning disable` a trailing comment saying why.
- Name tests `Member_Scenario_Expectation` with `// Arrange`, `// Act`, `// Assert` sections.
- Assertions come from AwesomeAssertions (`.Should()`), a global using in the test project.
- Isolate side effects behind internal interfaces with hand-written fakes, as in `AppControllerTests.FakeRuntime`.
- Wrap WPF object use in `WpfTestHelper.RunAsync`, which supplies the STA thread.
- Tests run fully in parallel, so each owns its temp files and state.
