using System;
using System.Collections;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_HapticFeedback : MOST_ActionCore
    {
        public MOSTAction_HapticFeedback() { ActionName = "Haptic Feedback"; }
        public enum HapticType { Pattern, Curve }

        public HapticType Type;
        [Line]
        [HelpBox("Setup the pattern for each platform\nfor pulse and curve testing, use Haptic examples scene", HelpBoxKind.Info)]
        [Tooltip("Cooldown before reenable"), Min(0)] public float Cooldown;
        [GUIColor("cyan"), HideIfAny(nameof(Type), HapticType.Pattern, false), Tooltip("IOS Pulse data")]
        public MOST_HapticFeedback.IOS_Haptic[] iOSHapticPattern;
        [GUIColor("cyan"), HideIfAny(nameof(Type), HapticType.Pattern, false), Tooltip("IOS Pulse data")]
        public MOST_HapticFeedback.Android_Haptic[] AndroidHapticPattern;

        [HideIfAny(nameof(Type), HapticType.Curve, false), Tooltip("IOS Pulse Curve data")]
        public MOST_HapticFeedback.iOSHapticCurve IOS_HapticCurve;
        [HideIfAny(nameof(Type), HapticType.Curve, false), Tooltip("Android Pulse Curve data")]
        public MOST_HapticFeedback.AndroidHapticCurve Android_HapticCurve;

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

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (!Enabled && IsPlaying) return;
            if (Type == HapticType.Pattern)
            {
                MOST_HapticFeedback.CustomHapticPattern pt = new(iosHapticPattern: iOSHapticPattern, androidHapticPattern: AndroidHapticPattern);
                MOST_HapticFeedback.GeneratePattern(pt);
                Owner.StartCoroutine(HapticTimer(pt.GetDuration()));
            }
            else
            {
                MOST_HapticFeedback.HapticCurve pattern = new(IOS_HapticCurve, Android_HapticCurve);
                MOST_HapticFeedback.GenerateCurve(pattern);
                Owner.StartCoroutine(HapticTimer(pattern.GetDuration()));
            }
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {

        }

        IEnumerator HapticTimer(float duration)
        {
            IsPlaying = true;
            yield return new WaitForSeconds(duration + Cooldown);
            IsPlaying = false;
        }

        #region Basic Haptics
        // __________________________________ Basic Haptics __________________________________
        public void SelectionHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Selection);
        }

        public void SuccessHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Success);
        }

        public void WarningHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Warning);
        }

        public void FailureHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Failure);
        }

        public void LightImpactHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);
        }

        public void MediumImpactHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);
        }

        public void HeavyImpactHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.HeavyImpact);
        }

        public void RigidImpactHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.RigidImpact);
        }

        public void SoftImpactHaptic()
        {
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.SoftImpact);
        }

        // __________________________________ Basic Haptics with Cooldown __________________________________ 
        public void SelectionHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Selection, cooldown);
        }

        public void SuccessHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Success, cooldown);
        }

        public void WarningHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Warning, cooldown);
        }

        public void FailureHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Failure, cooldown);
        }

        public void LightImpactHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.LightImpact, cooldown);
        }

        public void MediumImpactHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.MediumImpact, cooldown);
        }

        public void HeavyImpactHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.HeavyImpact, cooldown);
        }

        public void RigidImpactHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.RigidImpact, cooldown);
        }

        public void SoftImpactHapticWithCooldown(float cooldown)
        {
            MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.SoftImpact, cooldown);
        }
        #endregion
    }
}