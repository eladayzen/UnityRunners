using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_CameraFollow : MOST_ActionCore
    {
        public MOSTAction_CameraFollow() { ActionName = "Camera Follow"; PlayOnAwake = true; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("Transform that will be moved by this action (usually the Camera transform or the camera rig root).")]
        [Required] public Transform Camera;

        [Line]
        [Tooltip("Automatically set 'Offset' to the current (Camera - Target) vector on validate/play, using the initially placed positions.")]
        public bool AutomaticallySetTheOffset;

        [ReadOnlyIf(nameof(AutomaticallySetTheOffset), true)]
        [Tooltip("World-space offset from the target position to the desired camera position.")]
        public Vector3 Offset = new(0, 5, -5);

        [Line]
        [Tooltip("Follow the target’s X (horizontal) movement. Disable to lock camera X.")]
        public bool FollowX = true;

        [Tooltip("Follow the target’s Y (vertical) movement. Disable to lock camera Y.")]
        public bool FollowY = true;

        [Tooltip("Follow the target’s Z (depth) movement. Disable to lock camera Z.")]
        public bool FollowZ = true;

        [Line]
        [Tooltip("Interpolate camera movement smoothly toward the target position each LateUpdate.")]
        public bool SmoothFollow;

        [HideIfAny(nameof(SmoothFollow), true, false)]
        [Tooltip("Lerp speed used when SmoothFollow is enabled (units per second scale). Higher values snap faster.")]
        public float FollowSpeed = 5f;

        [HideIfAny(nameof(SmoothFollow), true, false)]
        [Tooltip("Per-axis limit for how far the camera may overshoot beyond Target+Offset while smoothing. Acts as a clamp.")]
        public Vector3 MaxExtraOffset;

        public override void OnValidate()
        {
#if UNITY_EDITOR
            base.OnValidate();
#endif
            if (AutomaticallySetTheOffset && Camera != null)
                Offset = Camera.localPosition - (TargetObject == null ? Owner.BaseTarget : TargetObject).transform.position;
        }

        public override void OnAwake() // As Awake function
        {
            base.OnAwake();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
        }

        public override void OnLateUpdate() // Called each frame as Update function
        {
            // Calculate target position with offset
            if (!IsPlaying || !Enabled) return;
            Vector3 targetPosition = (TargetObject == null ? Owner.BaseTarget : TargetObject).transform.position + Offset;

            // Apply axis constraints
            Vector3 currentPosition = Camera.localPosition;
            if (!FollowX) targetPosition.x = currentPosition.x;
            if (!FollowY) targetPosition.y = currentPosition.y;
            if (!FollowZ) targetPosition.z = currentPosition.z;

            if (SmoothFollow)
            {
                currentPosition = Vector3.Lerp(currentPosition, targetPosition, FollowSpeed * Time.deltaTime);

                if (FollowX && currentPosition.x > targetPosition.x + MaxExtraOffset.x) currentPosition.x = targetPosition.x + MaxExtraOffset.x;
                if (FollowY && currentPosition.y > targetPosition.y + MaxExtraOffset.y) currentPosition.y = targetPosition.y + MaxExtraOffset.y;
                if (FollowZ && currentPosition.z > targetPosition.z + MaxExtraOffset.z) currentPosition.z = targetPosition.z + MaxExtraOffset.z;
                Camera.localPosition = currentPosition;
            }
            else Camera.localPosition = targetPosition;
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            IsPlaying = true;
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            IsPlaying = false;
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}
