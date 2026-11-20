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
- Run Avalonia code in tests through `await AvaloniaTestHelper.RunAsync(() => ...)`. It dispatches onto one shared
  `Avalonia.Headless` UI thread with a fresh `App` per call. Tests stay plain `[Fact]`s because
  `Avalonia.Headless.XUnit` does not work with xunit v3. Call `AvaloniaTestHelper.FlushDispatcher()` to run queued
  background work, since the action runs no message loop.
- Tests run fully in parallel, so each owns its temp files and state.

## Avalonia

- Track text edits with `TextChanging`, which is synchronous. `TextChanged` is posted and arrives after programmatic
  updates finish.
- Set `AutomationProperties.Name` explicitly on controls whose content has an access-key underscore; otherwise the
  underscore leaks into the automation name.
- The XAML previewer needs `Program.BuildAvaloniaApp` and the parameterless `MainWindow` constructor.
- Publish is trimmed. Settings JSON uses the source-generated `SettingsJsonContext` and COM uses
  `[GeneratedComInterface]`, because reflection-based JSON and built-in COM (so `dynamic` COM) are unavailable.
- Publish is single-file, not Native AOT. AOT cannot embed the SkiaSharp and HarfBuzz native DLLs, so they would ship
  beside the exe and release downloads would no longer be one file.
