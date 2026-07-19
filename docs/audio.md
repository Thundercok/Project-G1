# Audio

The entire soundscape is **procedurally synthesized** — no recorded samples, no
asset packs. Twelve retro one-shots (~150 KB total) are generated from pure
math by [`Tools/audio/generate_sfx.py`](../Tools/audio/generate_sfx.py)
(Python stdlib only: `wave`, `math`, `random` — no numpy) into
`Assets/Resources/Audio/`, and played through a pooled runtime helper.

## Clip inventory

| Clip | Recipe | Used by |
|---|---|---|
| `fire_pistol` | filtered noise burst + 210 Hz body | G1Pistol |
| `fire_smg` | shorter, brighter burst | G1Smg |
| `fire_shotgun` | slow-decay noise + 85 Hz thump | G1Shotgun |
| `fire_magnum` | 62 Hz boom + crack | G1Magnum |
| `swing` | band-limited noise with mid-swing bump | Crowbar |
| `hit_thunk` | decaying 105 Hz sine + click | Crowbar impact |
| `crate_break` | crackle spikes over noise | Breakable death |
| `player_hurt` | falling square-ish grunt | HealthSystem (player), death groan (pitched down) |
| `enemy_death` | descending saw + noise tail | HealthSystem (NPCs) |
| `door_servo` | wobbling 88 Hz hum | SlidingDoor |
| `pickup` | two rising sine blips | G1WeaponPickup |
| `horde_roar` | AM-modulated low growl | ThreatDirector horde events |

Regenerate (deterministic — seeded RNG) after tweaking a recipe:

```bash
python3 Tools/audio/generate_sfx.py
```

Note: `*.wav` is Git-LFS-tracked (like all binary media in this repo).

## Runtime API — `G1Audio` (`Scripts/Core/G1Audio.cs`)

Static, zero-setup: a 14-source pool bootstraps on first call and survives
scene reloads (`DontDestroyOnLoad`). Clips are cached after first
`Resources.Load`.

```csharp
G1Audio.Play("hit_thunk", hit.point, 0.8f);   // 3D positional (world events)
G1Audio.Play2D("fire_pistol", 0.7f);          // flat (player-owned sounds)
```

Every call applies a small random pitch jitter (±6 % / ±4 %) so rapid repeats
(SMG fire, horde deaths) don't sound like a machine stamping the same sample.

**Conventions:** the player's own gun and hurt sounds are 2D (they're "in your
head", HL1-style); everything happening in the world is 3D positional with
linear rolloff, audible to ~45 m.

## Adding a sound

1. Add a recipe block to `generate_sfx.py` (compose the primitives: `noise`,
   `sine`, `lowpass`, `env_exp`) and run it.
2. Call `G1Audio.Play/Play2D("your_clip", ...)` from the gameplay code.

That's it — no importer setup, no mixer wiring (an Audio Mixer with SFX/
Ambient/Music buses is planned once music and ambience exist).
