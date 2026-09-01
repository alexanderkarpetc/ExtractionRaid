# Crosshair

`CrosshairPresenter` renders weapon state and hit events through a UI Toolkit/uGUI shader stack. It
does not decide accuracy, recoil, charge, damage or range.

## State mapping

- Ready/firing/cooldown/equip/reload phases control visibility and progress.
- Ballistic uses arms/dot; laser uses a segmented charge/cooldown ring.
- Charge fill reads the same charge ratio used by shooting.
- Recoil displacement reads gameplay `WeaponAimPoint`/recoil state.
- ADS and recoil pressure drive focus-edge softness; firing may add a short presentation pulse.

## Hit feedback

Resolved raid events select normal, kill, headshot or ricochet pulse profiles. The presenter consumes
the event result and never recalculates hit type. Event animation state must reset across scene/play
lifecycles because Reload Domain is off.

## Cursor and UI

`PointerOverUiTracker` is the shared authority for pointer-over-UI and OS cursor visibility. When UI
owns the pointer, gameplay aim/fire is gated and the UI cursor is shown at the same screen position.
Windows must use the shared tracker rather than implement local cursor rules.

## Tuning

Visual tuning belongs in the Crosshair ViewCheats section. Gameplay recoil/charge/aim values belong
in their gameplay configs and flow through `RaidContext`.
