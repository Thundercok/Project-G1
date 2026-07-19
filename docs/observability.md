# AI observability & arena presets

Tools for watching the combat AI think, and reproducible arena variations for
testing edge cases.

## F3 telemetry HUD (`Scripts/UI/ArenaDebugHUD.cs`)

Press **F3** in Play mode to toggle a screen panel showing:

- **Mobs engaging player** — from `CombatArenaMonitor` (radius poll)
- **Player HP %**
- **Opportunists waiting** — soldiers registered on the `SquadBlackboard`
  predator layer, holding back for a coordinated strike
- **Last alpha strike** — seconds since the blackboard last triggered one

Mounted on the Player by the scene builder. Read-only: it renders state,
never mutates it.

## Soldier state gizmos (`Scripts/NPC/AIDebugGizmos.cs`)

Editor-only (visible in the Scene view while playing; compiled out of builds).
Each HECU soldier shows:

- a **wire sphere** overhead colored by current action — yellow PopFire,
  cyan MoveToCover, orange Suppress, magenta Flank, blue Reload, grey Retreat,
  red Opportunist
- a **label** with `Action | SquadRole`
- a **green line** to the claimed `G1CoverPoint`, **red line** to the player

Backed by read-only getters added to `G1SoldierAI` (`CurrentAction`,
`ClaimedRole`, `ClaimedCover`, `PlayerXform`). The component lives in the
runtime assembly (an `Editor/` folder script can't be attached to a prefab)
with all drawing wrapped in `#if UNITY_EDITOR`.

## Arena presets (menu **G1 → Rebuild Arena**)

| Preset | Seed | Soldiers | Zombies | Aliens | Cover | Purpose |
|---|---|---|---|---|---|---|
| Standard Arena | 1337 | 1 | 1 | 1 | 8 | everyday testing |
| Solo HECU Test | 101 | 1 | 0 | 0 | 8 | isolate one soldier's decisions |
| Horde Overwhelm Test | 666 | 2 | 3 | 3 | 10 | director stress, alpha strikes |
| Low Cover Test | 42 | 2 | 1 | 1 | 2 | cover-starved soldier behavior |

All generation randomness (crate scatter, cover-block placement, extra NPC
positions) comes from one `System.Random` seeded per preset — rebuilds are
byte-for-byte reproducible, and the global `UnityEngine.Random` state is never
touched, so gameplay RNG can't shift the layout.

Cover blocks now place **two `G1CoverPoint` markers each** (one per broad
side); the soldiers' cover query validates line-of-sight at runtime and picks
the safe side. Before this, the built scene contained no cover points at all —
the cover system ran against an empty registry.

`G1SceneBuilder.BuildScene()` (menu **G1 → Build Test Scene**, batch
`-executeMethod`) still builds the Standard preset.
