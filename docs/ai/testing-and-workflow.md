# Testing and Acceptance

## EditMode

Test stateless systems with synthetic state, explicit config, fake adapters, deterministic RNG and
event buffers. Systems should not require scenes, `App.Instance`, `DevCheats`, Resources or direct
Unity physics/time calls.

Before adding coverage, audit the subject for global reads and Unity API use. Route dependencies
through method arguments or `RaidContext` first; otherwise tests lock in false-green behavior.

Guidelines:

- Assert observable state/events, not private call counts or implementation order.
- One fixture owns one subject; use `[TestCase]` for identical behavior over inputs.
- Shared context/definition/scenario builders live under `Assets/Tests/EditMode/Fakes/`.
- Snapshot and restore any unavoidable static state in setup/teardown.
- Cover boundaries, invalid input, state transitions and persistence writeback.

## PlayMode and manual validation

Use PlayMode only where Unity integration is the subject: physics collisions, NavMesh, scene
bindings, authored spawn/extraction triggers and launch flow. Feel, layout, VFX and audio require a
manual editor playtest even when pure logic has EditMode coverage.

Prefer the project Unity MCP bridge. If unavailable, use Unity Test Runner or batchmode. Report the
exact slice executed and never reuse a historical green count as current evidence.

## Acceptance

A gameplay change is complete when its system contract is implemented, focused tests pass, required
view wiring works in the editor, new raid state appears in the debugger, and the relevant living doc
and `tasks.md` status are updated in the same change.
