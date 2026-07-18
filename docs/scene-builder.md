# Scene builder

![Overview](images/unity_overview.png)

`Assets/G1/Editor/G1SceneBuilder.cs` generates the entire test scene from code.
Menu: **G1 → Build Test Scene** (or headless:
`Unity -batchmode -quit -projectPath <repo> -executeMethod G1SceneBuilder.BuildScene`).

The scene file itself is disposable. If it's ever broken, delete it and rebuild.
This is deliberate: while the game is young, "the level" should be reproducible
data, not a fragile hand-edited asset. When real level design starts, that work
happens in its own scenes — the builder stays as the regression sandbox.

## What one run does

1. **Importer config** — sets both character FBX files to Generic rig, renames
   animation takes to `Idle`/`Walk`, marks them looping.
2. **AnimatorControllers** — `Assets/G1/Anim/{Protagonist,Villain}.controller`,
   two states each, Idle default.
3. **Materials** — flat Standard-shader materials in `Assets/G1/Materials/`
   (concrete, floor, hazard orange, crate wood, door steel). Idempotent: re-runs
   update colors in place.
4. **Lighting** — warm directional sun + flat grey ambient + linear fog (25–80 m),
   the retro-industrial mood in three lines.
5. **Arena** — 32×32 m walled floor, four hazard-striped pillars, six breakable
   crates (one stack of three), a sliding door with frame.
6. **Player** — CharacterController (1.8 m, r 0.4) + `PlayerMovement` +
   `MouseLook` + `PlayerUse` + camera at 1.62 m + `WeaponHolder` with the crowbar
   viewmodel (colliders stripped).
7. **NPCs** — protagonist patrolling a 6×6 waypoint rectangle (Walk), villain
   standing at (−4, 2) facing the player spawn (Idle), both with capsule colliders
   so the crowbar raycast can find them later.
8. Saves `Assets/Scenes/TestScene.unity` and registers it in Build Settings.

## Headless verification

`G1Screenshot.Snap` (also an `-executeMethod` target) opens the test scene and
renders two PNGs — an overview and a player-POV shot — without opening the editor
UI. Useful for checking asset changes from CI or scripts:

```bash
Unity -batchmode -quit -projectPath <repo> \
      -executeMethod G1Screenshot.Snap -logFile snap.log
```

(Output paths are currently hardcoded at the top of `G1Screenshot.cs` — point them
somewhere in your working tree.)

## Extending

Add new props/systems inside `BuildArena()` or a new `Build*()` step. Keep the
idempotency rule: creating an asset checks whether it already exists; scene objects
are always built into a fresh empty scene so re-runs never duplicate.
