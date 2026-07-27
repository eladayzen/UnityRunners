using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_PositionAnimation : MOST_ActionCore
    {
        public MOSTAction_PositionAnimation() { ActionName = "Position Animation"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action animates this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("Animate a temporary parent (proxy) instead of the target itself. The target becomes a child and follows the parent.")]
        public bool UseParentTransform;

        [Tooltip("When the action stops or is destroyed, return the animated Transform (or proxy parent) to its initial position captured at Play().")]
        public bool ReturnToStartOnStop;

        [Tooltip("Animate localPosition instead of world-space position.")]
        public bool UseLocalPosition;

        [Line]
        [Tooltip("If enabled, the animation loops continuously. If disabled, the action stops when the curve duration is reached.\nNote: For ping-pong behavior, author your curves accordingly (or extend logic to ping-pong).")]
        public bool LoopCurve;

        [Tooltip("Interpret curve values as absolute positions (true) or as offsets added to the original position captured at Play() (false).")]
        public bool Override;

        [Tooltip("Per-axis animation curves. Disable an axis in the curve control to keep that component unchanged.")]
        public Vector3AnimationCurves AnimationCurve;

        GameObject _target, _parent;
        Vector3 _originalPosition;
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
                    if (UseLocalPosition) _parent.transform.localPosition = AxisCheck(final, _parent.transform.localPosition);
                    else _parent.transform.position = AxisCheck(final, _parent.transform.position);
                }
                else if (UseLocalPosition) _target.transform.localPosition = AxisCheck(final, _target.transform.localPosition);
                else _target.transform.position = AxisCheck(final, _target.transform.position);

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
                    _parent = new("Position Animator for " + _target.name);
                    _parent.transform.position = _target.transform.position;
                    _parent.transform.parent = _target.transform.parent;
                    _target.transform.parent = _parent.transform;
                    if (UseLocalPosition) _originalPosition = _parent.transform.localPosition;
                    else _originalPosition = _parent.transform.position;
                }
                else if (UseLocalPosition) _originalPosition = _target.transform.localPosition;
                else _originalPosition = _target.transform.position;
                OnUpdate();
            }
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            IsPlaying = false;
            try
            {
                if (_parent)
                {
                    if (_target.transform.parent == _parent.transform)
                    {
                        if (ReturnToStartOnStop) _parent.transform.position = _originalPosition;
                        _target.transform.parent = _parent.transform.parent;
                    }
                    UnityEngine.Object.Destroy(_parent);
                }
                else if (ReturnToStartOnStop) _target.transform.position = _originalPosition;
            }
            catch { /* Unity may be destroying objects (scene change/removal), this will make a conflict. Swallow to avoid errors. */ }
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }

        Vector3 AxisCheck(Vector3 target, Vector3 baseVec)
        {
            target.x = AnimationCurve.enableX ? (target.x + (Override ? 0 : _originalPosition.x)) : baseVec.x;
            target.y = AnimationCurve.enableY ? (target.y + (Override ? 0 : _originalPosition.y)) : baseVec.y;
            target.z = AnimationCurve.enableZ ? (target.z + (Override ? 0 : _originalPosition.z)) : baseVec.z;

            return target;
        }
    }
}
