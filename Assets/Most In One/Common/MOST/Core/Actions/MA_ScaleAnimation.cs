using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_ScaleAnimation : MOST_ActionCore
    {
        public MOSTAction_ScaleAnimation() { ActionName = "Scale Animation"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("Animate a temporary parent (proxy) instead of the target itself. The target becomes a child and follows the parent.")]
        public bool UseParentTransform;

        [Tooltip("When the action stops or is destroyed, return the animated Transform (or proxy parent) to its initial position captured at Play().")]
        public bool ReturnToStartOnStop;

        [Tooltip("Center point of the scaling (locally)")]
        [HideIfAny(nameof(UseParentTransform),false,true)]
        public Vector3 PivotPoint;

        [Line]
        [Tooltip("If enabled, the animation loops continuously. If disabled, the action stops when the curve duration is reached.\nNote: For ping-pong behavior, author your curves accordingly (or extend logic to ping-pong).")]
        public bool LoopCurve = true;

        [Tooltip("Interpret curve values as absolute positions (true) or as offsets added to the original position captured at Play() (false).")]
        public bool Override;

        [Tooltip("Per-axis animation curves. Disable an axis in the curve control to keep that component unchanged.")]
        public Vector3AnimationCurves AnimationCurve;

        GameObject _target, _parent;
        Vector3 originalScale;
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
            if (IsPlaying)
            {
                _animationTimer += Time.deltaTime;
                Vector3 final = AnimationCurve.Evaluate(_animationTimer);

                if (UseParentTransform && _parent)
                {
                    if (_parent.transform.position != _target.transform.position + PivotPoint)
                    {
                        Vector3 def = _parent.transform.position - _target.transform.position + PivotPoint;
                        _parent.transform.position -= def;
                        _target.transform.position += def;
                    }
                    _parent.transform.localScale = AxisCheck(final, _parent.transform.localScale);
                }
                else _target.transform.localScale = AxisCheck(final, _target.transform.localScale);

                if (!LoopCurve && _animationTimer > AnimationCurve.GetDuration()) Stop();
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
                    _parent = new("Scale Animator for " + _target.name);
                    _parent.transform.position = _target.transform.position + PivotPoint;
                    _parent.transform.parent = _target.transform.parent;
                    _target.transform.parent = _parent.transform;
                    originalScale = Vector3.one;
                }
                else originalScale = _target.transform.localScale;
                OnUpdate();
            }
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            try
            {
                if (_parent && _target.activeInHierarchy)
                {
                    if (_target.transform.parent = _parent.transform)
                    {
                        if (ReturnToStartOnStop) _parent.transform.localScale = originalScale;
                        _target.transform.parent = _parent.transform.parent;
                    }
                    UnityEngine.Object.Destroy(_parent);
                }
                else if (ReturnToStartOnStop) _target.transform.localScale = originalScale;
            }
            catch { /* Unity may be destroying objects (scene change/removal), this will make a conflict. Swallow to avoid errors. */ }
            IsPlaying = false;
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }

        Vector3 AxisCheck(Vector3 target, Vector3 baseVec)
        {
            target.x = AnimationCurve.enableX ? (target.x + (Override ? 0 : originalScale.x)) : baseVec.x;
            target.y = AnimationCurve.enableY ? (target.y + (Override ? 0 : originalScale.y)) : baseVec.y;
            target.z = AnimationCurve.enableZ ? (target.z + (Override ? 0 : originalScale.z)) : baseVec.z;

            return target;
        }
    }
}