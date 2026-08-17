using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Ends the run as soon as the level is actually cleared - nothing left
    // alive and nothing left heading toward the player - instead of waiting
    // for the finish line to physically travel into the character.
    //
    // The finish line still works and is left in place as a fallback: if this
    // never reaches zero (a stray enemy that somehow neither dies nor passes),
    // the endline still ends the run.
    //
    // ---------------------------------------------------------------------
    // WHY THIS DOES NOT READ HEALTH. An earlier version of this idea polled
    // MOST_Damage.IsDefeated() and produced instant false wins. Two reasons,
    // both still true:
    //   1. MOST_Damage.Health is 0 on a freshly instantiated object and only
    //      becomes MaxHealth inside its own Start(), a frame later - so on the
    //      first tick every enemy reads as already dead.
    //   2. A destroyed MOST_Damage reference also reads as defeated, so
    //      anything that removes an enemy for non-combat reasons silently
    //      completes the level.
    // This version never looks at health at all. It only asks whether the
    // object still exists, is active, and is still a threat by position -
    // which is true regardless of initialisation order.
    // ---------------------------------------------------------------------
    public class EpicRoadClearDetector : MonoBehaviour
    {
        [SerializeField] UniversalGameManager manager;
        [SerializeField] CharacterControl_ShootRunner character;

        [Tooltip("Seconds between clear checks. Kept short so the level ends promptly " +
                 "once the last enemy dies rather than lingering on empty road.")]
        [SerializeField] float checkInterval = 0.2f;

        [Tooltip("Delay after the run starts before checking at all.")]
        [SerializeField] float graceSeconds = 1.5f;

        [Tooltip("A gate is still 'incoming' only while it is at least this far ahead of the player.")]
        [SerializeField] float aheadMargin = 1f;

        bool _fired;
        bool _sawContent;

        // Enemies that have already drawn level with the player at least once.
        //
        // This is what fixes the 30-90s hang at the end of a run. The aggro trigger
        // (CheckPoint, layer 10) sits at z = -28, BEHIND the player at z = 0, so an
        // enemy only ever wakes up after it has already scrolled past you. It then
        // chases you forward, back into z > playerZ + aheadMargin, where a purely
        // positional test counts it as "incoming" again - forever, because bullets
        // spawn on the squad and travel forward only, so an enemy sitting level with
        // the squad can never be shot. Two such enemies were observed alive for 30+
        // seconds while the level refused to end.
        //
        // Once an enemy has been level with you, it is behind you conceptually no
        // matter where it drifts to. It is not content you have yet to face, so it
        // must never hold the level open again.
        readonly HashSet<WalkEnemyManager> _passed = new HashSet<WalkEnemyManager>();

        // Closest this enemy has ever been, and when it last got closer.
        //
        // An enemy only deserves to hold the level open while it is still COMING AT
        // YOU. Measured in play, the survivors at the end of a run sit at a dead-flat
        // 11 units for seconds on end: they are aggro'd and walk toward the player at
        // almost exactly the speed the road drags them back, so they hover forever.
        // A pure position test ("is it in front of me?") says yes to those enemies
        // for the rest of the level, and since bullets only travel straight forward,
        // one parked in a lane the squad is not in can never be shot. That is the
        // empty road you stand on waiting to be told you won.
        //
        // So: if an enemy has not got measurably closer in StuckSeconds, it is not
        // incoming, it is parked. It stops counting, permanently.
        readonly Dictionary<WalkEnemyManager, float> _closest = new Dictionary<WalkEnemyManager, float>();
        readonly Dictionary<WalkEnemyManager, float> _closestAt = new Dictionary<WalkEnemyManager, float>();

        [Tooltip("An enemy that has not got any closer for this long is parked, not " +
                 "incoming, and no longer holds the level open.")]
        [SerializeField] float stuckSeconds = 4f;

        [Tooltip("How far ahead still counts as 'in front of me'. Enemies further away " +
                 "than this cannot be seen or shot, so they do not keep the level alive. " +
                 "Must stay comfortably larger than the biggest gap between waves.")]
        [SerializeField] float reachAhead = 55f;

        // SUPERSEDED by EpicRoadEndWhenClear. Do not revive this.
        //
        // It ended a level when nothing was "incoming", where incoming had grown to
        // mean: ahead of the player, but not within aheadMargin, not previously level
        // with the player, and still closing. Measured in play, those exclusions wrote
        // off 256 living enemies and reported the road clear. Disabling the component
        // was not enough - a running coroutine survives enabled=false and Arm() is
        // invoked through a UnityEvent regardless - so it returns here instead.
        //
        // The replacement asks one question on a timer: is there a living enemy in
        // front of me? Keep it that way.
        public void Arm()
        {
            return;
        }

        void ArmLegacy()
        {
            _fired = false;
            _sawContent = false;
            _passed.Clear();
            _closest.Clear();
            _closestAt.Clear();
            StopAllCoroutines();
            StartCoroutine(Watch());
        }

        // Diagnostic for the "level takes ages to finish after the last enemy dies"
        // report. Four headless runs could not reproduce it - a clean win ended 1.3s
        // after the last kill and a loss raised Game Over in ~2s - so this records
        // what actually happens during a real play session instead of guessing.
        //
        // Writes one line per second plus a line when the run ends, so the gap
        // between "nothing left to fight" and "level actually ended" is measurable
        // rather than estimated. Turn off once the cause is found.
        [Header("Diagnostics")]
        [Tooltip("Append a per-second timeline to the file below. Off for release.")]
        [SerializeField] bool logTimeline = true;
        [SerializeField] string timelinePath = "/tmp/epicroad-timeline.txt";

        float _armedAt;
        float _lastNonZeroAt = -1f;
        int _lastRemaining = -1;

        // What is the level actually waiting for? Reports how far ahead each
        // surviving enemy is, so an empty-looking road that still will not end can
        // be explained: if these distances are large, the player is waiting on
        // enemies far beyond anything the camera draws.
        string DescribeRemaining()
        {
            float playerZ = character ? character.transform.position.z : 0f;
            var dist = new List<float>();
            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;
                var damage = enemy.GetComponent<MOST_Damage>();
                if (damage != null && damage.IsDefeated()) continue;
                if (!enemy.StartMove) continue;
                float d = enemy.transform.position.z - playerZ;
                if (d <= aheadMargin) continue;
                if (_passed.Contains(enemy)) continue;
                dist.Add(d);
            }
            dist.Sort();
            if (dist.Count == 0) return "";
            var s = new System.Text.StringBuilder("aheadDist=[");
            for (int i = 0; i < dist.Count && i < 12; i++) s.Append(dist[i].ToString("F0")).Append(' ');
            s.Append("] nearest=").Append(dist[0].ToString("F0"))
             .Append(" farthest=").Append(dist[dist.Count - 1].ToString("F0"));
            return s.ToString();
        }

        void Log(string line)
        {
            if (!logTimeline || string.IsNullOrEmpty(timelinePath)) return;
            // Wall clock, not Time.timeSinceLevelLoad: that resets on every level
            // load, which makes the load itself - the gap between one level ending
            // and the next arming - completely invisible. With 500-1100 enemies per
            // level that gap is exactly the thing worth measuring.
            try
            {
                System.IO.File.AppendAllText(timelinePath,
                    System.DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + "\n");
            }
            catch { }
        }

        IEnumerator Watch()
        {
            _armedAt = Time.timeSinceLevelLoad;
            _lastNonZeroAt = -1f;
            _lastRemaining = -1;
            int level = 0;
            if (manager != null && manager.DatabaseHolder != null)
            {
                var d = manager.DatabaseHolder.Get<IntData>(manager.LevelDataName);
                if (d != null) level = d.Value;
            }
            Log($"=== ARMED level {level} at t={_armedAt:F1} ===");

            yield return new WaitForSeconds(graceSeconds);

            while (!_fired)
            {
                int remaining = CountRemaining();

                if (remaining > 0) _lastNonZeroAt = Time.timeSinceLevelLoad;
                if (remaining != _lastRemaining)
                {
                    _lastRemaining = remaining;
                    Log($"t={Time.timeSinceLevelLoad:F1} remaining={remaining} squad={(character ? character.ChildHolder.childCount : -1)}"
                        + (remaining > 0 && remaining <= 30 ? "  " + DescribeRemaining() : ""));
                }

                // Only ever allowed to fire after content has actually been
                // seen, so a level that is still spawning cannot instant-win.
                if (remaining > 0) _sawContent = true;
                else if (_sawContent) { Trigger(); yield break; }

                yield return new WaitForSeconds(checkInterval);
            }
        }

        // Enemies only. Barrels and gates deliberately do NOT hold the level open.
        //
        // They used to, and it made the end drag badly: the squad shoots far
        // ahead, so the last enemy often dies while it is still tens of units
        // away. Everything between the player and that kill - every barrel still
        // scrolling in - then had to physically reach the player before the level
        // would end, which on a long level is many seconds of empty road.
        //
        // Leftover barrels are just pickups you chose to skip. Once nothing can
        // hurt you, the level is over.
        // THE RULE: is there a live enemy in front of the player, close enough to
        // matter? If not, the level is over. Nothing else gets a vote.
        //
        // Everything clever that used to live here made the level outlast the
        // gameplay. Barrels and gates never counted; now distance is capped too,
        // because an enemy 120 units away is not something the player can see, shoot,
        // or do anything about - it is just a reason to stand on an empty road
        // waiting. If nothing is inside reachAhead, there is nothing to do, and
        // standing there is not gameplay.
        int CountRemaining()
        {
            float playerZ = character ? character.transform.position.z : 0f;
            int remaining = 0;

            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;

                // Too far away to be the player's problem.
                if (enemy.transform.position.z - playerZ > reachAhead) continue;

                // Already dead, just not cleaned up yet. MOST_Damage waits
                // DelayBeforeApply (1.75s on these prefabs) after death before it
                // deactivates or destroys the object, so a corpse stays
                // active-in-hierarchy that whole time. Counting those added ~1.75s
                // of empty road after the final kill.
                //
                // Two signals, because neither alone is reliable:
                //   Health <= 0    - true for the normal path, where the squad shoots
                //                    an enemy down. Safe to read here (not at spawn):
                //                    Arm() runs on OnStartPlay and the first check is
                //                    graceSeconds later, long after every
                //                    MOST_Damage.Start() has set Health to MaxHealth.
                //   !StartMove     - MOST_Damage.OnDefeat is wired on these prefabs to
                //                    WalkEnemyManager.DestroyChild(), which clears
                //                    StartMove. This catches deaths that never touch
                //                    health, e.g. a direct InstantKill(), which sets an
                //                    internal flag and leaves Health at max.
                var damage = enemy.GetComponent<MOST_Damage>();
                if (damage != null && damage.IsDefeated()) continue;
                if (!enemy.StartMove) continue;

                // Only enemies still IN FRONT of the squad count.
                //
                // Enemies die solely to bullets, which spawn on the squad and travel
                // forward. Anything level with or behind the squad can therefore never
                // be shot - and aggro'd enemies chase the player, so they end up sitting
                // exactly there. Those became permanently-alive blockers that held the
                // level open until the finish line arrived, which is the long wait on
                // the enemy-heavy levels. They are no longer incoming, so they no longer
                // count. If one is still grinding the squad down, that resolves as a
                // loss on its own.
                if (enemy.transform.position.z <= playerZ + aheadMargin)
                {
                    // Remember it, so that if it aggros and chases back in front of
                    // the squad it cannot start counting as incoming all over again.
                    _passed.Add(enemy);
                    continue;
                }
                if (_passed.Contains(enemy)) continue;

                // Still approaching, or parked? Only the former is your problem.
                float d = enemy.transform.position.z - playerZ;
                float now = Time.timeSinceLevelLoad;
                if (!_closest.TryGetValue(enemy, out float best) || d < best - 0.5f)
                {
                    _closest[enemy] = d;
                    _closestAt[enemy] = now;
                }
                else if (now - _closestAt[enemy] > stuckSeconds)
                {
                    // Hovering at a fixed distance, or drifting away. Not incoming.
                    _passed.Add(enemy);
                    continue;
                }

                remaining++;
            }

            return remaining;
        }

        // Level ended some other way (finish line arrived, death, scene reload).
        // The gap printed here is the number in question: how long the run kept
        // going after there was nothing left to fight.
        void OnDisable()
        {
            if (_fired || _lastNonZeroAt < 0f) return;
            float now = Time.timeSinceLevelLoad;
            Log($"t={now:F1} ENDED WITHOUT DETECTOR (finish line / death). " +
                $"last enemy seen t={_lastNonZeroAt:F1} -> DEAD TIME {now - _lastNonZeroAt:F1}s");
        }

        void Trigger()
        {
            if (_fired) return;
            Log($"t={Time.timeSinceLevelLoad:F1} DETECTOR FIRED " +
                $"(last enemy seen t={_lastNonZeroAt:F1}, dead time {Time.timeSinceLevelLoad - _lastNonZeroAt:F1}s)");

            // If the squad is already wiped, the loss has its own flow and this must
            // not hand out a win on top of it - but it must still stop looking, or the
            // run limps on to the finish line. Measured on level 4: the detector
            // decided at t=71.6 and the level kept going to t=80.4 because this
            // returned without setting _fired.
            if (character && !character.IsActive) { _fired = true; return; }

            _fired = true;
            if (character) character.Deactivate();
            if (manager) manager.OnWin();
        }
    }
}
