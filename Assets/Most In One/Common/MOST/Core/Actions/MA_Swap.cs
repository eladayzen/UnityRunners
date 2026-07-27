using System;
using UnityEngine;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public struct ToggleSwap
    {
        public Toggle Controller;
        public GameObject OnTrueObject;
        public GameObject OnFalseObject;
    }

    [Serializable]
    public class MOSTAction_Swap : MOST_ActionCore
    {
        public MOSTAction_Swap() { ActionName = "Swap (Simple/Demo)"; }

        [Line]
        public ToggleSwap[] ToggleList;
        public bool AddListenersOnAwake;

        bool _wired;

        public override void OnAwake()
        {
            base.OnAwake();
            WireListeners();
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
            foreach (var swap in ToggleList) ApplySawp(swap: swap);
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            UnwireListeners();
        }

        void WireListeners() // Add Listeners for all sliders and toggles (if added)
        {
            if (_wired) return;
            _wired = true;

            foreach (var swap in ToggleList)
            {
                if (swap.Controller != null) swap.Controller.onValueChanged.AddListener(delegate { ApplySawp(swap: swap); });
            }
        }

        void UnwireListeners() // Add Listeners for all sliders and toggles (if added)
        {
            if (_wired) return;
            _wired = true;

            foreach (var swap in ToggleList)
            {
                if (swap.Controller != null) swap.Controller.onValueChanged.RemoveListener(delegate { ApplySawp(swap: swap); });
            }
        }

        void ApplySawp(ToggleSwap swap)
        {
            if (swap.OnTrueObject != null) swap.OnTrueObject.SetActive(swap.Controller.isOn);
            if (swap.OnFalseObject != null) swap.OnFalseObject.SetActive(!swap.Controller.isOn);
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}