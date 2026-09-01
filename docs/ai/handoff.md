# ExtractionRaid — Handoff

> Compact operational context for the next working session. Updated **2026-09-01**.
> Tasks and status live only in [`tasks.md`](./tasks.md).

## Start here

1. Read [`CLAUDE.md`](./CLAUDE.md).
2. Open [`tasks.md`](./tasks.md) and pick from **Поточний фокус**.
3. Read only the domain doc routed by `CLAUDE.md §9`.
4. Check `git log -15` and ownership notes before touching shared content.
5. Run the relevant EditMode slice; use the Unity MCP bridge when available.

## Current baseline

- Branch audited: `master` on 2026-09-01.
- Last recorded full suite: **700 EditMode tests green on 2026-08-05**. This is a baseline,
  not a claim about the current checkout; rerun before reporting current results.
- Release direction is fixed in [`release-scope.md`](./release-scope.md).
- The repository had no uncommitted documentation changes before this cleanup.

## Recent verified changes

- Raid timeout now ends through the ordinary KIA pipeline and preserves extraction-wins-tie behavior.
- Orphan ammo definitions were removed; ammo availability is guarded by tests.
- Inventory gained double-click quick transfer.
- Bot weapons roll module rarity per spawn.
- Weapon compare/tooltips show consistent values; magazine overfill is guarded by tests.
- `Test_Map`/Rednek City received substantial environment and prefab work through August.

## Important implementation facts

- `GameAudioPresenter` and a real audio library exist. Audio is no longer a missing scaffold;
  remaining work is coverage, mix, ambience, settings and content polish.
- Extraction already has system logic, UTK HUD states, minimap markers and support for multiple
  `ExtractionPointState` entries. Remaining work is authoring/readability/playtest.
- `ProgressionSystem.ApplyAllocatedEffects` is still intentionally empty on current `master`.
- `DeployUI` is still IMGUI.
- Save data has no explicit schema version or migration pipeline.

## Ownership and collision risks

- Ask the owner before changing `Systems/Meta`, progression/loot work or bot configs that overlap
  parallel contributions.
- Map content, vegetation and shaders are active contributor areas; keep gameplay diffs separate.
- Do not inspect `.unity`, `.prefab` or `.asset` files as text. Use the Unity Editor/MCP bridge.

## Architecture reminders

- Systems are stateless and do not call `App.Instance`.
- State stores values and IDs only; Unity references stay in view/presenter.
- Tunables flow through `RaidContext.*Config`, populated by `RaidSession`.
- New raid-state fields must appear in the Raid State Debugger.
- No new singleton; keep diffs local and cover logic changes with tests.
