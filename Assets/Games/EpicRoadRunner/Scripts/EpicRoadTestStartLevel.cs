using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Testing helper: forces the run to begin at a specific level instead of
    // wherever the saved counter happens to be.
    //
    // Set Start At Level in the Inspector, press Play, and the run starts
    // there. Set it back to 0 to disable and resume normal progression.
    //
    // Execution order is forced early because UniversalGameManager reads the
    // saved level inside its own Awake() (InitLevels -> Loading). If this ran
    // at the default order it would set the value AFTER the level had already
    // been instantiated, and do nothing.
    [DefaultExecutionOrder(-1000)]
    public class EpicRoadTestStartLevel : MonoBehaviour
    {
        [Tooltip("0 = off (use the saved progress). 1+ = always start on that level.")]
        [Min(0)] public int StartAtLevel = 0;

        [Tooltip("Also write the value to disk. Leave off so testing does not " +
                 "overwrite real saved progress.")]
        public bool PersistToSave = false;

        [SerializeField] UniversalGameManager manager;

        void Awake()
        {
            if (StartAtLevel <= 0) return;
            if (manager == null) manager = FindFirstObjectByType<UniversalGameManager>();
            if (manager == null || manager.DatabaseHolder == null) return;

            var data = manager.DatabaseHolder.Get<IntData>(manager.LevelDataName);
            if (data == null) return;

            data.Value = StartAtLevel;
            if (PersistToSave) manager.DatabaseHolder.SaveToJson();

            Debug.Log($"[EpicRoadTestStartLevel] Forcing start at level {StartAtLevel}.");
        }
    }
}
