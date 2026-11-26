# Taskus Studio: design brief

Settled on 2026-11-26.

## Goal

A cross-platform desktop editor for Taskus task files (YAML): create new tasks and edit existing ones,
see the mapping result on real data while typing. It lives in its own repository because most Taskus
users do not need a GUI.

## Decisions

### Product

- Creates new task files and opens existing ones. Comments in existing files are not preserved in v1;
  opening a file with comments shows a warning. New files start with
  `# yaml-language-server: $schema=https://raw.githubusercontent.com/vdasus/taskus/main/schema/task.schema.json`.
- Layout: three columns **Source | Map | Sink**, the type is chosen in each column header. General
  settings (`onError`, `maxErrors`, `report`, `smtp`, `logExclude`) in a separate "Run settings" panel.
  A live YAML preview slides out on the right. No wizard. "New" opens a template gallery built from the
  Taskus `examples/`.
- One window per file, "Recent files", `*` in the title for unsaved changes, a save prompt on close. No tabs.
- UI language: English only in v1.

### Map editor

- Rows: target field | template | preview value.
- Templates edited in AvaloniaEdit with Scriban highlighting and completion of `row.<column>`
  (names with spaces or diacritics complete as `row['Pavardė']`).
- A source column dragged onto Map adds `Column: "{{ row['Column'] }}"`.
- Preview of the mapped values on rows 1..N of real data.
- v1 preview: excel source only. Preview file: the first file in `folder` matching `pattern`, a list to
  pick another file of that folder, and "Browse..." for any .xlsx when the folder does not exist on this
  machine. Database and http sources: columns typed by hand.

### Validation and running

- Validation runs as the user types, through `Taskus.Core`. Errors show at the field (red border and text)
  and as a list in the bottom panel; a click on an error goes to its field. Saving an invalid file is
  allowed, with a warning.
- "Validate" and "Dry run" call the `taskus` CLI (path set in the settings) with
  `--secrets=<secrets file from the settings>` and show its raw console output in the bottom panel,
  ERR/WRN lines highlighted. The output is not parsed.
- Dry run asks for confirmation when the source makes a real call that may change data: http source with
  POST, PUT, PATCH or DELETE, or a SQL query that does not start with `SELECT` or `WITH`.
- GUI has no execution engine of its own.

### Secrets

- A drop-down lists the key names from the secrets file and inserts `${secret:name}`. Values are never
  shown or edited. On Windows the names are read from `secrets.sec` through `Taskus.Core` (DPAPI).
- Fields whose description says "Usually ${secret:name}" (connection strings, passwords) warn on a raw value.

### YAML writer

- The GUI does not serialize `TaskOptions` directly (that would write every default).
- Its own writer emits only the fields the user set, in this key order:
  `name, source, map, sink, onError, maxErrors, report, smtp, logExclude`.
- Every written file is loaded back through `TaskLoader` from `Taskus.Core` as a self-check.

### Reuse of Taskus code: `Taskus.Core`

- Everything in the Taskus project except `Program.cs`, `App.cs` and `Logging.cs` moves to a library
  `Taskus.Core` (Config, Mapping, Pipeline, Transports). The `taskus` executable becomes a thin shell.
- `Taskus.Core` is published to NuGet by the Taskus `release.yml`, with the Taskus version.
- Taskus Studio references an exact `Taskus.Core` version.

### Versions

- Taskus gets a `--version` option.
- Taskus and Taskus Studio share `major.minor`; patch numbers are independent. Studio `X.Y.n` works with
  any Taskus `X.Y.*`.
- Rule for Taskus: new or renamed keys and changed field meaning only in minor releases. Patch releases may
  tighten validation but do not change the schema.
- Studio shows "for Taskus X.Y" in About and in the title, and warns before Validate or Dry run when the
  `major.minor` of `taskus --version` differs.
- The first Studio release carries the current Taskus minor version.

### Settings

- `taskus` path, secrets file path, recent files, theme (system / light / dark).
- Stored in `taskus-studio.json` next to the executable. When that folder is not writable (Program Files,
  macOS App Translocation), the user folder is used instead (`%APPDATA%/TaskusStudio`,
  `~/.config/TaskusStudio`, `~/Library/Application Support/TaskusStudio`); the status bar shows the path in use.

### Technology

- .NET 10, Avalonia (latest stable, MIT), built-in Fluent theme following the system light/dark mode,
  one accent color. No third-party themes.
- MVVM with CommunityToolkit.Mvvm, no DI container (same as Taskus ADR-001).
- Tests: xUnit v3, AutoFixture, FluentAssertions 7.x, NSubstitute, Avalonia.Headless for view models;
  every test has a `Category` trait.

### Repository and delivery

- `github.com/vdasus/taskus-studio`, local `C:\REPO\vdasus\Taskus-studio`. Author `vdasus` only; LICENSE as
  in Taskus.
- Layout: `TaskusStudio.slnx`, `Directory.Packages.props`, `src/TaskusStudio` (assembly `taskus-studio`),
  `tests/TaskusStudio.Tests`.
- CI: tests on ubuntu and windows. Release on tag `v*`: self-contained zips for `win-x64`, `linux-x64`,
  `osx-arm64` plus sha256. No installer, no auto-update, `taskus` not bundled.
- macOS builds are not signed or notarized in v1; README documents `xattr -dr com.apple.quarantine <app>`.

## Order of work

1. Taskus: `--version`, extract `Taskus.Core`, publish it to NuGet from `release.yml`, release a new minor.
2. Studio: repository skeleton, CI, an empty window with the three columns.
3. Document model: open/save, YAML writer, validation as the user types.
4. Forms for Source, Sink and Run settings.
5. Map editor with excel preview.
6. Validate / Dry run through the CLI, secrets, recent files, release.

## Out of scope for v1

- Preserving comments of existing files.
- Data preview for postgres, odbc and http sources.
- Editing secrets or showing their values.
- Running tasks other than through the `taskus` CLI; scheduling.
- Wizard UI, tabs, localization.
- Installer, auto-update, macOS signing.
- Parsing CLI output into structured results.
