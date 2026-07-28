# EpicRoad Runner — Core Game PRD

## Context

Turning the `EpicRoad` template (see `Docs/MOST-IN-ONE-GUIDE.md`) into a standalone Go Balance game. The
template as shipped is one hand-placed level (~19 objects, a 35-cycle-capped enemy spawner) — a good
mechanic demo, not a game. This PRD is scoped to the **core game only**: no IAP, no currency, no shop, no
meta-progression, no unlock trees. One goal — a kid picks it up, plays a run, and wants to immediately play
another one, and the run itself keeps getting more interesting as it goes.

Everything below leans on systems that already exist in the asset (see Systems Mapping) — this is a
content and tuning problem, not a new-mechanics problem. Genre patterns referenced below are standard for
this exact subgenre (crowd/gate runner-shooter — the "Blob Runner 3D" / "Count Masters" / "Ball Run 2048"
family), not invented for this doc.

## Core fantasy

You're a small squad that grows as you run. Every gate is a snap decision — grow your squad, upgrade your
gun, or lose ground — and the run gets harder and louder the further you get.

## Core loop (already exists)

1. Character auto-runs forward (`MOST_RoadMovement`); player steers left/right within road bounds.
2. Squad auto-fires forward continuously (`CharacterControl_ShootRunner` + `CharacterControl_ShootRunner_Child`).
3. Player weaves through `MOST_Gate` decision points — each gate is Add/Subtract/Multiply/Divide against a
   stat (`GateType`: Health, FireRate, FireRange, Upgrade, AddChilds). Wrong lane = worse stat.
4. Squad encounters enemies: static clusters (`EnemyZoneControl`, circular spawn formation) and pursuers
   (`WalkEnemyManager`) — squad auto-fires through them, losing members on hits, per `MOST_Damage`.
5. Run ends on death or reaching the end. `EndGameUIHandler` tallies score × multiplier from
   `MOST_Database` with a count-up animation.
6. Immediate retry — no menu friction.

This loop needs zero new mechanics. It needs **length, variety, and a difficulty curve**, which is what
the rest of this doc is about.

## What makes a run "get more interesting" (the actual ask)

Four escalation levers, all standard for the genre, all buildable from existing fields — no new systems:

1. **Speed curve.** `MOST_RoadMovement.ForwardSpeed` increases with distance traveled (simple curve, e.g.
   AnimationCurve or lerp over distance). The single biggest "this run feels different from minute 1"
   lever in the genre, and it's one exposed field.
2. **Power curve via existing gate types.** `CurrentLevel` on `CharacterControl_ShootRunner` already drives
   `ChildPrefabs[]` — i.e. your squad's *visual tier* already upgrades automatically as you level up through
   `Upgrade`-type gates, and `UpgradeGateHelper` already swaps weapon-tier visuals. This is a genuinely
   satisfying already-built progression hook — it just needs enough `Upgrade` gates placed across a long
   run to have somewhere to go (currently there's ~1-2 in the whole template).
3. **Encounter variety, introduced progressively.** Don't show every enemy/gate type in the first 10
   seconds. Standard genre pattern: early run = simple `AddChilds` gates + small `EnemyZoneControl`
   clusters only; mid run = introduce `WalkEnemyManager` pursuers + `FireRate`/`FireRange` gates; late run =
   larger clusters, denser gate decision chains (multiple gates back-to-back forcing a real choice, not a
   freebie). This is purely a content-authoring order, not new code.
4. **A finale beat.** The asset already has an `EndLine EpicRoad.prefab` / `EndLineScore.prefab` (see
   `Runner/Prefabs/BuilderProfile prefs/EpicRunner/`) — genre standard is a visibly bigger final
   encounter right before it (a "boss cluster" sized to reward however big your squad got), so the run has
   a climax instead of just stopping.

## Structure: level-based, not infinite/endless

Decided: **short curated levels, not a true endless/infinite runner.** Reasoning:

- Pure endless (Subway Surfers-style survive-forever) only feels complete when paired with revives/ads —
  ruled out per scope. Without those, "you just died, no win, try again" is a weaker loop than a real
  finish line.
- This exact subgenre's real-world peers (Blob Runner 3D, Count Masters, Ball Run 2048) are themselves
  level-based, not endless — short (30-90s) curated runs with a hand-paced difficulty arc and a real
  boss/finish moment each, then the next level unlocks. That's a genuine "I beat it" moment with zero
  monetization required.
- The asset's own publisher already builds it this way: `Runner/Scenes/Levels/BigBrain/#1-3` and
  `Levels/Count/#1-3` are pre-baked, curated levels built at edit-time with `RunnerLevelGenerator` — not a
  runtime-generated infinite stream. EpicRoad should follow the same pattern its siblings already use.
- "Next level unlocked" is itself enough sequencing to feel like progression — no currency/meta needed for
  it, it's just level N+1 becoming playable.

An **optional Endless Mode after all curated levels are cleared** is a natural, low-effort bonus for later
(reuses the same generator output, no new pipeline) — not required for v1, noted only so the curated-level
work doesn't accidentally block it.

### Refinement: variant pools, not one static bake per level

Baking each level exactly once (literally what BigBrain/Count's 3 levels each are today) means a failed
retry shows the *identical* layout every time — after 2-3 attempts it's memorized, not replayed, which
undercuts the whole "play more and more runs" goal. Fix, at no engineering cost: **each level slot is a
small pool of pre-generated variants**, not a single bake.

`RunnerLevelGenerator` already selects `Part`s by weighted random chance — running it multiple times against
the *same* difficulty config produces a *different* layout each time. So: "Level 3" = the same `Part`
weights/length config, run ~4 times, each result saved as its own scene/prefab. On play or retry, one of
those ~4 is picked at random. Losing and retrying means "Level 3, a different roll," not "Level 3, again."

This needs zero new code — it's the same generator button, pressed a few extra times per level. The
authoring cost is the same either way (defining each level's difficulty-tier `Part` config); this just
means running that config N times per slot instead of once.

## Content pipeline (the actual build item)

Because levels are level-based and baked at edit-time (exactly like BigBrain/Count already do, with the
variant-pool refinement above), this needs **no new runtime streaming system** — just more curated content
using the tool that's already there:

1. Use the asset's own **`RunnerLevelGenerator`** (`Runner/Scripts/Editor/RunnerLevelProfile.cs`) to build
   several EpicRoad levels the same way BigBrain/Count's levels were built — a weighted-random `Part` list
   (gates/enemies/encounters) repeated along the road, run ~4 times per level slot to produce that level's
   variant pool.
2. Each level's difficulty is authored directly in its `Part` weights/`IgnoreFirstLines` settings per the
   escalation tiers above (early levels = simple `AddChilds` gates + small `EnemyZoneControl` clusters;
   later levels = `WalkEnemyManager` pursuers, `FireRate`/`FireRange` gates, denser gate chains) — the
   weights/config stay fixed per level, only the random roll changes across its variants.
3. `UniversalGameManager` (`LevelStoringType.Scenes` or `.Prefabs`) sequences "level cleared → next level,"
   plus a random pick among the current level's variants on load — reuses existing win/lose wiring, no new
   manager needed.
4. `MOST_Spawn.CyclesType.LoopForever` isn't needed for this structure (each level is finite by design);
   leave spawners on `LimittedCycles`, just tuned per level instead of hardcoded at 35.

This is now a **content-authoring task**, not an engineering task — the one thing to actually build is a
proper `Part` list per difficulty tier for EpicRoad, generated a handful of times per level slot.

## Retention hook (kept intentionally minimal, per scope)

- Per-player/per-account best-score tracking is **the Go Balance platform's job**, not this game's — when
  this game gets plugged into the main Go Balance system later, that system owns persistent best-scores
  across accounts. Not built here, and not blocking core-game work now.
- For now, this game only needs to produce a single **run score value** at the end of a run
  (`EndGameUIHandler` + `MOST_Database` already do exactly this locally) so there's something to show on
  the death screen and something to hand off later. Treat the current local `MOST_Database` score as a dev
  stand-in, not the final persistence layer — don't invest effort making it fancier than that.
- No currency, no unlocks, no daily rewards regardless. `MOST_Gate.GateType.Currency` exists in the asset
  but is explicitly **not used** here — repurpose those gate slots as extra `AddChilds`/`Upgrade` gates
  instead.

## Systems mapping

| Feature | System | Status |
|---|---|---|
| Forward run + lane steering | `MOST_RoadMovement` | Exists, needs a speed-over-distance curve added |
| Squad growth/shrink, visual tiers | `CharacterControl_ShootRunner`, `ChildPrefabs[]` | Exists, needs full tier art set + more `Upgrade` gates in content |
| Gate math (add/sub/mul/div, 5 stat types) | `MOST_Gate` | Exists, fully usable as-is |
| Weapon-tier visual swap on level-up | `UpgradeGateHelper` | Exists, needs to be placed/wired per chunk |
| Enemy clusters | `EnemyZoneControl` | Exists, needs size/tier variants for early/mid/late pools |
| Pursuing enemies | `WalkEnemyManager` | Exists, unused in current level — introduce mid-run |
| Per-level spawning, tuned cycles | `MOST_Spawn` (`LimittedCycles`) | Exists, just needs per-level tuning instead of the current fixed 35 |
| End-of-run score tally | `EndGameUIHandler` + `MOST_Database` | Exists as-is (dev stand-in, see Retention hook) |
| Finale/end trigger | `EndLine EpicRoad.prefab` | Exists, needs a scaled-up encounter placed just before it, per level |
| Level content generation | `RunnerLevelGenerator` (editor tool) | Exists, needs a real `Part` list authored for EpicRoad (currently minimal — this is the actual work) |
| Level-to-level sequencing | `UniversalGameManager` | Exists, not yet wired for EpicRoad's multi-level flow |
| Run score value (dev stand-in) | `MOST_Database` | Exists as-is |

## Explicitly out of scope

No IAP, no currency/gems, no shop integration (`MOST_Shop` exists in the asset, not used here), no
character/skin unlocks, no daily rewards, no ads, no meta-progression between runs beyond the single
best-score number.

## Decisions made

- **Art:** ship with the stock MOST IN ONE art for this game, no reskin.
- **Structure:** level-based (see above), not infinite/endless.

## Open decisions before build starts

1. **Number of levels for v1** — genre rule of thumb for this subgenre is a first "world" of roughly
   8-12 short levels before it needs new content, matching how many BigBrain/Count already ship with (3
   each, as a smaller reference point).
2. **Individual level length** — 30-90s per level is the genre norm; exact target affects how much `Part`
   content each level needs.
