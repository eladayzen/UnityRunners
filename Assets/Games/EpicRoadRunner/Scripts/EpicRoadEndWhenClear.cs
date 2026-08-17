using System.Collections;
using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Look ahead. See no enemy. End the level. That is the whole script.
    //
    // This replaces EpicRoadClearDetector, which grew a position test, a passed-set,
    // a parked-enemy timer, a margin and a reach window - and with every addition the
    // level kept outliving the gameplay. None of that is here and none of it is
    // coming back. There is one question, asked on a timer:
    //
    //     is there a living enemy ahead of the player, within LookAhead units?
    //
    // No  -> the player has nothing left to do, so the level is over. Win it.
    // Yes -> keep playing.
    //
    // Barrels, gates and pickups deliberately do not count. They are optional; you
    // can drive past every one of them. Only enemies can stop you, so only enemies
    // can keep the level alive.
    public class EpicRoadEndWhenClear : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] UniversalGameManager manager;
        [SerializeField] CharacterControl_ShootRunner character;

        [Header("The rule")]
        [Tooltip("How often to look, in seconds.")]
        [Min(0.1f)] public float CheckEvery = 1.5f;

        [Tooltip("How far ahead to look, in world units. A level is about 200 units " +
                 "long, so 500 means 'anywhere ahead of me'. Lower it if you want the " +
                 "level to end while far-off enemies are still technically coming.")]
        [Min(1f)] public float LookAhead = 500f;

        [Tooltip("Grace period after the run starts, so the check cannot fire before " +
                 "the level has finished spawning.")]
        [Min(0f)] public float StartDelay = 2f;

        [Tooltip("Pause between the road going clear and the level actually ending, so " +
                 "the last kill reads before the screen changes. Re-checked afterwards, " +
                 "so a late arrival keeps the run going.")]
        [Min(0f)] public float GraceBeforeEnd = 1.25f;

        [Tooltip("How far below the player an enemy may be before it is treated as " +
                 "fallen out of the world and ignored. Enemies are script-moved, so a " +
                 "large drop always means physics has taken them, never gameplay.")]
        [Min(1f)] public float MaxDropBelowRoad = 5f;

        [Tooltip("Name of the IntData holding collected gems, shown on the instant win " +
                 "screen. Leave empty to omit the line.")]
        public string GemsDataName = "";

        [Header("Diagnostics")]
        public bool LogChecks = true;
        public string LogPath = "/tmp/epicroad-timeline.txt";

        bool _done;
        bool _seenAnyEnemy;   // never win a level that has not started yet
        float _lastSeenAt = -1f;

        // Why does the count go down - because the player killed something, or
        // because it wandered past them untouched?
        //
        // The distinction decides everything. If enemies are dying, the last stretch
        // of a level is a fight and the length is earned. If they are drifting by
        // unkilled, the player is a spectator waiting for traffic to clear, and the
        // level is holding them for something they were never able to act on.
        readonly System.Collections.Generic.HashSet<WalkEnemyManager> _wasAhead
            = new System.Collections.Generic.HashSet<WalkEnemyManager>();
        int _killed, _driftedPast;
        float _noKillSince;
        bool _identified;

        void OnEnable()
        {
            _done = false;
            _seenAnyEnemy = false;
            _lastSeenAt = -1f;
            _noKillSince = 0f;
            _identified = false;
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            if (manager == null) manager = FindFirstObjectByType<UniversalGameManager>();
            if (character == null) character = FindFirstObjectByType<CharacterControl_ShootRunner>();

            Log("=== EndWhenClear armed (every " + CheckEvery + "s, looking " + LookAhead + " units ahead) ===");

            // Stop the round-mode wave hordes falling out of the world.
            //
            // EnemyNoGunNoHealthBar has a non-kinematic Rigidbody with gravity on, but
            // WalkEnemyManager moves it by writing transform.position directly - physics
            // is never asked to move these, it only drops them. The generator's own rows
            // land on the road; the wave hordes the builder adds have nothing under them
            // and free-fall from t=0, reaching y = -20197 by t=64 on level 4. They cannot
            // be shot, cannot reach the player, and cannot be damaged, so every wave the
            // round system adds has been quietly absent from the fight.
            //
            // Kinematic is what these should always have been: script-driven, trigger
            // colliders still fire for bullets, no gravity to fall to. Done here, at
            // runtime, so it fixes every already-built level without a rebuild.
            for (float t = 0f; t < 3f; t += 0.25f)
            {
                if (FixFallers() > 0 && t > 1f) break;
                yield return new WaitForSeconds(0.25f);
            }

            yield return new WaitForSeconds(StartDelay);

            while (!_done)
            {
                int ahead = CountAhead();

                if (ahead > 0)
                {
                    _seenAnyEnemy = true;
                    _lastSeenAt = Time.timeSinceLevelLoad;
                }
                else if (_seenAnyEnemy)
                {
                    // A beat before declaring it over, so the last kill has time to
                    // land visually instead of the screen cutting away mid-explosion.
                    // Re-checked afterwards: if something has arrived in the meantime
                    // the run simply carries on.
                    Log($"t={Time.timeSinceLevelLoad:F1} road clear - holding {GraceBeforeEnd:F1}s before ending");
                    yield return new WaitForSeconds(GraceBeforeEnd);

                    if (CountAhead() > 0)
                    {
                        Log($"t={Time.timeSinceLevelLoad:F1} enemies arrived during the grace - carrying on");
                        continue;
                    }

                    Log($"t={Time.timeSinceLevelLoad:F1} NOTHING AHEAD -> ending level " +
                        $"(last enemy seen t={_lastSeenAt:F1}, waited {Time.timeSinceLevelLoad - _lastSeenAt:F1}s)");
                    End();
                    yield break;
                }

                // Track how long nothing has been dying, so the survivors can be
                // identified exactly once rather than every second.
                if (_killed > 0) { _noKillSince = Time.timeSinceLevelLoad; _identified = false; }

                if (LogChecks)
                    Log($"t={Time.timeSinceLevelLoad:F1} enemiesAhead={ahead}"
                        + $" killed={_killed} driftedPast={_driftedPast}"
                        + $" squad={(character ? character.ChildHolder.childCount : -1)}"
                        + (ahead > 0 && ahead <= 25 ? "  dist=" + Distances() : ""));

                if (!_identified && ahead > 0 && Time.timeSinceLevelLoad - _noKillSince > 5f
                    && character != null && character.IsActive)
                {
                    _identified = true;
                    Log($"t={Time.timeSinceLevelLoad:F1} NOTHING HAS DIED FOR 5s - who are these?"
                        + IdentifySurvivors());
                }
                yield return new WaitForSeconds(CheckEvery);
            }
        }

        // Makes every enemy kinematic, and lifts back any that already started to sink.
        // Returns how many were fixed.
        int FixFallers()
        {
            int fixedCount = 0;
            float roadY = character != null ? character.transform.position.y : 0f;

            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rb = enemy.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic) continue;

                rb.isKinematic = true;
                rb.useGravity = false;

                // Put it on the player's plane, whichever way it had drifted.
                // The wave hordes spawn ABOVE the road and walk over the player's head
                // - visible to nothing, hit by nothing - and gravity then drags them
                // down through the world. Either way they are off the one plane where
                // the game happens, so snap them to it.
                var p = enemy.transform.position;
                if (Mathf.Abs(p.y - roadY) > 0.5f)
                {
                    enemy.transform.position = new Vector3(p.x, roadY, p.z);
                }
                fixedCount++;
            }

            if (fixedCount > 0) Log($"FIXED {fixedCount} falling enemies (kinematic, gravity off)");
            return fixedCount;
        }

        int CountAhead()
        {
            if (character == null) return 0;
            float playerZ = character.transform.position.z;
            int count = 0;

            var nowAhead = new System.Collections.Generic.HashSet<WalkEnemyManager>();
            _killed = 0;
            _driftedPast = 0;

            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;

                // Dead but not yet cleaned up - MOST_Damage keeps the corpse around
                // for DelayBeforeApply seconds after the kill.
                var damage = enemy.GetComponent<MOST_Damage>();
                bool dead = damage != null && damage.IsDefeated();

                float ahead = enemy.transform.position.z - playerZ;

                if (_wasAhead.Contains(enemy))
                {
                    if (dead) _killed++;
                    else if (ahead <= 0f) _driftedPast++;
                }

                if (dead) continue;
                if (ahead <= 0f) continue;          // behind the player
                if (ahead > LookAhead) continue;    // outside the window

                // Fallen out of the world, so not the player's problem.
                //
                // EnemyNoGunNoHealthBar carries a non-kinematic Rigidbody with gravity
                // on, and the round-mode wave hordes spawn with nothing under them, so
                // they free-fall from t=0. Measured on level 4 at t=64: y = -20197,
                // which is exactly 0.5*9.81*64.2^2. They can never be shot, never reach
                // the player, and never damage the squad - but their z still scrolls,
                // so a purely horizontal test counted 23 of them as live content and
                // held the level open ~13 seconds while they sank past.
                if (enemy.transform.position.y < character.transform.position.y - MaxDropBelowRoad) continue;

                nowAhead.Add(enemy);
                count++;
            }

            _wasAhead.Clear();
            foreach (var e in nowAhead) _wasAhead.Add(e);
            return count;
        }

        // Who are the enemies that never die?
        //
        // Measured on level 4: for the last 19 seconds of a run, 23 enemies walked
        // from 45 units away straight through a 56-strong squad and out the back.
        // killed=0 the whole time, and the squad took no damage either - they do not
        // interact with the player at all. Earlier in the same run 17 died in one
        // second, so kills register fine; these particular ones are untouchable.
        //
        // This dumps what they actually are - hierarchy, layer, tag, collider state,
        // health - because nothing about position or distance explains it.
        string IdentifySurvivors()
        {
            float playerZ = character.transform.position.z;
            var sb = new System.Text.StringBuilder();
            int shown = 0;

            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;
                var dmg = enemy.GetComponent<MOST_Damage>();
                if (dmg != null && dmg.IsDefeated()) continue;
                float a = enemy.transform.position.z - playerZ;
                if (a <= 0f || a > LookAhead) continue;
                if (shown++ >= 3) break;

                // Full path, so it is obvious whether this is a generator-rolled
                // enemy or one of the Obj_Enemy Wave hordes the builder adds.
                string path = enemy.name;
                for (var p = enemy.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;

                var col = enemy.GetComponentInChildren<Collider>(true);
                var pos = enemy.transform.position;

                sb.Append($"\n    [{shown}] {path}"
                    + $"\n        pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) ahead={a:F1} layer={enemy.gameObject.layer}"
                    + $" tag={enemy.gameObject.tag} startMove={enemy.StartMove}"
                    + $"\n        health={(dmg != null ? dmg.Health + "/" + dmg.MaxHealth : "no MOST_Damage")}"
                    + $" collider={(col == null ? "NONE" : col.GetType().Name + " enabled=" + col.enabled + " trigger=" + col.isTrigger)}");
            }
            return sb.ToString();
        }

        // How far ahead the survivors are. If these numbers are small and unchanging,
        // they are parked on top of the squad where forward-only bullets cannot reach
        // them; if they are large, they are off-screen and equally irrelevant. Either
        // way they are not something the player can act on.
        string Distances()
        {
            float playerZ = character.transform.position.z;
            var d = new System.Collections.Generic.List<float>();
            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;
                var dmg = enemy.GetComponent<MOST_Damage>();
                if (dmg != null && dmg.IsDefeated()) continue;
                float a = enemy.transform.position.z - playerZ;
                if (a <= 0f || a > LookAhead) continue;
                d.Add(a);
            }
            d.Sort();
            var s = new System.Text.StringBuilder("[");
            for (int i = 0; i < d.Count && i < 20; i++) s.Append(d[i].ToString("F0")).Append(' ');
            return s.Append(']').ToString();
        }

        void End()
        {
            if (_done) return;
            _done = true;

            // A dead squad has its own lose flow; do not hand out a win on top of it.
            if (character != null && !character.IsActive) return;

            if (character != null) character.Deactivate();
            if (manager != null) manager.OnWin();

            StartCoroutine(WatchForWinScreen());
        }

        // Measures the two halves of the wait the player actually feels:
        //   win decided -> win screen visible   (should be immediate)
        //   win screen  -> next level playable  (the level load)
        // Measured on a real session: detection fired 1.0s after the last enemy, then
        // 12.4s passed before the next level armed. If the win screen appears at the
        // start of that 12.4s the player at least knows they won; if it appears at the
        // end, they spend the whole time staring at an empty road.
        IEnumerator WatchForWinScreen()
        {
            float t0 = Time.realtimeSinceStartup;
            bool seen = false;

            while (Time.realtimeSinceStartup - t0 < 30f)
            {
                foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string n = rt.name.ToLower();
                    if ((n.Contains("win") || n.Contains("complete")) && rt.gameObject.activeInHierarchy)
                    {
                        Log($"WIN SCREEN '{rt.name}' visible {Time.realtimeSinceStartup - t0:F2}s after the win was decided");
                        seen = true;
                        break;
                    }
                }
                if (seen) yield break;
                yield return null;
            }

            Log("WIN SCREEN never appeared within 30s of the win being decided");
        }

        void Log(string line)
        {
            if (!LogChecks || string.IsNullOrEmpty(LogPath)) return;
            try
            {
                System.IO.File.AppendAllText(LogPath,
                    System.DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + "\n");
            }
            catch { }
        }
    }
}
