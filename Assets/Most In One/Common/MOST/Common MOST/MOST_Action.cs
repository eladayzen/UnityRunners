using System.Collections.Generic;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    public class MOST_Action : MonoBehaviour 
    {
        [HelpBox("MOST_Action works as a manager to a collection of stuff like particals, transform animations" +
                "sound effects, materials tweaks, cameras, and a lot more.", HelpBoxKind.Info)]
        [Tooltip("Drop debug messages for all activities across Most_Actions")]
        public bool EnableDebug;

        [Tooltip("The default target for all actions under this MOST\nyou can override it inside each action")]
        [ReadOnly] public GameObject BaseTarget;

        [SerializeReference]
        List<MOST_ActionCore> actions = new();
        public IReadOnlyList<MOST_ActionCore> Actions => actions;


        #region Actions Calls
#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += Validate;
        }
#endif
        void Validate()
        {
            try { BaseTarget = gameObject; } catch { } // MOST_Action validate itelf in some cases that it not defind inside an object yet
            foreach (var e in actions) { e.Owner = this; e.OnValidate(); } // (sync) Call OnValidate for all actions
        }

        void Awake()
        {
            BaseTarget = gameObject; // Just to make sure the base has been set
            foreach (var e in actions)
            {
                e.Owner = this;
                e.OnAwake(); // (sync) Awake all actions 
            }
        }

        void Update()
        {
            foreach (var e in actions) { e.OnUpdate(); } // (sync) Call OnUpdate for all actions
        }

        void LateUpdate()
        {
            foreach (var e in actions) { e.OnLateUpdate(); } // (sync) Call OnLateUpdate for all actions
        }

        void OnDestroy()
        {
            foreach (var e in actions) { e.OnDestroy(); } // (sync) Call OnDestroy for all actions
        }

        /// <summary>
        /// return an action behavior as object 
        /// </summary>
        public object GetAction(string name) // return action as object
        {
            foreach (var e in actions) if (e.ActionName == name) return e;
            return null;
        }

        /// <summary>
        /// Apply, Play an action called by name
        /// </summary>
        public void PlayAction(string name) // Apply action by name
        {
            foreach (var e in actions) if (e.ActionName == name) e.PlayAction();
        }

        /// <summary>
        /// Apply, Play an action called by name
        /// </summary>
        public void StopAction(string name) // Apply action by name
        {
            foreach (var e in actions) if (e.ActionName == name) e.StopAction();
        }

        /// <summary>
        /// Apply, Play an action as sub-action called by name
        /// </summary>
        public void PlaySubAction(string name) // Apply action by name
        {
            foreach (var e in actions) if (e.ActionName == name) e.PlaySubAction();
        }

        /// <summary>
        /// Apply, Play an action called by name
        /// </summary>
        public void StopSubAction(string name) // Apply action by name
        {
            foreach (var e in actions) if (e.ActionName == name) e.StopSubAction();
        }

        /// <summary>
        /// Apply, Play all actions at once
        /// </summary>
        public void PlayAllActions() // Apply all actions at once
        {
            foreach (var e in actions) e.PlayAction();
        }

        private void OnDisable()
        {
            foreach (var e in actions) e.Enabled = false;
        }

        private void OnEnable()
        {
            foreach (var e in actions) e.Enabled = true;
        }

        /// <summary>
        /// Apply, Play all Camera Shake actions inside this behavior
        /// </summary>
        //public void PlayCameraShakeActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if(e.GetType() == typeof(MOSTAction_CameraShake)) e.PlayAction();
        //}

        //public void PlayHDRGlowActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_HDRGlow)) e.PlayAction();
        //}

        //public void PlayAudioSourceActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_AudioSource)) e.PlayAction();
        //}

        //public void PlayEventsActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_Events)) e.PlayAction();
        //}

        //public void PlayGrayShiftActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_GrayShift)) e.PlayAction();
        //}

        //public void PlayHapticFeedbackActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_HapticFeedback)) e.PlayAction();
        //}

        //public void PlaySpawnerActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_Spawner)) e.PlayAction();
        //}

        //public void PlayScaleAnimationActions() // apply rot action by type
        //{
        //    foreach (var e in actions) if (e.GetType() == typeof(MOSTAction_ScaleAnimation)) e.PlayAction();
        //}
        #endregion

        #region Actions Array Listing (Editor)
        public void AddEffect(MOST_ActionCore effect)
        {
            if (effect != null) actions.Add(effect);
        }

        public void RemoveEffect(MOST_ActionCore effect)
        {
            if (effect != null) actions.Remove(effect);
        }

        public void RemoveEffectAt(int num)
        {
            actions.RemoveAt(num);
        }

        public void MoveEffectUp(int index)
        {
            if (index > 0 && index < actions.Count)
            {
                (actions[index - 1], actions[index]) = (actions[index], actions[index - 1]);
            }
        }

        public void MoveEffectDown(int index)
        {
            if (index >= 0 && index < actions.Count - 1)
            {
                (actions[index + 1], actions[index]) = (actions[index], actions[index + 1]);
            }
        }
        #endregion
    }
}
