# AGENTS.md — coding-agent entry point (Codex, etc.)

**Your operating contract for this repo is [`docs/ai/CLAUDE.md`](docs/ai/CLAUDE.md) — open and read
it before any non-trivial work, and follow it.** It is tool-agnostic; ignore any assistant-specific
framing. Its **§9 "task routing"** table tells you exactly which doc under `docs/ai/` to open for the
area you're touching — open those **on demand**, don't load everything. `docs/ai/handoff.md` has the
current state; `docs/ai/release-scope.md` defines release scope and `docs/ai/tasks.md` is the only
task tracker.

> Note for `AGENTS.md`-loading agents (Codex): only this file is auto-loaded into your context —
> **linked files are not.** So actually open `docs/ai/CLAUDE.md` (and the per-task docs it routes you
> to) yourself before starting.

## Absolute rules (full detail in `docs/ai/CLAUDE.md`)

- **Project:** Unity 6 (6000.3.10f1, URP) top-down extraction shooter. 5-layer architecture:
  App → Session → Systems → Adapters → View/Presenter. Collaboration is in **Ukrainian**, terse.
- Gameplay logic = **stateless static Systems**; the only singleton is `App.Instance`; Systems must
  not call `App.Instance`. **State** holds values/IDs only (no Unity object refs). View/Presenter hold
  Unity refs and no game rules. **Never add new singletons.**
- Tunables flow through `RaidContext.*Config` (populated in `RaidSession.Tick`); **Systems don't read
  `DevCheats` directly.** New State field → update the Raid State Debugger.
- Keep diffs **small and local**; add/adjust tests when logic changes.
- **Do not commit or push unless the maintainer asks.**

## Tooling

- Don't read `.unity` / `.prefab` / `.asset` files as text to reconstruct scene state — they're large
  and binary-ish; inspect them in the Unity Editor.
- **If you have an editor/MCP bridge available, use it** to inspect scenes and run EditMode tests.
  Otherwise edit the `.cs` and run EditMode tests via the Unity Test Runner (or `-batchmode -runTests`),
  or hand the change to the maintainer to run. Don't claim tests pass unless you actually ran them.
  See `docs/ai/testing-and-workflow.md`.

_This is a thin pointer; `docs/ai/CLAUDE.md` is the single source of truth (its §9 is the doc index)._
