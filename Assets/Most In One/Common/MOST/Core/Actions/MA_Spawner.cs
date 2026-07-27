using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_Spawner : MOST_ActionCore
    {
        public MOSTAction_Spawner() { ActionName = "Spawner"; }

        [Line, HelpBox("This is a transfrom traget base spawn system, for the advanced spawnning, Use MOST_Spawn")]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")
            , InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("When enabled, this action will target all objects under the Target object")]
        public bool TargetChildren;

        [Line]
        [Required, Tooltip("As a prefab, the object to be spawned")]
        public GameObject SpawnedObject;

        [Tooltip("If enabled, the spawned object will be set as a child under the target transform")]
        public GameObject AsChildTo;

        [Tooltip("Offset from the original transform position point")]
        public Vector3 Offset;

        [Tooltip("If enabled, the scale will follow the scale of the target transform")]
        public bool FollowParentScale;

        [Line]
        [Tooltip("If enabled, the spawned object will auto destroys after (LifeTime)")]
        public bool AutoDestruct;

        [ReadOnlyIf(nameof(AutoDestruct), false), Tooltip("Delay before the object got destroyed")]
        [Min(0)] public float LifeTime;

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

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (!Enabled) return;
            Transform _target = TargetObject == null ? Owner.BaseTarget.transform : TargetObject.transform;
            for (int i = 0; i < (TargetChildren ? _target.childCount : 1); i++)
            {
                GameObject sp = UnityEngine.Object.Instantiate(SpawnedObject, TargetChildren ? _target.GetChild(i).position + Offset
                : _target.position + Offset, Quaternion.identity, (FollowParentScale && AsChildTo != null) ? AsChildTo.transform : null);
                if (!FollowParentScale && AsChildTo != null) sp.transform.parent = AsChildTo.transform;
                if (AutoDestruct) UnityEngine.Object.Destroy(sp, LifeTime);
            }
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {

        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}

