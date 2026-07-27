using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_RotationAnimation : MOST_ActionCore
    {
        public MOSTAction_RotationAnimation() { ActionName = "Rotation Animation"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("Animate a temporary parent (proxy) instead of the target itself. The target becomes a child and follows the parent.")]
        public bool UseParentTransform;

        [Tooltip("When the action stops or is destroyed, return the animated Transform (or proxy parent) to its initial position captured at Play().")]
        public bool ReturnToStartOnStop;

        [Tooltip("Center point of the rotation (locally)")]
        public Vector3 PivotPoint;

        [Line]
        [Tooltip("the speed for each axis")]
        public Vector3 RotationAxisSpeed = Vector3.up * 180;

        [Tooltip("for variable/curve speed")]
        public AnimationCurve SpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 1f);

        GameObject _target, _parent;
        Quaternion originalRotation;
        float _animationTimer;

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
            if (!Enabled) return;
            if (IsPlaying && _target)
            {
                _animationTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_animationTimer);
                float pulseValue = SpeedCurve.Evaluate(progress);

                if (UseParentTransform && _parent)
                {
                    if (_target.transform.localPosition - PivotPoint != Vector3.zero)
                    {
                        Vector3 def = _target.transform.localPosition - PivotPoint;
                        _parent.transform.position += def;
                        _target.transform.position -= def;
                    }
                    _parent.transform.Rotate(pulseValue * Time.deltaTime * RotationAxisSpeed);
                }
                else _target.transform.Rotate(pulseValue * Time.deltaTime * RotationAxisSpeed);
                if (_animationTimer >= 1)
                {
                    _animationTimer = 0;
                }
            }
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (!Enabled) return;
            _animationTimer = 0;
            if (!IsPlaying)
            {
                IsPlaying = true;
                _target = TargetObject == null ? Owner.BaseTarget : TargetObject;
                if (UseParentTransform)
                {
                    _parent = new("Rotation Animator for " + _target.name);
                    _parent.transform.position = _target.transform.position + PivotPoint;
                    _parent.transform.parent = _target.transform.parent;
                    _target.transform.parent = _parent.transform;
                    originalRotation = _parent.transform.rotation;
                }
                else originalRotation = _target.transform.rotation;
                OnUpdate();
            }
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            try
            {
                if (_parent)
                {
                    if (_target.transform.parent = _parent.transform)
                    {
                        if (ReturnToStartOnStop) _parent.transform.rotation = originalRotation;
                        _target.transform.parent = _parent.transform.parent;
                    }
                    UnityEngine.Object.Destroy(_parent);
                }
                else if (ReturnToStartOnStop) _target.transform.rotation = originalRotation;
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