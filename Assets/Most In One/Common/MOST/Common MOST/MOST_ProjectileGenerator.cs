using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_ProjectileGenerator : MonoBehaviour
    {
        public enum ProjectileSelect { OneGameObject, ListOfGameObjects, RandomList }
        public enum DataToSend { Throw, Directed }
        public enum Sequence { One, Multiple }
        public enum OffsetSelect { None, Fixed, RandomBetweenTwo, ListOfOffsets, RandomListOfOffsets }
        public enum OffestTarget { OnDirection, OnPosition }

        // NOTE: This system is designed to work with your own projectile scripts.
        // Inside the Instantiate calls you have a reference to the spawned object
        // so you can fetch and configure your custom components there.
        [HelpBox("Important: This projectile system is friendly to custom scripts your projectile may have. If you want to control a custom script on the spawned projectile," +
            " check the Instantiate call—there’s a reference to the spawned object.", HelpBoxKind.Info)]
        [Tooltip("How to select the projectile prefab: one prefab, iterate through a list, or pick randomly from a list.")]
        public ProjectileSelect ObjectSelect;

        [Tooltip("Projectile prefab (must include a Projectile_Movement behavior or one will be added at runtime).")]
        [HideIfAny(nameof(ObjectSelect), ProjectileSelect.OneGameObject, false)]
        [Required] public GameObject Projectile;

        [Tooltip("Projectile prefabs list (each must include a Projectile_Movement behavior or one will be added at runtime).")]
        [HideIfAny(nameof(ObjectSelect), ProjectileSelect.OneGameObject, true)]
        public GameObject[] Projectiles;

        [Tooltip("If enabled, all spawned projectile colliders will ignore collisions with the assigned target collider (below).")]
        public bool DisableCollisionBetweenSpawndObjectAndThis;

        [Tooltip("Target collider used to ignore collisions with spawned projectiles when the above option is enabled.")]
        [ReadOnlyIf(nameof(DisableCollisionBetweenSpawndObjectAndThis), false)]
        [InnerHint("(Required If)")] public Collider Collider;

        // Spawn Control
        [BigHeader("Spawn Control")]
        [GUIColor("green"), Tooltip("Read-only status indicating whether this behavior is currently enabled.\nUse EnableState(bool) to toggle.")]
        public bool Enable = true;

        [Tooltip("World-space start point of the projectile.")]
        [Required] public Transform SpawnPoint;

        [Tooltip("Spawn one projectile or multiple in sequence.")]
        public Sequence SpawnSequence;

        [Tooltip("Number of projectiles to spawn when Sequence = Multiple.")]
        [HideIfAny(nameof(SpawnSequence), Sequence.Multiple, false)]
        [Min(1)] public int Amount;

        [Tooltip("What data the generator sends to the projectile: Throw (arc parameters) or Directed (direction vector).")]
        public DataToSend Type;

        [Tooltip("Optional VFX/GameObject instantiated at the spawn point when a projectile is spawned.")]
        [InnerHint("(Optional)")] public GameObject OnShootEffect;

        // Offset Control
        [BigHeader("Offset Control")]
        [Tooltip("Absolute world-space offset added to the SpawnPoint position.")]
        public Vector3 BaseOffset;

        [HelpBox("Base Offset is an absolute shift from the spawn point. Other offsets below are applied relative to the " +
            "projectile direction (its rotation).", HelpBoxKind.Info)]
        [Tooltip("How to compute the additional offset/rotation applied to each spawn (after BaseOffset).")]
        public OffsetSelect OffsetType;

        [Tooltip("Where to apply the offset: modify the aim direction (OnDirection) or offset the spawn position (OnPosition).")]
        [HideIfAny(nameof(OffsetType), OffsetSelect.None, true)]
        public OffestTarget OffsetApply;

        [Tooltip("If true, interpret offsets as Euler rotation (degrees). If false, offsets are treated as directional deltas (see code for mapping).")]
        [HideIfAny(nameof(OffsetType), OffsetSelect.None, true)]
        public bool AsRotation;

        [Tooltip("List of offsets used when OffsetType is ListOfOffsets or RandomListOfOffsets. (Name kept as-is for serialization.)")]
        [HideIfAll(nameof(OffsetType), OffsetSelect.ListOfOffsets, false, nameof(OffsetType), OffsetSelect.RandomListOfOffsets, false)]
        public Vector3[] OffestList;

        [Tooltip("Fixed offset used when OffsetType = Fixed.")]
        [HideIfAny(nameof(OffsetType), OffsetSelect.Fixed, false)]
        public Vector3 Offset;

        [Tooltip("Lower bound offset for OffsetType = RandomBetweenTwo.")]
        [HideIfAny(nameof(OffsetType), OffsetSelect.RandomBetweenTwo, false)]
        public Vector3 OffsetA;

        [Tooltip("Upper bound offset for OffsetType = RandomBetweenTwo.")]
        [HideIfAny(nameof(OffsetType), OffsetSelect.RandomBetweenTwo, false)]
        public Vector3 OffsetB;

        // Projectile Data
        [BigHeader("Projectile Data")]
        [Tooltip("Delay before any projectile is spawned (seconds).")]
        [Min(0)] public float DelayBeforeSpawn;

        [Tooltip("Delay between spawned projectiles when Sequence = Multiple (seconds).")]
        [HideIfAny(nameof(SpawnSequence), Sequence.Multiple, false)]
        [Min(0)] public float DelayBetweenEachSpawn = .18f;

        [Tooltip("Directed mode only: auto-destroy spawned projectile after this time (seconds). 0 = never.")]
        [HideIfAny(nameof(Type), DataToSend.Throw, true)]
        [Min(0)] public float LifeTime = 5;

        [Tooltip("Throw mode: time to reach the target (seconds).")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        [Min(0)] public float DefaultHitTime = 1;

        [Tooltip("Throw mode: maximum arc height (world units).")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        [Min(0)] public float DefaultMaxHight = 8;

        [Tooltip("Throw mode: random rotation speed applied to the projectile while flying.")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        [Min(0)] public float DefaultRandomRotationSpeed = 20;

        [Tooltip("Throw mode: if enabled, scales HitTime/MaxHight by distance to target versus DefaultMaxRange.")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        public bool DynamicOutput;

        [Tooltip("Throw mode: reference distance used for DynamicOutput scaling (world units).")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        [Min(0)] public float DefaultMaxRange;

        [Tooltip("Throw mode (optional): prefab placed at the impact point until the projectile lands.")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        [InnerHint("(Optional)")] public GameObject Marker;

        [Tooltip("Throw mode: scale multiplier applied to the Marker instance.")]
        [HideIfAny(nameof(Type), DataToSend.Throw, false)]
        public float MarkerScale = 1;

        [BigHeader("Damage/Health Setter")]
        public bool AddDamageSetter;

        [HideIfAny(nameof(AddDamageSetter), true, false)]
        [Min(0)] public float DamageAmount;

        [HideIfAny(nameof(AddDamageSetter), true, false)]
        [Min(0)] public int DestroyAfterGetDamage;

        [HideIfAny(nameof(AddDamageSetter), true, false)]
        public UnityEvent OnGetDamage;

        [HideIfAny(nameof(AddDamageSetter), true, false)]
        [Min(0)] public float HealthAmount;

        [HideIfAny(nameof(AddDamageSetter), true, false)]
        [Min(0)] public int DestroyAfterGetHealth;

        [HideIfAny(nameof(AddDamageSetter), true, false)]
        public UnityEvent OnGetHealth;

        // Internal indices for cycling through lists
        int _offsetPointer = -1, objPointer = -1;

        public void EnableState(bool enable) // main behavior enable and disable controller
        {
            Enable = enable; // adjust this method as it fit to your behavior mechanism
            if (enable) // On Enable... do
            {

            }
            else // On Disable... do
            {
                
            }
        }

        /// <summary>
        /// Spawn a directed projectile using a Vector2 (XZ plane) target.
        /// Use the Vector3 overload for Throw mode (requires Y).
        /// </summary>
        public void SpawnProjectile(Vector2 targetPoint)
        {
            if (!Enable) return;
            Vector3 target = new(targetPoint.x, 0, targetPoint.y);
            if (Type == DataToSend.Directed)
                StartCoroutine(SpawnProjectileDirectDelay(target));
            else
                Debug.LogWarning("Throw projectiles don't support Vector2 inputs. Use the Vector3 overload.");
        }

        /// <summary>
        /// Spawn a projectile towards a Vector3 target.
        /// - Throw: computes arc parameters (HitTime/MaxHight) and spawns immediately.
        /// - Directed: spawns after DelayBeforeSpawn and applies direction in coroutine.
        /// </summary>
        public void SpawnProjectile(Vector3 targetPoint)
        {
            if (!Enable) return;
            float delta = 1;
            if (DynamicOutput) delta = Vector3.Distance(SpawnPoint.position, targetPoint) / DefaultMaxRange;

            if (Type == DataToSend.Throw)
                SpawnProjectile(targetPoint, DefaultHitTime * delta, DefaultMaxHight * delta, DefaultRandomRotationSpeed);
            else
                StartCoroutine(SpawnProjectileDirectDelay(targetPoint));
        }

        /// <summary>
        /// Spawn multiple projectiles toward a sequence of targets, with per-item delay spacing.
        /// </summary>
        public void SpawnProjectile(Vector3[] targetPoints)
        {
            if (!Enable) return;
            float delay = DelayBeforeSpawn;
            foreach (Vector3 pos in targetPoints)
            {
                StartCoroutine(ListManager(delay, pos));
                delay += DelayBetweenEachSpawn;
            }
        }

        /// <summary>
        /// Throw mode: spawn toward targetPoint with explicit HitTime and MaxHeight.
        /// </summary>
        public void SpawnProjectile(Vector3 targetPoint, float hitTime, float hight)
        {
            if (!Enable) return;
            if (Type == DataToSend.Throw)
                SpawnProjectile(targetPoint, hitTime, hight, DefaultRandomRotationSpeed);
            else
                Debug.LogWarning("HitTime/Height parameters are ignored in Directed mode. Pass only a Vector3 target.");
        }

        /// <summary>
        /// Throw mode: spawn using a distance multiplier (delta) and explicit height.
        /// </summary>
        public void SpawnProjectile(float delta, float hight, Vector3 targetPoint)
        {
            if (!Enable) return;
            if (Type == DataToSend.Throw)
                SpawnProjectile(targetPoint, DefaultHitTime * delta, hight, DefaultRandomRotationSpeed);
            else
                Debug.LogWarning("HitTime/Height parameters are ignored in Directed mode. Pass only a Vector3 target.");
        }

        /// <summary>
        /// Throw mode: spawn with optional overrides for HitTime/Height/RotationSpeed.
        /// </summary>
        public void SpawnProjectile(Vector3 targetPoint, float? hitTime = -1, float? hight = -1, float? rotSpeed = -1) // attached to spawn system cycle events
        {
            if (!Enable) return;
            if (Type == DataToSend.Throw)
                StartCoroutine(SpawnProjectileThrowDelay(targetPoint, hitTime, hight, rotSpeed));
            else
                Debug.LogWarning("This function works only with Throw projectiles.");
        }

        /// <summary>
        /// Helper that schedules one spawn after a specified delay (used by the list overload).
        /// </summary>
        IEnumerator ListManager(float delay, Vector3 targetPoint)
        {
            yield return new WaitForSeconds(delay);

            float delta = 1;
            if (DynamicOutput) delta = Vector3.Distance(SpawnPoint.position, targetPoint) / DefaultMaxRange;

            if (Type == DataToSend.Throw)
                SpawnProjectile(targetPoint, DefaultHitTime * delta, DefaultMaxHight * delta, DefaultRandomRotationSpeed);
            else
                StartCoroutine(SpawnProjectileDirectDelay(targetPoint));
        }

        /// <summary>
        /// Coroutine that waits DelayBeforeSpawn, spawns the projectile (Throw mode),
        /// ensures Projectile_Movement exists, configures it, and handles marker/collision-ignores.
        /// </summary>
        IEnumerator SpawnProjectileThrowDelay(Vector3 targetPoint, float? hitTime, float? hight, float? rotSpeed)
        {
            yield return new WaitForSeconds(Mathf.Max(0, DelayBeforeSpawn));

            // Optional muzzle/spawn VFX
            if (OnShootEffect)
                Destroy(Instantiate(OnShootEffect, SpawnPoint.position, Quaternion.LookRotation(targetPoint)), 5);

            // Spawn projectile (always uses the single 'Projectile' prefab for Throw mode)
            GameObject spawndObj = Instantiate(Projectile, SpawnPoint.position, Quaternion.identity);

            if (AddDamageSetter)
            {
                DamageAmountSetter data = spawndObj.GetComponent<DamageAmountSetter>() ?
                    spawndObj.GetComponent<DamageAmountSetter>() : spawndObj.AddComponent<DamageAmountSetter>();
                data.DamageAmount = DamageAmount;
                data.DestroyAfterGetDamage = DestroyAfterGetDamage;
                data.OnGetDamage = OnGetDamage;
                data.HealthAmount = HealthAmount;
                data.DestroyAfterGetHealth = DestroyAfterGetHealth;
                data.OnGetHealth = OnGetHealth;
            }

            // Ensure a movement component exists
            if (!spawndObj.GetComponent<Projectile_Movement>())
                spawndObj.AddComponent<Projectile_Movement>();

            // Configure arc parameters
            var move = spawndObj.GetComponent<Projectile_Movement>();
            move.TargetPosition = targetPoint;
            move.HitTime = hitTime is -1 ? DefaultHitTime : (float)hitTime;
            move.MaxHeight = hight is -1 ? DefaultMaxHight : (float)hight;
            move.RandomRotationSpeed = rotSpeed is -1 ? DefaultRandomRotationSpeed : (float)rotSpeed;
            move.Activate();

            // impact marker
            if (Marker)
            {
                GameObject marker = Instantiate(Marker, targetPoint + Vector3.up * .05f, Quaternion.identity);
                marker.transform.localScale *= MarkerScale;
                Destroy(marker, move.HitTime);
            }

            // collision ignore with owner collider
            if (DisableCollisionBetweenSpawndObjectAndThis && Collider && spawndObj.GetComponent<Collider>())
                Physics.IgnoreCollision(spawndObj.GetComponent<Collider>(), Collider);
        }

        /// <summary>
        /// Coroutine that waits DelayBeforeSpawn, selects a projectile prefab based on ObjectSelect,
        /// computes and applies offsets, then spawns and optionally schedules destruction (Directed mode).
        /// </summary>
        IEnumerator SpawnProjectileDirectDelay(Vector3 targetPoint)
        {
            yield return new WaitForSeconds(Mathf.Max(0, DelayBeforeSpawn));

            // Optional muzzle/spawn VFX
            if (OnShootEffect)
                Destroy(Instantiate(OnShootEffect, SpawnPoint.position, Quaternion.LookRotation(targetPoint)), 5);

            int _amount = SpawnSequence == Sequence.One ? 1 : Amount;

            for (int i = 0; i < _amount; i++)
            {
                // 1) Choose projectile prefab
                GameObject projectile;
                if (ObjectSelect == ProjectileSelect.OneGameObject)
                {
                    projectile = Projectile;
                }
                else
                {
                    if (ObjectSelect == ProjectileSelect.ListOfGameObjects) objPointer += 1; // cycles forward (no wrap—by design)
                    else if (ObjectSelect == ProjectileSelect.RandomList) objPointer = Random.Range(0, Projectiles.Length);
                    projectile = Projectiles[objPointer];
                }

                // 2) Compute per-shot offset
                Vector3 offset = Vector3.zero;
                if (OffsetType == OffsetSelect.Fixed)
                {
                    offset = Offset;
                }
                else if (OffsetType == OffsetSelect.RandomBetweenTwo)
                {
                    offset = Vector3.Lerp(OffsetA, OffsetB, Random.Range(0f, 1f));
                }
                else if (OffsetType == OffsetSelect.ListOfOffsets)
                {
                    if (_offsetPointer++ >= OffestList.Length - 1) _offsetPointer = 0;
                    offset = OffestList[_offsetPointer];
                }
                else if (OffsetType == OffsetSelect.RandomListOfOffsets)
                {
                    offset = OffestList[Random.Range(0, OffestList.Length)];
                }

                // 3) Apply offset either on direction (rotation) or on position
                GameObject spawndObj;
                if(OffsetType == OffsetSelect.None)
                {
                    spawndObj = Instantiate(projectile,
                        SpawnPoint.transform.position + BaseOffset,
                        Quaternion.LookRotation(targetPoint));
                }
                else if (OffsetApply == OffestTarget.OnDirection)
                {
                    // Directional offset: rotate the forward vector by the offset.
                    // If AsRotation = true, use offset as Euler angles;
                    // otherwise, we create a small yaw/pitch tweak from offset (see code).
                    Quaternion originalRot = Quaternion.LookRotation(targetPoint);
                    Quaternion offsetRot = AsRotation
                        ? Quaternion.Euler(offset.x, offset.y, offset.z)
                        : Quaternion.Euler(offset.y * 10f, offset.x * 10f, 0f);

                    Quaternion newRot = originalRot * offsetRot;
                    spawndObj = Instantiate(projectile,
                        SpawnPoint.transform.position + BaseOffset,
                        Quaternion.LookRotation(newRot * Vector3.forward));
                }
                else // OffestTarget.OnPosition
                {
                    // Positional offset: place a temporary child and offset in local space.
                    SpawnPoint.transform.rotation = Quaternion.LookRotation(targetPoint);
                    GameObject tempPos = new("TempSpawnPos");
                    tempPos.transform.parent = SpawnPoint.transform;
                    tempPos.transform.position = SpawnPoint.position;
                    tempPos.transform.localPosition += offset;

                    spawndObj = Instantiate(projectile,
                        tempPos.transform.position + BaseOffset,
                        Quaternion.LookRotation(targetPoint));
                    Destroy(tempPos);
                }

                if (AddDamageSetter)
                {
                    DamageAmountSetter data = spawndObj.GetComponent<DamageAmountSetter>() ?
                        spawndObj.GetComponent<DamageAmountSetter>() : spawndObj.AddComponent<DamageAmountSetter>();
                    data.DamageAmount = DamageAmount;
                    data.DestroyAfterGetDamage = DestroyAfterGetDamage;
                    data.OnGetDamage = OnGetDamage;
                    data.HealthAmount = HealthAmount;
                    data.DestroyAfterGetHealth = DestroyAfterGetHealth;
                    data.OnGetHealth = OnGetHealth;
                }

                // 4) lifetime for directed projectiles
                if (LifeTime > 0) Destroy(spawndObj, LifeTime);

                // 5) collision ignore with owner collider
                if (DisableCollisionBetweenSpawndObjectAndThis && Collider && spawndObj.GetComponent<Collider>())
                    Physics.IgnoreCollision(spawndObj.GetComponent<Collider>(), Collider);

                // 6) spacing between spawns
                if (DelayBetweenEachSpawn > 0)
                    yield return new WaitForSeconds(DelayBetweenEachSpawn);
            }
        }
    }
}
