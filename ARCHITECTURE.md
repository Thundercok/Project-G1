# PROJECT G1 — ARCHITECTURE & AGENTIC GUIDE

Welcome to **Project G1**, a fast-paced, high-octane retro FPS built in Unity (GoldSrc/Quake lineage).

This document serves as the authoritative map of the codebase for human developers and AI coding agents.

---

## 📁 1. Directory Structure & Namespace Map

```
Assets/G1/
├── Anim/                 # Weapon & Character Animator Controllers
├── Editor/               # Procedural Level & Scene Builders (G1SceneBuilder, G1HugeMapBuilder, G1CampaignBuilders)
├── Materials/            # Project Materials & Textures
├── Models/               # Character & Weapon FBX Models (Protagonist, Villain, Firearms)
├── Prefabs/              # Game & Entity Prefabs
├── Resources/            # Audio & Font Resources (ShareTechMono, G1Audio SFX)
├── Scenes/               # Unity Scenes (MenuScene, MainScene, Level1, Level2, Level3, HugeMap)
└── Scripts/              # C# Logic divided into 6 modular domain folders:
    ├── Core/             # HealthSystem, G1Difficulty, G1ObjectiveManager, G1TutorialSystem, G1SaveSystem, G1Audio
    ├── Interaction/      # SlidingDoor, Breakable, IUsable
    ├── NPC/              # G1SoldierAI (F.E.A.R. GOAP), G1FactionFighter, G1Ally, G1AlienAI, G1ZombieAI, Ragdoll Physics
    ├── Player/           # PlayerMovement (Quake bhop), MouseLook, PlayerUse, CameraEffects
    ├── UI/               # PlayerHUD, ArenaDebugHUD, G1SettingsPanel, G1MainMenu, G1StoryCard
    └── Weapons/          # WeaponBase, G1Pistol, G1Shotgun, G1Smg, G1Magnum, G1WeaponFX (Tracers), Damped Spring Physics
```

---

## ⚡ 2. Core Game Rules & Agentic Conventions

When contributing code or modifying features in Project G1, **always follow these conventions**:

### A. Cutscene HUD Suppression
All `OnGUI()` interfaces (e.g. `PlayerHUD.cs`, `ArenaDebugHUD.cs`, `G1TutorialSystem.cs`) MUST check `IsCutsceneActive()` and return early during cutscenes to prevent text overlap:
```csharp
if (G1IntroStory.IsActive || G1EndingCutscene.IsPlaying || (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive))
    return;
```

### B. Hitscan Ballistics & Visual Bullet Tracers
All firearms (`G1Pistol`, `G1Shotgun`, `G1Smg`, `G1Magnum`) and combat AI (`G1SoldierAI`, `G1NPCCombat`) use instant `Physics.Raycast` hitscan coupled with 0.04s `G1WeaponFX.PlayTracerBeam(start, end, color)` for visual feedback. Do NOT replace with floating 3D sphere projectiles.

### C. Difficulty & Active Recovery Mechanics (`G1Difficulty.cs`)
Difficulty tuning lives strictly in `G1Difficulty.cs`:
- **Mode 0 (Casual Action)**: `0.6x` incoming damage, `2.0x` player damage, +15 HP & +10 Armor per enemy kill siphon reward.
- **Mode 1 (Tactical Easy)**: `0.8x` incoming damage, `1.4x` player damage, +10 HP & +5 Armor per kill.
- **Mode 2 (Normal)**: `1.0x` standard Half-Life 1 difficulty.

### D. Human Reaction Delay & Door Ambush Buffer
- `G1SoldierAI` enforces a `0.65s - 0.80s` reaction time delay upon first spotting the player, accompanied by an HECU radio bark callout ("CONTACT!") and a telegraphed red laser sight line.
- `SlidingDoor.GlobalDoorOpeningGraceTime` provides a 0.65s sightline grace buffer when doors slide open.

---

## 🛠️ 3. In-Game Sandbox & Testing Hotkeys

| Key | Function | File Source |
|---|---|---|
| **`G`** | Toggle God Mode (Invincibility) | [HealthSystem.cs](file:///Users/thundercock2/Documents/Rockstar%20Games/Project%20G1/Assets/G1/Scripts/Core/HealthSystem.cs) |
| **`V`** | Toggle 3D Fly / Noclip Mode | [PlayerMovement.cs](file:///Users/thundercock2/Documents/Rockstar%20Games/Project%20G1/Assets/G1/Scripts/Player/PlayerMovement.cs) |
| **`H`** | Heal + Refill Ammo + Unlock All Weapons | [HealthSystem.cs](file:///Users/thundercock2/Documents/Rockstar%20Games/Project%20G1/Assets/G1/Scripts/Core/HealthSystem.cs) |
| **`TAB`** | Open Mob Spawner & Instant "Kill All" Button | [G1MobSpawnerToolbox.cs](file:///Users/thundercock2/Documents/Rockstar%20Games/Project%20G1/Assets/G1/Scripts/Core/G1MobSpawnerToolbox.cs) |
| **`F3`** | Toggle Real-Time Telemetry & AI Debug Overlay | [ArenaDebugHUD.cs](file:///Users/thundercock2/Documents/Rockstar%20Games/Project%20G1/Assets/G1/Scripts/UI/ArenaDebugHUD.cs) |

---

## 📦 4. Blender Automation Pipeline

Blender scripts live in `Tools/blender/`:
- `rig_character.py`: Rigging & NLA animation export (`Idle`, `Walk`, `Run`, `CombatIdle`, `Attack`, `Death`).
- `build_huge_map.py`: Geometry generation for the 600x600m Corvus Sprawl battlefield.
- Run via Blender headless CLI: `blender --background --python Tools/blender/<script>.py`
