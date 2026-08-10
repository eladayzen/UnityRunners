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
        [Tooltip("0 = off (use the saved progress). 1+ = jump to that level when you press Play.")]
        [Min(0)] public int StartAtLevel = 0;

        [Tooltip("Also write the value to disk. Leave off so testing does not " +
                 "overwrite real saved progress.")]
        public bool PersistToSave = false;

        [SerializeField] UniversalGameManager manager;

        // Applied ONCE per play session, not per scene load. Winning reloads the
        // scene, so re-applying every Awake would force the same level forever
        // and progression could never advance past it.
        static bool _applied;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        static void ResetOnEnterPlayMode() => _applied = false;
#endif

        void Awake()
        {
            if (StartAtLevel <= 0 || _applied) return;
            if (manager == null) manager = FindFirstObjectByType<UniversalGameManager>();
            if (manager == null || manager.DatabaseHolder == null) return;

            var data = manager.DatabaseHolder.Get<IntData>(manager.LevelDataName);
            if (data == null) return;

            _applied = true;
            data.Value = StartAtLevel;
            if (PersistToSave) manager.DatabaseHolder.SaveToJson();

            Debug.Log($"[EpicRoadTestStartLevel] Starting at level {StartAtLevel} " +
                      "(applies once; progression continues normally from here).");
        }
    }
}
