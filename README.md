# Project G1

A retro FPS in the spirit of 1998 — original story, original world, built in Unity
with a fully scripted Blender asset pipeline. Hazard suits, humming laboratories,
a crowbar, and a man in a suit who is always watching.

| The scientist | The man in the suit | First tool of the trade |
|---|---|---|
| ![Protagonist](docs/images/protagonist_front.png) | ![Villain](docs/images/villain_front.png) | ![Crowbar](docs/images/crowbar.png) |

![Test scene](docs/images/unity_pov.png)

## Current features

- **Playable Level 1** — Locker Room → Lab Corridor → Control Room → Industrial
  Hall (HECU ambush) → Alien Breach Zone → Emergency Elevator, generated
  entirely by the scene builder from modular Blender-scripted environment kit.
- **Half-Life 1 movement physics** — real GoldSrc constants converted to meters:
  Quake-lineage acceleration, friction, the authentic 30-ups air cap (strafe
  steering + bhop speed gain), hold-to-bunnyhop, coyote time, crouch.
- **Five weapons with progression** — crowbar → pistol → shotgun → SMG →
  .357 magnum, found through the level as spinning pickups. Every model is
  scripted in Blender with animated slides/bolts/cylinders; per-shell shotgun
  reload, revolver cylinder FSM with emergency chamber.
- **Enemies & AI** — zombies and aliens with separation steering, HECU
  soldiers running a GOAP-lite planner (cover claims, squad roles, flanks,
  opportunist alpha strikes), all paced by an L4D2-style ThreatDirector with
  horde events.
- **Procedural audio** — twelve synthesized retro SFX generated from pure
  math (guns, impacts, doors, pickups, horde roar), pooled playback,
  no external assets.
- **Combat core** — `IDamageable` / `HealthSystem` events, breakables, damage
  vignette + hit markers, world-space debug health bars, death → fade →
  respawn.
- **Retro HUD** — GoldSrc-amber health/ammo in Share Tech Mono, low-health
  pulse, weapon pickup flash, green crosshair.
- **Observability tooling** — F3 telemetry HUD, soldier AI state gizmos,
  seeded arena presets for reproducible AI testing.
- **Everything is procedural** — scene, models, animations, audio, navmesh:
  all generated from code in the repo, nothing hand-placed.

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
2. Open `Assets/Scenes/TestScene.unity` and press **Play**.
3. If the scene is missing or broken, regenerate it: **G1 → Build Test Scene**.

### Controls

| Input | Action |
|---|---|
| WASD | Move (HL1 acceleration model) |
| Mouse | Look |
| Space (hold) | Jump / auto-bunnyhop |
| Ctrl or C | Crouch |
| Left mouse | Attack (swing / fire) |
| R | Reload |
| 1–5 / scroll | Switch weapon (unlocked slots only) |
| E | Use (doors, terminals) |
| F3 | Toggle AI telemetry overlay |
| Esc | Release mouse cursor |

## Project layout

```
Assets/
  G1/
    Models/       Protagonist.fbx, Villain.fbx, Crowbar.fbx (Blender exports)
    Scripts/      runtime gameplay code (movement, weapons, NPCs, interaction)
    Editor/       G1SceneBuilder (scene generator), G1Screenshot (headless captures)
    Anim/         generated AnimatorControllers
    Materials/    generated scene materials
  Scenes/         TestScene.unity (generated — safe to delete and rebuild)
Tools/
  blender/        the asset pipeline: model, rig, and animate everything from code
docs/             documentation (start here: docs/asset-pipeline.md)
```

## Documentation

- [Asset pipeline](docs/asset-pipeline.md) — how every model and animation is generated from Blender scripts
- [Player movement](docs/player-movement.md) — the HL1 physics model and how to tune it
- [Characters & animation](docs/characters-and-animation.md) — skeleton, skinning, clips, NPC driver
- [Weapons](docs/weapons.md) — crowbar, 9mm pistol, and how to add the next weapon
- [Combat & health](docs/combat-and-health.md) — IDamageable, HealthSystem events, health bars, death/respawn
- [Audio](docs/audio.md) — the procedural SFX pipeline and G1Audio API
- [Observability](docs/observability.md) — F3 telemetry HUD, soldier AI gizmos, seeded arena presets
- [Scene builder](docs/scene-builder.md) — how the level is generated from code
- [Story bible](docs/story.md) — the Corvus Annex, the Threshold event, chapters, characters
- [Architecture](docs/architecture.md) — full technical spec
- [Art bible](docs/art_bible.md) — art direction and asset list

## Roadmap

- ~~Firearms (pistol, SMG, shotgun, magnum)~~ ✓ · ~~audio~~ ✓ · ~~death/respawn~~ ✓ · ~~Level 1~~ ✓
- Health & ammo pickups, grenades, flashlight
- Checkpoints, main menu, settings (sensitivity/FOV)
- Ambience + music, footsteps
- Level 2 (outdoor escape, HECU helicopter) → Level 3 (Xen portal, boss)
