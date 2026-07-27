using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_Destroy : MOST_ActionCore
    {
        public MOSTAction_Destroy() { ActionName = "Destroy"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Min(0)]public float DestroyAfter;

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
            UnityEngine.Object.Destroy(TargetObject == null ? Owner.BaseTarget : TargetObject, DestroyAfter);
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
        }
    }
}