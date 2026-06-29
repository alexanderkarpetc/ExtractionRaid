# Weapon Comparison UX — Recon

> How shooters/looters let you compare a loot/inventory weapon against what you have equipped,
> and — the hard part for us — **which** equipped weapon to compare against when you carry more
> than one. Recon only (2026-06-26); recommendation at the end, implementation after approval.
> Reference picks via [competitor-reference-db.md](./competitor-reference-db.md).

## The problem, split in three

1. **Compare target** — against which weapon do we diff? (the crux: we hold **2** weapons)
2. **Presentation** — how the diff reads (side-by-side panels · inline ↑/↓ arrows · delta column)
3. **Trigger** — how the player invokes it (auto-on-hover · hold-key · toggle)

Our constraints: top-down; **2 generic hotbar weapon slots** (`WeaponSlots[0/1]`, no primary/secondary
role), one "selected" (in hand); weapons differ by archetype (Ballistic/Laser × Pistol/Rifle/Shotgun),
rarity, attachments. We already have `WeaponStatDisplay` (player-facing bars) + green/red delta rendering
(built for the attachment editor) — the diff math is solved; this is a UX-shape + "compare-target" call.

## "Compare against which?" — the taxonomy

| Model | Who uses it | Fits us? |
|---|---|---|
| **Single equipped slot → auto** | Diablo/PoE (per slot), Destiny (per bucket) | ✗ we have 2 generic weapon slots, not 1-per-category |
| **Same slot-category** | Division (primary1/primary2/sidearm), Tarkov, **STALKER 2** (2 primary + pistol) | ⚠️ needs typed slots; ours are generic → doesn't disambiguate |
| **Active / in-hand weapon** | Cyberpunk, Monster Hunter, most consoles | ✓ one clear target ("better than what I'm holding?") |
| **All equipped (multi-column delta)** | Diablo dual-rings, PoE | ✓ only 2 slots → "vs Slot 1 / vs Slot 2" is bounded |
| **"Would-replace" prediction** | some auto-equip RPGs | ✗ generic slots → no natural auto-target |

## Per-game notes (knowledge-grounded; ★ = confidence)

- **The Division 2** ★★★ — auto side-by-side vs the equipped item of the **same slot**; green↑/red↓ per stat + gear-score headline. Clean because every gear piece has one obvious slot. *Lesson:* a single headline number (gear-score) + per-stat arrows reads instantly.
- **Destiny 2** ★★★ — **hold-to-compare**: inspecting a weapon pops the equipped weapon of that bucket beside it, stat bars with the delta highlighted. *Lesson:* hold-key keeps the default view uncluttered; bars make deltas glanceable.
- **Borderlands 3** ★★ — hover a ground weapon → an "Equipped" card auto-pops next to it with ↑/↓ per stat vs your **active** weapon; you can cycle which equipped slot it compares to. *Lesson:* auto-pop on hover is great for high-loot-churn games (≈ extraction); cycling handles multiple slots.
- **Diablo / PoE** ★★★ — the origin of side-by-side compare; dual-slot items (rings) show **both** equipped side by side. *Lesson:* for ≤2 of a kind, showing both is acceptable and complete.
- **STALKER 2** ★★ — 2 primary slots + 1 pistol; grid inventory; weapon stat bars (damage/accuracy/handling/range/durability). Compare is **per-slot vs equipped**. *Caveat:* exact auto-diff behavior worth a web-verify if we lean on it; the structural point (typed slots → same-slot compare) is the takeaway, and it's the part we **can't** copy (our slots are generic).
- **Tarkov** ★★★ — **no** clean auto-compare; you read raw stats yourself. Our db flags this "hidden formula" as the anti-pattern. *Lesson:* don't make the player do the diff in their head.
- **ZERO Sievert** ★★★ (direct competitor) — top-down; inventory shows weapon stats; comparison is light. *Opportunity:* a clean diff tooltip is a concrete UX win over the closest competitor.

## Recommendation for ExtractionRaid

The user's mental model — *"open 2 tooltips and diff"* — maps cleanly to **two side-by-side tooltips**:
the hovered weapon + one **baseline** equipped weapon, with green/red per-stat deltas on the hovered one.
The whole "against which" question = **how we pick the baseline** for that second tooltip.

**Primary proposal — active baseline + flip:**
- Baseline defaults to the **in-hand (selected) weapon** (`WeaponSlots[SelectedHotbarSlot]`).
- A **flip key** (e.g. hold `Alt`, or `Tab`/scroll while hovering) cycles the baseline to the **other**
  equipped slot — so both comparisons are one keystroke apart without a third panel on screen.
- If only one weapon is equipped → that's the baseline; if none → no compare panel (just the normal tooltip).
- Deltas work cross-archetype (the `WeaponStatDisplay` axes — Damage/RoF/Stability/Accuracy/Ergo/Mag — are
  normalized), so "Laser Shotgun vs Ballistic Pistol" still reads as raw power deltas.

**Alternative — dual-delta (both at once):** one hovered tooltip + a compact **two-column** delta block
(`vs Slot 1` / `vs Slot 2`). More complete, no flip needed, but denser on a top-down screen. Good if we
find flipping annoying in playtest.

**Trigger:** auto-show the compare panel when hovering a weapon in **loot/backpack** while a weapon is
equipped (extraction = constant loot triage, like Borderlands). Keep a hold-key as the "show even more"
or the flip. (Auto vs hold is a small playtest call.)

**Why this over same-slot (STALKER/Division):** those rely on typed slots to pick the target automatically.
Our 2 slots are interchangeable, so "active + flip" is the honest adaptation — it never guesses wrong and
covers both guns with one key.

## Implementation sketch (light — for the post-approval phase)

Reuse, don't rebuild:
- `WeaponTooltipBuilder` / `WeaponStatDisplay` already produce the stat rows + bars.
- Green/red delta rendering already exists in the attachment editor (`BuildDeltaLabel`, comparison bars) —
  lift that into a shared `WeaponStatDiff` helper used by both.
- `TooltipController` is currently single-panel → add a second "compare" panel (or a combined two-column
  model). Needs a baseline-weapon input + the flip-key handling in the inventory hover path
  (`InventoryWindow.OnSlotPointerEnter`).
- Baseline selection is pure logic → a small testable helper (`WeaponCompareTarget`) that, given the
  inventory + hovered item, returns the baseline weapon(s).

## Shipped (2026-06-26)

Decisions locked: **active baseline + hold-Alt peek** (hold = show the other equipped weapon,
release = back to active), **auto-on-hover**, diff shown as **comparison bars (gold base +
green/red delta segment) + numeric delta chips** (like the attachment editor).

- `View/UI/Inventory/WeaponCompareTarget.cs` — baseline pick (selected-first, skip-self, flip-wrap).
- `Systems/WeaponStatComparison.cs` — pairs two weapons' `WeaponStatDisplay` rows → delta rows.
- `View/UI/Compare/WeaponComparePanel.cs` + `Resources/UI/Compare/WeaponCompare.uss` — floating
  two-column overlay (hovered w/ comparison bars | baseline "IN HAND" plain). Cursor-positioned,
  reuses the Tooltip PanelSettings.
- `InventoryWindow` — hover a weapon (loot/backpack/…) with an equipped weapon → compare panel
  auto-shows (suppresses the single tooltip); **holding `Alt`** swaps the baseline to the other
  equipped weapon (release returns to active); hidden on leave / drag-start.
- 622 EditMode green (WeaponCompareTests covers target + diff).

Still open / future: diff currently covers the bar stats only (not attachments/ammo-type lines);
dual-delta (both slots at once) remains the fallback if active+flip feels off in playtest.
