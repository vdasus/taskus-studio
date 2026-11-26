# Taskus Studio

Cross-platform desktop editor (Avalonia) for Taskus task files: create and edit the YAML that the
`taskus` CLI runs, with live validation and a preview of the mapping on real data. For Taskus users who
prefer a GUI to writing YAML by hand. Design: [docs/brief.md](docs/brief.md).

## Build, test, run

```
dotnet build TaskusStudio.slnx
dotnet test
dotnet run --project src/TaskusStudio [-- path/to/task.yaml]
```

Taskus.Core is referenced with an exact version. Until that version is on nuget.org, pack it from the
Taskus repo into the local feed: `dotnet pack ../Taskus/src/Taskus.Core -c Release -o local-packages`.
Release builds and steps: [README.md](README.md).

## Environment

- **Runtime / SDK**: .NET 10 (`global.json`), Avalonia 12.2, AvaloniaEdit 12, CommunityToolkit.Mvvm 8.4.
- **Tools**: `taskus` CLI (path set in the Studio settings) for Validate and Dry run.
- **Services**: NuGet package `Taskus.Core` (published by the Taskus release; `local-packages/` until then,
  see `nuget.config`).
  Related repo: `C:\REPO\vdasus\Taskus` (`github.com/vdasus/taskus`).
- **Configuration**: `taskus-studio.json` next to the executable, fallback to the user config folder.
  Holds the paths to `taskus` and to the secrets file; no secret values.
- **Deploy target**: self-contained zips for win-x64, linux-x64, osx-arm64 on GitHub Releases (tag `v*`).

## Repository rules

- Public repo `github.com/vdasus/taskus-studio`: author and owner only as `vdasus`; commit identity
  `vdasus <me@vdasus.com>`. No AI attribution in commits.
- Version `major.minor` follows Taskus; patch numbers are independent.

## Project wiki

This repo keeps its own small wiki so that a fresh session can continue without the old chat.
Read this section first; follow it without being asked.

| File | Holds |
|------|-------|
| `CLAUDE.md` (this file) | What the project is, how to build, test and run it, environment, this section |
| `docs/DECISIONS.md` | Decisions that shape the code: what, why, date. Newest last |
| `docs/GOTCHAS.md` | Traps already hit: symptom, cause, fix. One entry per trap |
| `docs/<yyyy-mm-dd>-handoff-<slug>.md` | State for the next session. Only the newest one is current |
| `temp/` (repo root, always git-ignored) | Unresolved sensitive matters: open security holes, exploitable bugs, incident details. Never committed |

At the start of a session: if a `docs/*-handoff-*.md` exists, read the newest one before anything else,
then every file in `temp/`.

### Commands

Each command also works as a phrase. The slash commands are in `.claude/commands/`.

| Command | Also said as | Do |
|---------|--------------|----|
| `/stash` | `/dev-wiki stash`, `/handoffus`, "подготовься к запуску с пустым контекстом", "prepare for a fresh session" | First do `/decisions` and `/gotcha` for this session, and `/env` if build files, services or settings changed. Then write `docs/<yyyy-mm-dd>-handoff-<slug>.md`: **Goal**, **State** (done, file paths, branch, last commit), **Decisions** (agreed in this session, with why; link to `docs/DECISIONS.md`), **Dead ends** (tried, failed, why), **Next steps** (numbered; the first one concrete enough to start at once). Delete older handoff files. Anything unresolved and sensitive goes to `temp/<yyyy-mm-dd>-<slug>.md` with the same headings, not to the handoff; the handoff does not mention it. Reply with the path and the line `Read <path> and continue from Next steps.` |
| `/decisions` | "запиши важные решения", "record the decisions" | Add every decision of this session that is not in `docs/DECISIONS.md` yet |
| `/gotcha [what]` | "сохрани gotcha", "запомни грабли", "save this gotcha" | Add the trap to `docs/GOTCHAS.md`; no argument: the last trap hit in this session |
| `/env` | "обнови окружение", "update the environment" | Update **Environment** in this file from the repo (SDKs, tools, services, config) |
| "продолжай" | `/resumeus`, "continue", "дальше" — in a session that has not done any work yet | Read the newest handoff and the files it points to; compare its **State** with `git status` and `git log -1` and say in one line what differs. Then start on **Next steps** from the first one not done. No handoff: say so and ask what to do |

Without being asked, at the end of a task: a non-obvious fix after debugging → add a gotcha;
a design choice the user made or confirmed → add a decision. Say so in one line.

### Entry formats

```markdown
## <yyyy-mm-dd> <Decision in a few words>

<What was decided.> **Why:** <reason, constraint, rejected alternative.>
```

```markdown
## <Symptom as seen: error text, wrong behaviour>

**Cause:** <why it happens.> **Fix:** <what to do; command or code verbatim.>
```

### Rules

- Generated files in English, neutral register; chat in the user's language.
- No secrets in any file: name the setting and where it lives, never its value.
- `docs/` holds only what is safe to publish. An open security hole, an exploitable bug or an
  incident in progress goes to `temp/`, never to `docs/`, a commit message or `CLAUDE.md`, not even
  as "security issue, being fixed". Before writing to `temp/`, check `git check-ignore -q temp/`;
  not ignored: add `/temp/` to `.gitignore` first. Once fixed, move to `docs/` only what is safe to
  publish (for example a gotcha without the exploit), then delete the `temp/` file.
- Link, don't copy: the README, `docs/DECISIONS.md` and the code are the sources; a handoff points to them.
- Keep `CLAUDE.md` short: details belong in `docs/`.
