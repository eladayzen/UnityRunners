using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class Detector_Core : MonoBehaviour
    {
        MOST_Detector _Dlistener;
        MOSTAction_Collision_Trigger _MAlistener;
        public void Initialize(MOST_Detector reciver)
        {
            _Dlistener = reciver;
        }
        public void Initialize(MOSTAction_Collision_Trigger reciver)
        {
            _MAlistener = reciver;
        }
        void OnTriggerEnter(Collider other)
        {
            
            _Dlistener?.OnTriggerEnterReciver(other);
            _MAlistener?.OnTriggerEnterReciver(other);
        }
        void OnTriggerExit(Collider other)
        {
            _Dlistener?.OnTriggerExitReciver(other);
            _MAlistener?.OnTriggerExitReciver(other);
        }

        void OnCollisionEnter(Collision collision)
        {
            _MAlistener?.OnCollisionEnterReciver(collision);
        }

        void OnCollisionExit(Collision collision)
        {
            _MAlistener?.OnCollisionExitReciver(collision);
        }
    }
}
