using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class NinjaMode : MonoBehaviour
    {
        string _currentMode = "KATANA";
        public MOST_FreeMovement MovementSystem;
        public MOST_Aim AimSystem;
        [BigHeader("Katana Mode")]
        public GameObject KatanaModel;
        public Animator KatanaAnimator;
        [BigHeader("Shuriken Mode")]
        public GameObject ShurikenModel;
        public Animator ShurikenAnimator;

        public UnityEvent OnSwitch;

        public void SwapToShuriken()
        {
            if (_currentMode == "SHURIKEN") return;
            _currentMode = "SHURIKEN";

            ShurikenModel.SetActive(true);
            ShurikenModel.transform.rotation = KatanaModel.transform.rotation;
            ShurikenAnimator.SetBool("Run", KatanaAnimator.GetBool("Run"));

            KatanaModel.SetActive(false);
            
            MovementSystem.RotateModel = ShurikenModel;
            MovementSystem.AnimationControl = ShurikenAnimator;

            AimSystem.AnimationControl = ShurikenAnimator;
            AimSystem.RotateModel = ShurikenModel;

            AimSystem.AngleScale = 30;
            AimSystem.Radius = 12;
            AimSystem.InnerCut = 0;
            AimSystem.SideOffsetDistance = 1.5f;

            AimSystem.Clear();
            AimSystem.CreateRay();

            OnSwitch.Invoke();
        }

        public void SwapToKatana()
        {
            if (_currentMode == "KATANA") return;
            _currentMode = "KATANA";

            KatanaModel.SetActive(true);
            KatanaModel.transform.rotation = ShurikenModel.transform.rotation;
            KatanaAnimator.SetBool("Run", ShurikenAnimator.GetBool("Run"));

            ShurikenModel.SetActive(false);

            MovementSystem.RotateModel = KatanaModel;
            MovementSystem.AnimationControl = KatanaAnimator;

            AimSystem.AnimationControl = KatanaAnimator;
            AimSystem.RotateModel = KatanaModel;

            AimSystem.AngleScale = 130;
            AimSystem.Radius = 5;
            AimSystem.InnerCut = 1.2f;
            AimSystem.SideOffsetDistance = 0f;

            AimSystem.Clear();
            AimSystem.CreateRay();

            OnSwitch.Invoke();
        }
    }
}