# Decisions

Full design: [brief.md](brief.md).

## 2026-11-26 Separate repository

The GUI lives in `vdasus/taskus-studio`, not in the Taskus repo. **Why:** most Taskus users do not need a
GUI, and its dependencies (Avalonia, editors) would clutter the CLI repo.

## 2026-11-26 Avalonia on .NET 10

Desktop UI with Avalonia, built-in Fluent theme, MVVM with CommunityToolkit.Mvvm, no DI container.
**Why:** cross-platform including Linux (MAUI has no Linux), same language as Taskus so its code runs in
process; Avalonia core is MIT. Rejected: Tauri/Electron (second language, preview only through the CLI).

## 2026-11-26 Three-column layout, no wizard

Source | Map | Sink columns, a Run settings panel, a slide-out YAML preview. **Why:** matches the task model
(source, map, sink) and shows the whole task at once; the user dislikes wizards.

## 2026-11-26 Reuse Taskus through the Taskus.Core package

Everything in Taskus except `Program.cs`, `App.cs`, `Logging.cs` moves to a library `Taskus.Core`,
published to NuGet with the Taskus version; Studio references an exact version. **Why:** validation,
mapping preview and secrets reading must behave exactly as the CLI. Rejected: rewriting from
`task.schema.json` (preview could differ from Taskus), a `--json` CLI mode (too slow for live preview).

## 2026-11-26 Versions share major.minor with Taskus

Studio `X.Y.n` works with Taskus `X.Y.*`; patches are independent. Taskus changes the schema only in minor
releases. **Why:** same-version means compatible, without forcing empty releases for patch fixes.

## 2026-11-26 Own YAML writer

Write only the fields the user set, in a fixed key order, then load the result through `TaskLoader`.
Comments of opened files are not preserved in v1. **Why:** serializing `TaskOptions` would write every default.

## 2026-11-26 Validate and Dry run through the CLI

Buttons call the configured `taskus` and show its raw output; dry run asks for confirmation when the source
call may change data. **Why:** checks the actual installed `taskus`; no execution engine in the GUI.

## 2026-11-26 Settings next to the executable

`taskus-studio.json` next to the exe, fallback to the user config folder when not writable. **Why:** user
preference for portable zips; the fallback covers Program Files and macOS App Translocation.

## 2026-11-26 Forms built from the Taskus.Core model

Source, sink and run settings forms are generated from the `TaskOptions` records by reflection (names,
`[Description]`, defaults, enums, required members, nested records); no hand-written form per type.
**Why:** a new Taskus setting appears in Studio by updating the Taskus.Core package, with no Studio change.

## 2026-11-26 Single-file builds in two kinds

Releases ship single-file executables for win-x64, linux-x64 and osx-arm64, each self-contained and
framework-dependent (6 zips); the Taskus CLI release uses the same layout. **Why:** user request; one file to
copy, and the framework-dependent kind is much smaller where .NET 10 is installed.

## 2026-11-26 Local package feed until Taskus.Core is published

`nuget.config` adds `local-packages/` next to nuget.org; the `.nupkg` files there are not committed.
**Why:** Studio is developed before the first Taskus.Core release; CI resolves the package from nuget.org only.
