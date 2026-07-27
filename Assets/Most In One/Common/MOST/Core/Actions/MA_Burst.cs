using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [System.Serializable]
    public class MOSTAction_Burst : MOST_ActionCore
    {
        public MOSTAction_Burst() { ActionName = "Burst"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        public enum MotionMode { ExplosionForce, RadialImpulse, LinearVelocity }

        [BigHeader("Prefabs & Count")]
        [Tooltip("Pool of prefabs to spawn from (random pick per spawn).")]
        public GameObject[] Prefabs;

        [Tooltip("Random spawn count (X = min, Y = max), treated as integers (inclusive).")]
        public Vector2 SpawnCountRange = new(6, 12);

        [Line, BigHeader("Spawn Placement")]
        [Tooltip("Randomizes initial spawn position inside this sphere (world units) around the spawner.")]
        public float SpawnJitterRadius = 0.5f;

        [Tooltip("Optional random initial rotation for spawned pieces.")]
        public bool RandomizeRotation = true;

        [Line, BigHeader("Scale")]
        [Tooltip("Minimum scale per axis; actual scale is random between Min and Max (per axis).")]
        public Vector3 ScaleMin = Vector3.one * 0.6f;

        [Tooltip("Maximum scale per axis; actual scale is random between Min and Max (per axis).")]
        public Vector3 ScaleMax = Vector3.one * 1.4f;

        [Line, BigHeader("Motion")]
        [Tooltip("How to launch spawned pieces.")]
        public MotionMode Mode = MotionMode.ExplosionForce;

        [HideIfAny(nameof(Mode), MotionMode.ExplosionForce, false)]
        [Tooltip("Explosion force range (min..max). Used by AddExplosionForce.")]
        public Vector2 ExplosionForceRange = new(6f, 12f);

        [HideIfAny(nameof(Mode), MotionMode.ExplosionForce, false)]
        [Tooltip("Explosion radius for AddExplosionForce falloff.")]
        public float ExplosionRadius = 4f;

        [HideIfAny(nameof(Mode), MotionMode.ExplosionForce, false)]
        [Tooltip("Upward modifier for AddExplosionForce (lifts debris).")]
        [Range(0f, 2f)] public float UpwardModifier = 0.2f;

        [HideIfAny(nameof(Mode), MotionMode.RadialImpulse, false)]
        [Tooltip("Radial impulse magnitude (min..max). Applied outward from the spawner.")]
        public Vector2 RadialImpulseRange = new(6f, 12f);

        [HideIfAny(nameof(Mode), MotionMode.LinearVelocity, false)]
        [Tooltip("Minimum initial velocity per axis.")]
        public Vector3 VelocityMin = new(-2f, 3f, -2f);

        [HideIfAny(nameof(Mode), MotionMode.LinearVelocity, false)]
        [Tooltip("Maximum initial velocity per axis.")]
        public Vector3 VelocityMax = new(2f, 6f, 2f);

        [Line, BigHeader("Common Chaos")]
        [Tooltip("Extra randomized impulse added on top (ExplosionForce / RadialImpulse modes).")]
        public Vector2 ExtraImpulseRange = new(0.0f, 2.0f);

        [Tooltip("Random angular velocity magnitude applied around a random axis.")]
        public Vector2 RandomTorqueMagnitudeRange = new(1.0f, 5.0f);

        [Tooltip("Multiply existing angular velocity after applying random torque (1 = none).")]
        public float AngularVelocityMultiplier = 1.0f;

        [Line, BigHeader("Grounded Blast")]
        [Tooltip("If enabled, clamp the initial Y velocity to be positive (≥ MinUpwardY).")]
        public bool UpwardOnlyY = false;

        [HideIfAny(nameof(UpwardOnlyY), false, true)]
        [Tooltip("Minimum upward Y velocity when 'Upward Only' is enabled.")]
        public float MinUpwardY = 0.15f;

        public override void OnAwake() // As Awake function
        {
            base.OnAwake();
        }

        public override void OnValidate() // Called On Each inspector vaildation as Validate function
        {
#if UNITY_EDITOR
            base.OnValidate();
#endif
        }

        public override void OnLateUpdate() // Called each frame as LateUpdate function
        {
            base.OnLateUpdate();
        }

        public override void OnUpdate() // Called each frame as Update function
        {
            base.OnUpdate();
        }

        protected override void Play()
        {
            if (!Enabled) return;
            if (Prefabs == null || Prefabs.Length == 0)
            {
                Debug.LogWarning("[BreakSpawner] No prefabs assigned.");
                return;
            }

            // spawn count (int, inclusive)
            int minCount = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(SpawnCountRange.x, SpawnCountRange.y)));
            int maxCount = Mathf.Max(minCount, Mathf.CeilToInt(Mathf.Max(SpawnCountRange.x, SpawnCountRange.y)));
            int spawnCount = Random.Range(minCount, maxCount + 1);

            // scale ranges per axis (ensure min<=max)
            Vector3 sMin = new(
                Mathf.Min(ScaleMin.x, ScaleMax.x),
                Mathf.Min(ScaleMin.y, ScaleMax.y),
                Mathf.Min(ScaleMin.z, ScaleMax.z));
            Vector3 sMax = new(
                Mathf.Max(ScaleMin.x, ScaleMax.x),
                Mathf.Max(ScaleMin.y, ScaleMax.y),
                Mathf.Max(ScaleMin.z, ScaleMax.z));

            // common ranges
            float extraMin = Mathf.Min(ExtraImpulseRange.x, ExtraImpulseRange.y);
            float extraMax = Mathf.Max(ExtraImpulseRange.x, ExtraImpulseRange.y);
            float tqMin = Mathf.Min(RandomTorqueMagnitudeRange.x, RandomTorqueMagnitudeRange.y);
            float tqMax = Mathf.Max(RandomTorqueMagnitudeRange.x, RandomTorqueMagnitudeRange.y);

            // mode-specific ranges
            float expFMin = Mathf.Min(ExplosionForceRange.x, ExplosionForceRange.y);
            float expFMax = Mathf.Max(ExplosionForceRange.x, ExplosionForceRange.y);

            float radFMin = Mathf.Min(RadialImpulseRange.x, RadialImpulseRange.y);
            float radFMax = Mathf.Max(RadialImpulseRange.x, RadialImpulseRange.y);

            float vxMin = Mathf.Min(VelocityMin.x, VelocityMax.x);
            float vxMax = Mathf.Max(VelocityMin.x, VelocityMax.x);
            float vyMin = Mathf.Min(VelocityMin.y, VelocityMax.y);
            float vyMax = Mathf.Max(VelocityMin.y, VelocityMax.y);
            float vzMin = Mathf.Min(VelocityMin.z, VelocityMax.z);
            float vzMax = Mathf.Max(VelocityMin.z, VelocityMax.z);

            Vector3 origin = (TargetObject == null ? Owner.BaseTarget : TargetObject).transform.position;

            for (int i = 0; i < spawnCount; i++)
            {
                var prefab = Prefabs[Random.Range(0, Prefabs.Length)];
                if (!prefab) continue;

                // spawn transform
                Vector3 spawnPos = origin + Random.insideUnitSphere * SpawnJitterRadius;
                Quaternion rot = RandomizeRotation ? Random.rotation : Quaternion.identity;
                var go = Object.Instantiate(prefab, spawnPos, rot);

                // scale
                go.transform.localScale = new Vector3(
                    Random.Range(sMin.x, sMax.x),
                    Random.Range(sMin.y, sMax.y),
                    Random.Range(sMin.z, sMax.z));

                // rb
                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // outward dir
                Vector3 radialDir = (go.transform.position - origin).normalized;
                if (radialDir.sqrMagnitude < 1e-6f) radialDir = Random.onUnitSphere;

                // apply motion
                switch (Mode)
                {
                    case MotionMode.ExplosionForce:
                        {
                            float f = Random.Range(expFMin, expFMax);
                            rb.AddExplosionForce(f, origin, Mathf.Max(0.01f, ExplosionRadius), UpwardModifier, ForceMode.Impulse);

                            float extra = Random.Range(extraMin, extraMax);
                            if (extra > 0f)
                                rb.AddForce(Vector3.Slerp(radialDir, Random.onUnitSphere, 0.35f) * extra, ForceMode.Impulse);
                            break;
                        }
                    case MotionMode.RadialImpulse:
                        {
                            float f = Random.Range(radFMin, radFMax);
                            rb.AddForce(radialDir * f, ForceMode.Impulse);

                            float extra = Random.Range(extraMin, extraMax);
                            if (extra > 0f)
                                rb.AddForce(Vector3.Slerp(radialDir, Random.onUnitSphere, 0.35f) * extra, ForceMode.Impulse);
                            break;
                        }
                    case MotionMode.LinearVelocity:
                        {
                            Vector3 v = new(
                                Random.Range(vxMin, vxMax),
                                Random.Range(vyMin, vyMax),
                                Random.Range(vzMin, vzMax));
                            rb.linearVelocity = v;
                            break;
                        }
                }

                // upward-only clamp
                if (UpwardOnlyY)
                {
                    var v = rb.linearVelocity;
                    if (v.y < MinUpwardY) v.y = MinUpwardY;
                    rb.linearVelocity = v;
                }

                // random torque
                float tq = Random.Range(tqMin, tqMax);
                if (tq > 0f) rb.AddTorque(Random.onUnitSphere * tq, ForceMode.Impulse);

                if (!Mathf.Approximately(AngularVelocityMultiplier, 1f))
                    rb.angularVelocity *= AngularVelocityMultiplier;
            }
        }
        protected override void Stop(){ }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
        }
    }
}