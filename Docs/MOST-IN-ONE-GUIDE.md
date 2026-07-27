# MOST IN ONE — Reference Guide

Everything learned about the `Assets/Most In One/` package (Solo Player), for building Go Balance games
from it. See project context in the memory index for *why* this asset is here and what constraints Go
Balance games need to satisfy — this doc is the asset reference itself.

Source of truth: `Assets/Most In One/ReadMe.txt`, `License.txt`, and the actual `.cs` scripts — every claim
below was either read directly from a file or confirmed by opening the scene live in the Editor and
inspecting its root GameObjects/components. Verification status is marked per template.

## What it is

Two layers:

1. **Systems** (`Common/MOST/Common MOST/*.cs`) — 20 self-contained, single-file, drag-and-drop components.
   Each works standalone on any GameObject in any project; no other MOST files required (per the
   publisher's own README: "copy/paste any MOST system to your game... just remove MOST namespace").
2. **Templates** — 12 category folders, each a playable demo built from the Systems. Reference
   implementations meant to be forked, not shipped as-is.

Full official docs: https://solo-player.gitbook.io/most-in-one/

## Systems reference

| System | File | Purpose |
|---|---|---|
| MOST_Action | `Common MOST/MOST_Action.cs` | Drag-and-drop trigger → `MA_*` action chain. The no-code event layer. |
| MOST_Aim | `Common MOST/MOST_Aim.cs` | Aim-at-target logic (used by NinjaMode's shuriken throw, gun aiming). |
| MOST_Controller | `Common MOST/MOST_Controller.cs` | Generic input controller base. |
| MOST_Damage | `Common MOST/MOST_Damage.cs` | Health/damage system (used by Pistol runner, Strong walls). |
| MOST_Detector | `Common MOST/MOST_Detector.cs` | Line-of-sight / proximity detection, built on `Detector_Core.cs`. |
| MOST_FreeMovement | `Common MOST/MOST_FreeMovement.cs` | Continuous/analog movement (Dodge, NinjaMode). |
| MOST_Grab | `Common MOST/MOST_Grab.cs` | Grab/carry mechanic (Controls/Grab demo). |
| MOST_GridMovement | `Common MOST/MOST_GridMovement.cs` | Discrete grid-cell movement (Cubey, and the Grid family generally). |
| MOST_ProjectileGenerator | `Common MOST/MOST_ProjectileGenerator.cs` | Bullet/projectile spawning, paired with `ProjectileMovement.cs`. |
| UniversalGameManager | `Common MOST/UniversalGameManager.cs` | Shared win/lose + menu-panel manager every template wires into. See below. |
| MOST_AudioManager | `Core/MOST_AudioManager.cs` | Central audio playback. |
| MOST_Database | `Core/MOST_Database.cs` | Save/load key-value persistence (Shop purchases, Dodge score). |
| MOST_Editor | `Core/MOST_Editor.cs` | Editor tooling: auto-syncs `HAS_INPUT_SYSTEM_PACKAGE` define on load/recompile. Menu: `Tools/MOST/Refresh Input System Define`. |
| MOST_HapticFeedback | `Core/MOST_HapticFeedback.cs` | Mobile haptics, with an iOS post-process step (`Core/Editor/MOST_HapticFeedback_iOS-PostProcess.cs`). |
| Detector_Core | `Core/Detector_Core.cs` | Shared raycast/overlap core behind MOST_Detector and MOST_Hide. |
| DatabaseSaver | `Core/DatabaseSaver.cs` | Persistence backend for MOST_Database. |

**MA_\* actions** (`Core/Actions/*.cs`) — the building blocks `MOST_Action` chains together. Confirmed present:
`MA_AudioSource`, `MA_Burst`, `MA_CameraFollow`, `MA_CameraShake`, `MA_Collision_Trigger`, `MA_DataDisplay`,
`MA_Destroy`, `MA_Events`, `MA_GrayShift`, `MA_HDRGlow`, `MA_HapticFeedback`, `MA_PositionAnimation`,
`MA_RandomSelect`, `MA_RotationAnimation`, `MA_ScaleAnimation`, `MA_SceneManage`, `MA_Spawner`, `MA_Swap`,
`MA_UpdateData`. All routed through a shared `MA_Core.cs`.

**UniversalGameManager** — every template scene reports into this for win/lose. Key concept: `ObjectsJump`
struct — named UI panel entries with a `TargetObject`, a `Listener` button, and a `DeactivateAllOnEnable`
flag, referenced by name from `OnWinMenuName`/`OnLoseMenuName`. Levels can be stored as `Prefabs`, `Scenes`,
or `ScriptableObjects` (`LevelStoringType` enum) — the ScriptableObject path is what `RunnerLevelProfile`/
`GridLevelProfile` use, and is the right lever for "new level" without touching the scene.

**Not part of Common/MOST but load-bearing:** `ForwardMovement.cs` lives in `Controls/Scripts/` (not
Common) but is reused by Runner's `EpicRoadLevelPlayer.PlayAll()` alongside `MOST_Spawn` and
`WalkEnemyManager` — worth knowing it's not actually a "MOST system," just a shared utility script.

## Template catalog (12 categories, 54 scenes)

Legend: **✓ live-verified** (scene opened, root GameObjects and/or full script logic read) · *(disk-read)*
not yet opened live.

### Runner — ✓ shoot + crowd-clone runner, not a plain endless runner
`Runner/Scenes/{EpicRoad, EpicRunner, BigBrain, Pistol, Count, BigBrain/Levels/*, Count/Levels/*}.unity`

All four main modes share `MOST_RoadMovement` for lane movement. Two distinct character controllers:

- **`CharacterControl_ShootRunner`** (EpicRoad, EpicRunner) — road movement + crowd-cloning
  (`NumberOfChilds`/`StartAmount` fields, "Crowd Settings" header) + shooting. EpicRoad's root hierarchy
  includes an `EpicRoad_Level 1` level-container object that EpicRunner's doesn't — level-based mode
  (EpicRoad) vs. endless/procedural mode (EpicRunner) looks like the actual difference between the two,
  not the core mechanic.
- **`CharacterControl_Gun`** (Pistol) — pure shooter runner, no crowd-cloning. Spawns a `Bullet` prefab from
  a `SpawnPoint`, uses `MOST_Damage` for the target/character.
- **`CharacterControl_BigBrain`** — head-scale gate runner. `MOST_Gate.Calculation()` grows/shrinks
  `CharacterHead`'s scale via `ScaleController()`; enemy-layer collisions shrink it further (layers 7/8/9 =
  -15/-25/-40). Not a quiz — the "brain" is literally the character's scaling head.
- **`CharacterControl_Count`** *(disk-read)* — a `_Child` variant script exists too
  (`CharacterControl_Count_Child.cs`); presumably a number-collecting counter runner, unconfirmed live.

Editor tooling: `Runner/Scripts/Editor/RunnerLevelProfile.cs` + a `Profiles/` folder — ScriptableObject
level data, the actual lever for building new Runner levels.

### ThreeRoads — ✓ 3-lane crowd runner
`ThreeRoads/Scene/3Road.unity`

`CharacterControl_TRoad` — `LaneIndex` (0=Left/1=Mid/2=Right) + `OnRightSide` rail position.
Enemy-layer/tag triggers **remove** a follower, `AddCharLayer`/`AddCharTags` triggers **add** one — a
clone-army/crowd accumulation mechanic (closer to "Count Masters" than a branching-choice game).
`LockZToController` ties forward progress to a shared `ThreeRoadsController`.

### Grid — three genres sharing one grid-movement core
`Grid/Scene/{BlockBlast Classic, BlockBlast Adventure, BlockBlast MainMenu, Cubey, Cubey Levels/*, Splat,
Splat Levels/*}.unity`

- **BlockBlast** ✓ (Classic scene opened; roots = `Background`, `Tray` — confirms the drag-piece-from-tray
  mechanic). `BlockBlastGame.cs` implements classic 1010!/Block-Blast line-clear scoring with a
  `MultiLineStage` combo system (multi-line clears in one move get a score multiplier + flat bonus).
  Adventure and MainMenu variants exist but weren't diffed against Classic — likely a level-progression
  wrapper and a menu shell around the same `BlockBlastGame` core, unconfirmed.
- **Cubey** ✓ (opened; generic `Main`/`Objects` roots, no contradictions) — `CharacterControl_Cubey.cs`
  drives grid-cell movement via `MOST_GridMovement`.
- **Splat** ✓ (opened; generic `Main`/`Objects` roots) — **no dedicated controller script** of its own;
  driven by the same `MOST_GridMovement`/`GridLevelData`/`GridLevelProfile` core as Cubey, themed with a
  "Block Splat" material and its own level-prefab set (`Splat Levels/`). Likely a paint/coverage win
  condition (cover all cells) layered on the shared grid system rather than unique code — unconfirmed in
  detail.

Shared Grid infra: `GridLevelData.cs` (per-cell state: Empty/Occupied/Blocked, `CellStates` byte array,
supports XY or XZ plane), `GridLevelProfile.cs`, `ShapeLiberary.cs` (piece shape definitions). Editor tool:
`Grid/Scripts/Editor/BlockSpriteGeneratorEditor.cs` (`Tools/MOST/Block Sprite Generator`).

### Dodge — ✓ near-miss bull-dodge arcade
`Dodge/Scenes/Dodge.unity`

`CharacterControl_Dodge.cs` — continuous movement (`MOST_FreeMovement`), tracks `DodgedEnemies` (literally
referred to as "Bull dodge levels" in the code/tooltips) with a per-level `ScoreForLevel` array — a
near-miss scoring system (closer you dodge, more points), not just survival time. Uses `MOST_Database` for
score persistence.

### Hide — ✓ corrected: camera-occlusion utility, NOT a hiding/stealth game
`Hide/Scene/Hide.unity`

`MOST_Hide.cs` — raycasts from a `Watcher` to a `Watched` transform; any geometry on `OccluderLayers`
blocking that line goes transparent (via `MaterialSwap` or `PropertyBlock` alpha override) until it no
longer blocks. This is the classic 3rd-person "camera sees through the wall" trick, not a
player-hides-from-enemy mechanic. Good first fork for camera work, not for a stealth game concept.

### Strong — ✓ physics-impact wall-breaking
`Strong/Scene/Strong.unity`

`DynamicWallControl.cs` — walls have `Health`/`Strength`/`Flixibility` (sic) fields; `OnCollisionEnter`
computes `impact = relativeVelocity² × ActionMultiplier × mass`, subtracts from `Health` if it exceeds
`Strength`. Real physics-based destruction, not scripted animation.

### Gradient — ✓ pure shader showcase, not a distinct gameplay genre
`Gradient/Scene/Skyscraper.unity`

**No dedicated `.cs` script exists for Gradient at all.** The scene (roots: `Main`, `Moving`, `Bird`) exists
specifically to demo the custom gradient shader on a vertical "skyscraper" climb — movement/animation is
almost certainly generic (`MA_PositionAnimation` or similar), not unique code. Treat this template as a
visual asset (the shader) with a scene wrapper, not a gameplay template to fork.

Shader conversion note: ships as Built-in RP by default; RunnerPac runs URP 17.0.4. URP variant is bundled
as `.bytes` inside `Gradient/Shaders/` — strip the `.bytes` extension to activate it, reassign on affected
materials. Steps: `Gradient/Shaders/HowTo_URP.txt` →
https://solo-player.gitbook.io/most-in-one/general/how-to-convert-renderer-from-srp-to-urp

### Shop — ✓ character/skin store, two modes off one system
`Shop/Scenes/{Shop Chars, Shop Skins}.unity`

`MOST_Shop.cs` — `ShopItem` struct (price, `isIAP`/`isFree`/`isPurchased`, `controlledGameObjects`,
`materials`) + `ShopType` enum: `GameObjectControl` (swap character models — Shop Chars) vs `CustomEffect`
(swap materials/skins — Shop Skins). One system, two scenes = two configurations of the same shop, not
different code. `CustomShopEdits.cs` and `ShopMenusControl.cs` round out UI wiring;
`SharedMaterialControl.cs` handles the skin-swap rendering.

### Controls — *(disk-read)* mechanic-demo library, not full games
`Controls/Scenes/{Auto, Grab, Lots of Rays, Multiply, Ninja, Legacy/*, Mini/*}.unity`

Twelve input/mechanic demos, not games. Scripts confirmed present: `BallBehavior.cs`, `CharsJump.cs`,
`DamageAmountSetter.cs`, `EnemyControl.cs`, `ForwardMovement.cs`, `HealthBar.cs`, `NinjaMode.cs`,
`ProjectileMovement.cs`. `NinjaMode.cs` read directly — mode-switches between Katana (melee, via
`MOST_FreeMovement`) and Shuriken (throw, via `MOST_Aim`), a Fruit-Ninja-style slice/throw toggle. Most of
this category is touch-gesture-first (drag, grab, slice) — poor fit for Go Balance's arrow-style input
except where a demo turns out to be direction-driven like Ninja's movement half.

### Spawn — *(disk-read)* spawner-pattern library, not full games
`Spawn/Scenes/{Explosion, Mini/{3D, Pattern, Point, Random, Spawn_Map}}.unity`

`MOST_Spawn.cs` is the core system (referenced everywhere else too — e.g. Runner's `PlayAll()` enables all
`MOST_Spawn` instances in a scene). `ExplosionBomb.cs`/`ExplosionLine.cs` are spawner-triggered effect
scripts. Building-block library for wave/pattern spawning, not a game on its own.

### HapticFeedback — *(disk-read)* feature test scene, not a game
`HapticFeedback/Scenes/HapticFeedback.unity` — `HapticsExample.cs`, a demo harness for `MOST_HapticFeedback`.

## Config / setup notes specific to RunnerPac

- **URP shader conversion needed** — see Gradient section above. Only known outstanding item.
- **Input System define auto-syncs** — `Tools → MOST → Refresh Input System Define` runs on load/recompile;
  RunnerPac has `com.unity.inputsystem` installed so `HAS_INPUT_SYSTEM_PACKAGE` is already live.
- **TextMesh Pro + Post Processing** — both required; `com.unity.postprocessing` was auto-added to
  `manifest.json` during import.
- **Import overwrote Quality/Player/Tag Manager/Build settings** — per the asset's own README warning,
  worth diffing if anything in project settings looks off post-import.

## License

"You can't redistribute the code or any of the asset's content... feel free to use all scripts, images,
sounds and all assets included in your games." — fine to ship games built from this to Go Balance; don't
hand the raw package/source to another team or platform. Third-party sub-licenses live in
`Common/Third-Party/` (TextMesh Pro, fonts, Quick Outline) with their own license files.

## Playbook — turning a template into a shippable Go Balance game

1. Duplicate the closest-fit template scene into your own `Assets/Games/<name>/` — never edit the source
   template in place, keep it as a clean reference.
2. Decide up front: ship the stock art, or fully reskin? (Both are valid per-game — see project memory.)
   Swapping character/enemy/environment prefabs alone already reads as a different game.
3. Re-tune via the `*LevelProfile` ScriptableObject, not the scene — that's the intended lever for new
   levels/difficulty.
4. Reconfigure `MOST_*` component fields on the controller object (speed, gravity, detection range, damage
   — all exposed Inspector fields, per the publisher's "copy/paste, remove the namespace" design intent).
5. For new behavior not covered by a field tweak, compose existing `MA_*` actions via `MOST_Action` before
   writing new script.
6. Wire win/lose through the existing `UniversalGameManager` — register new panels in its `ObjectsJump`
   list rather than building a new manager.

## Go Balance input fit

No multiplayer/networking needed anywhere in this project — single-player only, confirmed. Go Balance's
physical device eventually drives input as Bluetooth-simulated directional presses (arrow-key-like); that
wiring happens later in a separate export/integration project, not here. Both continuous and discrete
movement are acceptable inputs to design around; the real disqualifier is touch-gesture-first mechanics
(drag-to-place, precision aim, multi-touch), which several Controls demos and BlockBlast rely on.

**Good fit, ready to start from:** Runner (EpicRoad/EpicRunner/BigBrain/Pistol), ThreeRoads, Grid/Cubey, Dodge.
**Poor fit as-is:** BlockBlast (drag/tap placement), most of Controls, Hide (not a game).
**Not gameplay templates:** Gradient (shader showcase), Spawn/HapticFeedback (mechanic libraries).
