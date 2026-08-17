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

        [Header("Diagnostics")]
        public bool LogChecks = true;
        public string LogPath = "/tmp/epicroad-timeline.txt";

        bool _done;
        bool _seenAnyEnemy;   // never win a level that has not started yet
        float _lastSeenAt = -1f;

        void OnEnable()
        {
            _done = false;
            _seenAnyEnemy = false;
            _lastSeenAt = -1f;
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            if (manager == null) manager = FindFirstObjectByType<UniversalGameManager>();
            if (character == null) character = FindFirstObjectByType<CharacterControl_ShootRunner>();

            Log("=== EndWhenClear armed (every " + CheckEvery + "s, looking " + LookAhead + " units ahead) ===");
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
                    Log($"t={Time.timeSinceLevelLoad:F1} NOTHING AHEAD -> ending level " +
                        $"(last enemy seen t={_lastSeenAt:F1}, waited {Time.timeSinceLevelLoad - _lastSeenAt:F1}s)");
                    End();
                    yield break;
                }

                if (LogChecks)
                    Log($"t={Time.timeSinceLevelLoad:F1} enemiesAhead={ahead}"
                        + (ahead > 0 && ahead <= 20 ? "  dist=" + Distances() : ""));
                yield return new WaitForSeconds(CheckEvery);
            }
        }

        int CountAhead()
        {
            if (character == null) return 0;
            float playerZ = character.transform.position.z;
            int count = 0;

            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;

                // Dead but not yet cleaned up - MOST_Damage keeps the corpse around
                // for DelayBeforeApply seconds after the kill.
                var damage = enemy.GetComponent<MOST_Damage>();
                if (damage != null && damage.IsDefeated()) continue;

                float ahead = enemy.transform.position.z - playerZ;
                if (ahead <= 0f) continue;          // behind the player
                if (ahead > LookAhead) continue;    // outside the window

                count++;
            }

            return count;
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
