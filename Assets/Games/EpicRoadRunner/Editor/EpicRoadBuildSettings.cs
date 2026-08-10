using UnityEngine;

namespace RunnerPac.EpicRoadRunner.EditorTools
{
    // Every knob that shapes a level, in one asset you can see and edit.
    //
    // The MOST generator only knows "pick a random part per row". Everything that
    // makes a level feel authored - enemies vs gifts mix, pacing, barrel cost
    // ramp, gate shielding - is applied afterwards by EpicRoadLevelBuilder using
    // the values below.
    //
    // Select the asset, edit, press Build All Levels.
    [CreateAssetMenu(menuName = "EpicRoad/Build Settings", fileName = "EpicRoad Build Settings")]
    public class EpicRoadBuildSettings : ScriptableObject
    {
        [System.Serializable]
        public class LevelSpec
        {
            public string Label = "L1";

            [Tooltip("The MOST RunnerLevelProfile asset used to roll the raw layout.")]
            public ScriptableObject Profile;

            [Header("Layout")]
            [Tooltip("Distance between rows. Smaller = denser level.")]
            [Min(3f)] public float RowSpacing = 8f;

            [Tooltip("Total run length in world units. Seconds = this / 2.5.")]
            [Min(40f)] public float TrackLength = 130f;

            [Tooltip("Clear run-up before the first obstacle, in world units.")]
            [Min(0f)] public float StartClearance = 24f;

            [Header("Mix")]
            [Tooltip("Share of encounters that should be enemies rather than barrels/gates. " +
                     "0.25 = a quarter of everything you meet is a fight.")]
            [Range(0f, 0.6f)] public float EnemyShare = 0.25f;

            [Tooltip("Minimum number of reward pickups (barrels + charge gates).")]
            [Min(0)] public int MinGifts = 10;

            [Tooltip("How many members an enemy crowd contains. 6 / 12 / 20 prefabs exist.")]
            [Min(1)] public int CrowdSize = 12;

            [Header("Barrel cost ramp")]
            [Tooltip("Bullets needed for the FIRST barrel of the level.")]
            [Min(1f)] public float BarrelHpStart = 3f;

            [Tooltip("Bullets needed for the LAST barrel. Higher than start, because " +
                     "your rate of fire grows during a run.")]
            [Min(1f)] public float BarrelHpEnd = 14f;

            [Header("Charge gates (red/blue)")]
            [Tooltip("Most negative starting value a gate can roll.")]
            public float GateMin = -4f;

            [Tooltip("Most positive starting value. Keep low - a high start is free troops.")]
            public float GateMax = 3f;

            [Header("Turn pickups into fights")]
            [Tooltip("Share of charge gates swapped for enemies. Applied after the safe " +
                     "opening, so the start is untouched.")]
            [Range(0f, 1f)] public float GatesToEnemies = 0f;

            [Tooltip("Share of barrels swapped for enemies. Applied after the safe opening.")]
            [Range(0f, 1f)] public float BarrelsToEnemies = 0f;

            [Header("Back-half surge")]
            [Tooltip("Where a solid wall of enemies begins, as a share of the level. " +
                     "0 = off. 0.5 = the whole second half is packed with hordes. " +
                     "Adds no pickups, only enemies.")]
            [Range(0f, 1f)] public float SurgeStart = 0f;

            [Tooltip("Rows between surge enemies. 1 = an enemy every row.")]
            [Min(0.5f)] public float SurgeSpacingRows = 1f;

            [Header("Pacing")]
            [Tooltip("Opening stretch with no enemies at all, as a share of the level. " +
                     "0.25 = the first quarter is pickups only, so you can gear up " +
                     "before anything attacks you.")]
            [Range(0f, 0.5f)] public float GearUpShare = 0.25f;

            [Tooltip("Longest allowed empty stretch, in rows. Anything bigger gets an " +
                     "enemy dropped into it so the level never goes quiet.")]
            [Min(1f)] public float MaxGapRows = 1.6f;

            [Tooltip("Clear air an enemy needs from any barrel/gate, in rows. Too small " +
                     "and there is no time to shoot it after committing fire to a barrel.")]
            [Min(0f)] public float EnemyClearRows = 1.15f;
        }

        [Tooltip("One entry per level, in play order.")]
        public LevelSpec[] Levels = new LevelSpec[0];

        [Header("Output")]
        [Tooltip("Folder the generated level prefabs are written to.")]
        public string OutputFolder = "Assets/Games/EpicRoadRunner/Levels";

        [Tooltip("Rolls to try per level before keeping the best one.")]
        [Min(1)] public int RollsPerLevel = 20;
    }
}
