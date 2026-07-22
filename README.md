# Project G1

A retro FPS in the spirit of 1998 — original story, original world, built in Unity
with a fully scripted Blender asset pipeline. Hazard suits, humming laboratories,
a crowbar, and a man in a suit who is always watching.

| The scientist | The man in the suit | First tool of the trade |
|---|---|---|
| ![Protagonist](docs/images/protagonist_front.png) | ![Villain](docs/images/villain_front.png) | ![Crowbar](docs/images/crowbar.png) |

![Test scene](docs/images/unity_pov.png)

## Current features

- **Campaign & Level Generation** — Complete 3-level campaign programmatically generated from modular Blender-scripted environment kits:
  - **Level 1 (Corvus Facility)**: Locker Room → Lab Corridor → Control Room → Industrial Hall (HECU ambush) → Alien Breach Zone → Emergency Elevator.
  - **Level 2 (Quarantine Zone)**: Outdoor industrial complex, toxic hazard zones, jump pads, and squad combat.
  - **Level 3 (Threshold Breach)**: Anomaly chamber, Xen portal breach, and multi-phase boss arena.
  - **Main Menu & Settings**: Retro UI with level select, volume controls, mouse sensitivity tuning, and FOV adjustments.
- **Half-Life 1 movement physics** — real GoldSrc constants converted to meters:
  Quake-lineage acceleration, friction, the authentic 30-ups air cap (strafe steering + bhop speed gain), hold-to-bunnyhop, coyote time, crouch.
- **Weapons & Equipment** — crowbar → pistol → shotgun → SMG → .357 magnum → cookable grenades. Found as spinning pickups. Every model is scripted in Blender with animated slides/bolts/cylinders; per-shell shotgun reload, revolver cylinder FSM with emergency chamber. Includes a toggleable **Flashlight** (`F`).
- **Secondary fire (RMB)** — pistol 3-round burst, shotgun double-barrel, SMG 40mm grenade launcher (draws from grenade reserve), crowbar charged heavy swing (2.5× + knockback). Grenades bounce with a full explosion (light flash, shockwave ring, debris) and cooking feedback.
- **HEV armor** — HL-style armor pool absorbs 80% of incoming damage; AP meter on the HUD; battery pickups and wall chargers (`E`) across the campaign.
- **Pickups & Progression** — Health/armor/ammo packs, story lore cards, checkpoints, and a cross-session **save/Continue** (JSON in persistentDataPath).
- **Bosses** — Level 2 HECU gunship: strafing machine-gun runs, 3-rocket salvos, destructible rotor health.
- **Enemies & AI** — zombies and aliens with separation steering, HECU soldiers running a GOAP-lite planner (cover claims, squad roles, flanks, opportunist alpha strikes), all paced by an L4D2-style ThreatDirector with horde events. Features interactive CCTV monitoring screens and narrative G-Man cameos.
- **Procedural Audio & Music** — Synthesized retro SFX generated from pure math (guns, impacts, doors, pickups, footsteps, horde roars), dynamic background ambience, and tension music tracks without external audio files.
- **Combat core** — `IDamageable` / `HealthSystem` events, breakables, damage vignette + hit markers, world-space debug health bars, death → fade → checkpoint respawn.
- **Retro HUD** — GoldSrc-amber health/ammo in Share Tech Mono, low-health pulse, weapon pickup flash, story cards, green crosshair.
- **Sandbox & Testing Range** — Complete testing sandbox with auto-infinite ammo locking (`G1InfiniteAmmoSandbox`), God Mode invincibility (`G`), 3D Fly/Noclip Mode (`V`), and an interactive **Mob Spawner Toolbox** (`TAB`) for dynamically spawning Zombies, HECU Soldiers, Aliens, Hordes, Squads, and Bosses with a "Kill All" cleanup utility.
- **Observability tooling** — F3 telemetry HUD, soldier AI state gizmos, seeded arena presets for reproducible AI testing.
- **Everything is procedural** — scenes, models, animations, audio, navmesh: all generated from code in the repo, nothing hand-placed.

## Requirements

- **Unity 2022.3.62f3 LTS** (built-in render pipeline, classic Input Manager)
- **Git LFS** — model binaries (`*.fbx`) are LFS-tracked. Run `git lfs install`
  once before cloning/pulling, or every model imports as an empty 131-byte
  pointer file and the scene builder fails with
  `InvalidOperationException: Sequence contains no matching element`.
- **Blender 4.x/5.x** — only needed to regenerate or modify the 3D assets

> This repo began as an empty Unity 6 URP template and was retargeted to
> 2022.3 LTS + built-in RP for a simpler, retro-appropriate baseline.

## Getting started

1. Clone and open the project in Unity Hub with 2022.3 LTS (first import takes a few minutes).
2. Open `Assets/Scenes/TestScene.unity` or `Assets/Scenes/MainMenu.unity` and press **Play**.
3. Rebuild levels anytime via the top editor menu:
   - **G1 → Build Main Menu**
   - **G1 → Build Test Scene** (Level 1)
   - **G1 → Build Level 2 (Quarantine)**
   - **G1 → Build Level 3 (Threshold)**
   - **G1 → Build Weapon Testing Range** (Sandbox range with mob spawner & infinite ammo)
   - **G1 → Rebuild Arena / [Preset]** (AI testing sandboxes)

### Controls

| Input | Action |
|---|---|
| WASD | Move (HL1 acceleration model) |
| Mouse | Look |
| Space (hold) | Jump / auto-bunnyhop (Up in Fly mode) |
| Ctrl or C | Crouch (Down in Fly mode) |
| Shift (hold) | Sprint / Speed boost (Fast fly mode) |
| Left mouse | Attack (swing / fire / cook grenade) |
| Right mouse | Secondary fire (burst / double-barrel / launcher / heavy swing) |
| R | Reload |
| 1–6 / scroll | Switch weapon (unlocked slots only; 6 = Grenade) |
| F | Toggle Flashlight |
| E | Use (doors, terminals) |
| G | Toggle God Mode (Invincibility) |
| V | Toggle 3D Fly Mode (Flight / Noclip) |
| TAB | Toggle Mob Spawner Toolbox |
| F3 | Toggle AI telemetry overlay |
| Esc | Release mouse cursor / Open pause settings menu |

## Project layout

```
Assets/
  G1/
    Models/       Protagonist.fbx, Villain.fbx, Crowbar.fbx, Gun FBXs (Blender exports)
    Scripts/      runtime gameplay code (movement, weapons, NPCs, checkpoints, pickups, UI)
    Editor/       G1SceneBuilder, G1CampaignBuilders, G1MenuBuilder, G1Screenshot
    Anim/         generated AnimatorControllers
    Materials/    generated scene materials
  Scenes/         MainMenu.unity, TestScene.unity, etc. (generated — safe to delete and rebuild)
Tools/
  blender/        the asset pipeline: model, rig, and animate everything from code
docs/             documentation (start here: docs/asset-pipeline.md)
```

## Documentation

- [Asset pipeline](docs/asset-pipeline.md) — how every model and animation is generated from Blender scripts
- [Player movement](docs/player-movement.md) — the HL1 physics model and how to tune it
- [Characters & animation](docs/characters-and-animation.md) — skeleton, skinning, clips, NPC driver
- [Weapons](docs/weapons.md) — crowbar, 9mm pistol, shotgun, SMG, magnum, grenades
- [Combat & health](docs/combat-and-health.md) — IDamageable, HealthSystem events, health bars, death/respawn
- [Audio](docs/audio.md) — the procedural SFX pipeline, footsteps, ambience, and G1Audio API
- [Observability](docs/observability.md) — F3 telemetry HUD, soldier AI gizmos, seeded arena presets
- [Scene builder](docs/scene-builder.md) — how levels are generated from code
- [Story bible](docs/story.md) — the Corvus Annex, the Threshold event, chapters, characters
- [Architecture](docs/architecture.md) — full technical spec
- [Art bible](docs/art_bible.md) — art direction and asset list

## Roadmap

- ~~Firearms (pistol, SMG, shotgun, magnum)~~ ✓ · ~~Audio & footsteps~~ ✓ · ~~Death/respawn~~ ✓
- ~~Health & ammo pickups, grenades, flashlight~~ ✓
- ~~Checkpoints, main menu, settings (sensitivity/FOV/volume)~~ ✓
- ~~Ambience + music soundscapes~~ ✓
- ~~Level 1 (Corvus Annex) → Level 2 (Quarantine Zone) → Level 3 (Threshold Boss Arena)~~ ✓
- Save/load game state serialization to disk
- Secondary fire modes for firearms (SMG grenade launcher, shotgun double-barrel)
- Advanced multi-phase alien boss mechanics in Level 3
- Modding and custom procedural level seed export/import support

