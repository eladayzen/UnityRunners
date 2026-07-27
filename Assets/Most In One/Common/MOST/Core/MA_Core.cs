using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public abstract class MOST_ActionCore
    {
        // Action enable Control, you can see it above each action menu
        [HideInInspector] public bool Enabled = true;

        // A readonly boolean represent the "IsActive" state of the action
        [HideInInspector] public bool IsPlaying { protected set; get; }

        // The Most_Actions behavior that contains this action, you can access to all Most_Actions data and functions from this Owner
        [HideInInspector] public MOST_Action Owner;

        [GUIColor("green"), Tooltip("Action name, also Work as a key, actions called by this key")]
        public string ActionName = "Unnammed Action";

        [Tooltip("Apply the action when script instance is being loaded")]
        public bool PlayOnAwake;

        [Tooltip("When this Action played, all these sub-actions will be played")]
        public string[] SubActions;

        /// <summary>
        /// Apply, Start the action internally. Use ApplyAction() to start the action
        /// </summary>
        protected abstract void Play();

        /// <summary>
        /// Stop the action internally. Use StopAction() to start the action
        /// </summary>
        protected abstract void Stop();

        /// <summary>
        /// Apply, Start the action
        /// </summary>
        public void PlayAction()
        {
            if (Enabled)
            {
                Play();
                foreach (string action in SubActions) Owner.PlaySubAction(action);
                if (Owner.EnableDebug) Debug.Log(ActionName + " Applied");
            }
            else if (Owner.EnableDebug) Debug.Log(ActionName + " called but it is disabled");
        }

        /// <summary>
        /// Stop the action and all sub-actions connected to it
        /// </summary>
        public virtual void StopAction()
        {
            Stop();
            foreach (string action in SubActions) Owner.StopSubAction(action); // Stop All sub actions
            if (Owner.EnableDebug) Debug.Log("Action of " + Owner.BaseTarget.name + " object stopped");
        }

        /// <summary>
        /// Apply, Start the action as sub action
        /// </summary>
        public void PlaySubAction()
        {
            if (Enabled) { Play(); }
        }

        /// <summary>
        /// Stop the action as sub action (don't affect local sub-actions)
        /// </summary>
        public void StopSubAction()
        {
            Stop();
        }

        /// <summary>
        /// As Unity Awake(), OnAwake is called when the script instance is being loaded
        /// </summary>
        public virtual void OnValidate() { }

        /// <summary>
        /// As Unity Awake(), OnAwake is called when the script instance is being loaded
        /// </summary>
        public virtual void OnAwake()
        {
            if (Owner.EnableDebug) Debug.Log("Action of " + Owner.BaseTarget.name + " object Initialized");
            if (PlayOnAwake) PlayAction();
        }

        /// <summary>
        /// As Unity Update(), OnUpdate is called everyframe
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// As Unity LateUpdate(), OnUpdate is called everyframe
        /// </summary>
        public virtual void OnLateUpdate() { }

        /// <summary>
        /// As OnDestroy, OnDestroy Called when this mono behavior will be destroyed
        /// </summary>
        public virtual void OnDestroy()
        {
            if (Owner.EnableDebug) Debug.Log("Action of " + Owner.BaseTarget.name + " has been destroyed");
        }
    }
}