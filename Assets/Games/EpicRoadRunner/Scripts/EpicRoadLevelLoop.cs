using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Keeps the game going past the last authored level by cycling the final few.
    //
    // UniversalGameManager.Loading() picks its level with
    //     LevelsPrefs[Mathf.Min(level, LevelsPrefs.Length) - 1]
    // so once you pass the last entry it clamps and replays that one prefab
    // forever - byte-identical, every time.
    //
    // This runs before that (execution order below UniversalGameManager's default
    // 0) and swaps LevelsPrefs for a single-entry array holding the level that
    // SHOULD play. Because Mathf.Min(level, 1) - 1 is always 0, the manager then
    // loads exactly that one, whatever the level number is.
    //
    // The saved counter is never touched, so the HUD keeps counting up - level 14
    // still reads "14" while playing the level-11 layout.
    //
    // Runs after EpicRoadTestStartLevel (-1000) so a forced test level is already
    // applied, and before UniversalGameManager (0).
    [DefaultExecutionOrder(-500)]
    public class EpicRoadLevelLoop : MonoBehaviour
    {
        [SerializeField] UniversalGameManager manager;

        [Tooltip("First level of the repeating pool, 1-based. 11 means levels 1-10 play " +
                 "once each, then 11, 12, 13 cycle forever.")]
        [Min(1)] public int LoopStart = 11;

        [Tooltip("Log which level prefab was chosen.")]
        public bool LogChoice = true;

        void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<UniversalGameManager>();
            if (manager == null || manager.DatabaseHolder == null) return;

            var all = manager.LevelsPrefs;
            if (all == null || all.Length == 0) return;

            var data = manager.DatabaseHolder.Get<IntData>(manager.LevelDataName);
            if (data == null) return;

            int level = Mathf.Max(1, data.Value);
            GameObject chosen;

            if (level < LoopStart || all.Length < LoopStart)
            {
                // Still inside the authored run - behave exactly as before.
                chosen = all[Mathf.Min(level, all.Length) - 1];
            }
            else
            {
                int poolStart = LoopStart - 1;                 // 0-based
                int poolSize = all.Length - poolStart;         // how many levels cycle
                chosen = all[poolStart + ((level - LoopStart) % poolSize)];
            }

            // Runtime-only assignment; the scene asset keeps its full list.
            manager.LevelsPrefs = new[] { chosen };

            if (LogChoice) Debug.Log($"[EpicRoadLevelLoop] level {level} -> {chosen.name}");
        }
    }
}
