using System;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_Events : MOST_ActionCore
    {
        public MOSTAction_Events() { ActionName = "Events"; }

        [Line]
        [Tooltip("Event On play, Use PlayAction()")]
        public UnityEvent OnPlay;

        [Line]
        [Tooltip("Event On stop, Use StopAction()")]
        public UnityEvent OnStop;

        [Line]
        [Tooltip("Event On Destroy, This Behavior or the Owner Object")]
        public UnityEvent OnBehaviorDestroy;

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
            if(Enabled) OnPlay?.Invoke();
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            if (Enabled) OnStop?.Invoke();
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            OnBehaviorDestroy?.Invoke();
        }
    }
}