# EpicRoad — Level Design Plan

Plan for making levels *interesting* rather than merely harder. Written after playtest
feedback: L1–L2 good, L3 "okay but boring", L4–L6 boring, L7 way too hard.

## Why the levels are boring (root cause)

`RunnerLevelGenerator` picks each row **independently at random** from a weighted list.
That has three consequences, and they explain every complaint:

1. **No relationship between neighbouring props.** A charge-gate can sit in open road with
   nothing in front of it, so you can pour fire into it and inflate it enormously.
2. **No arc inside a level.** Barrel HP is one flat random range from first row to last, so
   minute three plays exactly like minute one.
3. **Levels differ only by numbers.** L4, L5 and L6 use the same structure with bigger
   values, so they read as the same level. "Boring" is really "statistically identical".

The generator cannot express ordering ("gate behind barrel") or position-dependent values
("HP rises along the track"). So the fix is not more tuning — it is a **design pass that
runs after generation** and rewrites the layout into something authored.

## The design rules

From playtest feedback, stated as rules the pass must enforce:

**R1 — Charge-gates sit immediately behind a barrel.**
`Gate_Childs_Big` charges *upward* when shot (`IncreaseAmount: +1`, cooldown 0.1). With a
clear line of fire you can pump it far too high. Putting a barrel directly in front means
the barrel eats your bullets first, so geometry — not an arbitrary cap — limits how much
you can charge it.

**R2 — Charge-gates start negative or barely positive.**
Initial value should rarely exceed **+2**, and should often be negative. A gate that starts
red is a decision: take the hit, or spend fire flipping it positive. Current values
(+3..+12 at L1) hand out troops for free.

**R3 — Barrel HP ramps along the level.**
First barrels cheap, later barrels expensive, scaled by distance along the track. Your
squad's rate of fire grows during a run, so flat HP means barrels get *easier* as you go.
Ramping HP keeps every barrel a real cost and gives the level an internal arc.

**R4 — Constant action, no dead road.**
No stretch longer than ~2 rows without an encounter. "Full but manageable" is the target:
pressure the whole way, never a lull.

**R5 — End the level fast once the last enemy dies.**
Currently ~4.4s of dead time: up to 0.4s detection + `DelayBeforeWinJump` 2s +
`NextLevelDelay` 2s.

**R6 — Enemies need clear air around gates and barrels.**
An enemy placed right after a barrel or gate is unreactable: your fire is committed to
the barrel and there is no distance left to shoot the enemy before it lands on you.
Minimum separation of ~1.25 rows between any enemy and any gate/barrel.

Two failure modes to avoid when enforcing this, both hit in practice:
- *Clumping.* Pushing each enemy forward until clear sends them all to the same first
  clear slot, producing a long empty stretch and then a wall of enemies. Search outward
  from each enemy's own position instead, and keep enemies apart from each other.
- *Silent give-up.* Dense levels may have no slot satisfying every constraint. Falling
  back to the original position re-creates the violation; pick the position with the
  largest clearance instead, so the result degrades gracefully.

## Where to change all of this

`Assets/Games/EpicRoadRunner/EpicRoad Build Settings.asset`

Select it in Unity and every knob is on one screen, per level: row spacing, track
length, run-up, enemy share, minimum gifts, crowd size, barrel cost ramp, gate value
range, max gap, enemy clearance. Press **Build All Levels** to re-roll everything,
apply the rules below, write the prefabs and point `UniversalGameManager` at them.

Tool code: `Assets/Games/EpicRoadRunner/Editor/EpicRoadLevelBuilder.cs`.

## Implementation: a post-generation design pass

New editor script, run after `GenerateLevel()` for each level, operating on the placed
props sorted by z. `MOST_Gate.SetGateValue(operation, value, refresh: true)` is public and
handles the legacy-text sync, so values can be rewritten safely.

| Step | Action |
|---|---|
| 1 | Sort all props by z; compute each one's progress `t` (0 at start, 1 at finish). |
| 2 | **Barrel ramp (R3):** every Barrel Children / Barrel Upgrade gets `HP = lerp(hpStart, hpEnd, t)`, jittered slightly. |
| 3 | **Gate pairing (R1):** for every charge-gate, ensure a barrel sits one row ahead in the same lane; if absent, relocate the gate behind the nearest barrel. |
| 4 | **Gate values (R2):** rewrite each charge-gate to a value drawn mostly from negatives, capped at +2. |
| 5 | **Density (R4):** scan for gaps > 2 rows with no enemy and insert one. |
| 6 | **Finale + endline:** already implemented — enemy crowd last, finish line 8 units after. |

This keeps the generator as a dumb layout source and puts all authored intent in one
reviewable place.

## Difficulty curve rework

Interest should come from structure, not from raw counts.

| Level | Change |
|---|---|
| L1–L2 | Keep. Approved in playtest. |
| L3–L6 | Same enemy growth, but steeper barrel-HP ramp, more gate decisions, tighter enemy spacing. These must stop being "L2 with bigger numbers". |
| L7–L8 | Pull the spike back. L7 at 85 enemies was too hard. Cap enemy growth and push difficulty into barrel HP and deeper red gates instead. |

## Timing changes (R5)

| Setting | Now | Proposed |
|---|---|---|
| `EpicRoadClearDetector.checkInterval` | 0.4s | 0.2s |
| `UniversalGameManager.DelayBeforeWinJump` | 2s | 0.5s |
| `UniversalGameManager.NextLevelDelay` | 2s | 1.0s |

Total after the last kill: ~4.4s → ~1.7s.

## Order of work

1. Timing changes (R5) — smallest, immediately felt.
2. Gate values (R2) — biggest single balance lever, no new code.
3. The design pass (R1, R3, R4) — the real fix for "boring".
4. Curve rework across L3–L8, then regenerate and re-verify.

Each step is its own commit so any of it can be reverted independently.

## Endgame: three variants, then a loop (L11–L13)

The run used to stop having new ideas around L8. Two separate causes:

1. **Levels 13+ were literally L12.** `UniversalGameManager.Loading()` picks its
   level with `LevelsPrefs[Mathf.Min(level, LevelsPrefs.Length) - 1]`. Past the
   last entry that clamps, so 13, 14 and 50 all loaded the same prefab
   byte-for-byte, forever.
2. **L11 and L12 were near-identical anyway.** They differed only by
   `RowSpacing` 6→7 and one extra wave group. Same rounds, same crowd size, and
   — the real problem — the same enemy placement.

### Placement was the missing lever

Every wave in every level was positioned with `g % 2 == 0 ? -6 : 6`: enemies
always on the shoulders, alternating, centre lane permanently free. No amount of
retuning round counts changes what that *feels* like, because the thing you
actually read while playing is where the enemies are.

`WaveShape` on each level spec now controls that:

| Shape | Layout | Reads as |
|---|---|---|
| `AlternateSides` | left, right, left, right — centre free | the original; L1–L10 keep it |
| `Pincer` | both shoulders at once, centre is the gap | walls you thread |
| `Sweep` | rolls across the lanes as a diagonal | a wave you track sideways |
| `Scatter` | deterministic pseudo-random lane per group | react, don't memorise |

`Scatter` hashes `(round, group)` rather than calling `Random`, so a level still
builds identically every time — builds have to be repeatable.

### The three variants

Same pressure, different shape. Wave enemy units are matched so none is harder:

| | L11 | L12 | L13 |
|---|---|---|---|
| Feel | few huge walls | steady medium waves | constant swarm |
| Rounds | 4 | 10 | 12 |
| Groups per wave | 8 → 13 | 5 → 9 | 8 → 14 |
| Crowd size | 20 | 12 | 6 |
| **Wave units** | **840** | **840** | **798** |
| Formation | Pincer | Sweep | Scatter |
| Row spacing | 8 (airy) | 5.5 | 4.5 (dense) |
| Track length | 205 | 190 | 175 |
| Barrel HP | 6 → 40 | 5 → 28 | 4 → 22 |
| Gates removed | 70% | 35% | 10% |

The knobs move together on purpose. L11 is sparse road, few enormous fights,
expensive barrels and almost no gates — slow and heavy. L13 is dense road, small
fights arriving constantly, cheap barrels and plenty of gates — fast and twitchy.
L12 sits between them. Difficulty is held flat while everything about the
texture changes.

### The loop

`EpicRoadLevelLoop` (execution order −500, so after `EpicRoadTestStartLevel` at
−1000 and before `UniversalGameManager` at 0) replaces `LevelsPrefs` with a
one-entry array holding the level that *should* play. `Mathf.Min(level, 1) - 1`
is always 0, so the manager loads that one whatever the level number is.

Levels 1–10 play once each, then 11, 12, 13 cycle forever: 14→L11, 15→L12,
16→L13, 17→L11. The saved counter is never touched, so the HUD keeps counting
up — level 14 still reads "14".

`LoopStart` is a field, so the size of the repeating pool is just how many levels
exist past it. Adding an L14 spec extends the cycle to four with no code change.
