using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_CameraShake : MOST_ActionCore
    {
        public MOSTAction_CameraShake() { ActionName = "Camera Shake"; }

        [Line]
        [Tooltip("Target Camera Transform\nBy default the Main camera will be the target")]
        [InnerHint("(Optional)")] public Transform TargetCamera;

        [Tooltip("Shake Duration in Seconds"), Min(0)]
        public float ShakeDuration = .6f;

        [Tooltip("Shake Strength"), Min(0)]
        public float ShakeStrength = .5f;

        float _duration = 0;
        Vector3 _ShakeOffset = Vector3.zero;
        GameObject _parentToShake;

        public override void OnAwake() // As Awake function
        {
            base.OnAwake();
            if (!TargetCamera) TargetCamera = Camera.main.transform;
            if (PlayOnAwake) PlayAction();
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
            if (!Enabled) return;
            if (_duration > 0)
            {
                if (_parentToShake == null)
                {
                    _parentToShake = TargetCamera.parent.name == "MOSTCameraShakeHelperObj"? TargetCamera.parent.gameObject : new GameObject("MOSTCameraShakeHelperObj");
                    _parentToShake.transform.parent = TargetCamera.parent;
                    TargetCamera.parent = _parentToShake.transform;
                }
                _parentToShake.transform.position -= _ShakeOffset;
                _ShakeOffset = UnityEngine.Random.insideUnitSphere * ShakeStrength;
                if ((_duration -= Time.deltaTime) > 0)
                {
                    _parentToShake.transform.position += _ShakeOffset;
                }
                else Stop();
            }
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (!Enabled) return;
            _duration = ShakeDuration;
            IsPlaying = true;
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            try
            {
                if (_parentToShake != null)
                {
                    _ShakeOffset = Vector3.zero;
                    TargetCamera.parent = _parentToShake.transform.parent;
                    UnityEngine.Object.Destroy(_parentToShake);
                }
            }
            catch { /* Unity may be destroying objects (scene change/removal), this will make a conflict. Swallow to avoid errors. */ }
            IsPlaying = false;
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}