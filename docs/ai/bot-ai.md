# Bot AI Contract

## Pipeline

Each tick runs:

```text
Perception → Behavior Tree → intents → Movement / Combat
```

Perception updates observations and memory. The behavior tree selects intent. Movement and combat
execute that intent through adapters and domain events. Nodes do not move transforms, spawn Unity
objects or call App.

## Decision model

The tree is a priority selector. Survival actions pre-empt offensive actions; combat requires alert
state; search/chase bridge lost contact; patrol is the fallback. Behavior flags configure which
branches a bot type can use without creating separate AI implementations.

Long actions return `Running` and keep their timing/state on the blackboard. Cooldowns and timestamps
are per bot, never static.

## Perception and memory

- Vision uses range, field of view and physics occlusion.
- Hearing records an approximate last-known position; it does not grant exact tracking.
- Damage alerts may acquire the attacker/impact direction without bypassing reaction delay.
- Target memory decays after loss of contact; expiry or arrival at the last-known position triggers
  a bounded search before forgetting. Search owns its full window and cannot be cut short by memory.
- Alert/reaction gates aiming, facing and firing so acquisition is readable to the player.
- After search, patrol resumes from the nearest route point. A single spawn fallback is rebased to
  the search area instead of pulling the bot back across the map.

## Combat behavior

- Bots fire in bursts, consume magazine ammo and reload; `ShootNode` decides trigger intent while
  `BotCombatSystem` performs the shot.
- Accuracy combines authored config with per-spawn personality and current conditions.
- Cover selection must have a valid NavMesh path and meaningful protection from the remembered
  threat. Peek/fire/duck behavior remains interruptible by damage and reload.
- Heal and grenade actions expose cast/commit windows and cannot secretly execute while another
  incompatible action is active.
- Engagement limits prevent untelegraphed fire from far outside the player view.

## Movement

The brain writes desired destinations/velocity; `BotMovementSystem` resolves NavMesh movement and
facing. Repath is throttled, stuck paths fail cleanly, and movement never changes authoritative
combat results directly.

## Tuning and tests

Bot type data and behavioral values live in `BotConstants` and context configs. The Raid State/BT
debuggers are the runtime inspection tools. Unit tests should lock branch priority, perception
boundaries, action state machines and intent execution; feel tuning still requires an editor playtest.
