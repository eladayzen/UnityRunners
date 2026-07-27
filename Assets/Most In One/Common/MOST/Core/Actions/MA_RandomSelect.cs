using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_RandomSelect : MOST_ActionCore
    {
        public MOSTAction_RandomSelect() { ActionName = "Random Select"; }

        [Line]
        public List<GameObject> ObjectsList;
        public bool ApplyOnValidate = true;

        public override void OnAwake()
        {
            base.OnAwake();
        }

        public override void OnValidate() // Called On Each inspector vaildation as Validate function
        {
#if UNITY_EDITOR
            base.OnValidate();
#endif
            try
            {
                if (ApplyOnValidate)
                {
                    foreach (GameObject obj in ObjectsList) obj.SetActive(false);
                    ObjectsList[UnityEngine.Random.Range(0, ObjectsList.Count)].SetActive(true);
                }
            }
            catch { /*in case that is called inside scriptable or as a ref inside an inspector*/ }
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
            foreach (GameObject obj in ObjectsList) obj.SetActive(false);
            ObjectsList[UnityEngine.Random.Range(0, ObjectsList.Count)].SetActive(true);
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