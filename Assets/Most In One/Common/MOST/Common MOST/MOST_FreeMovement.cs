using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [RequireComponent(typeof(Rigidbody))]
    [HideScriptField]
    public class MOST_FreeMovement : MonoBehaviour
    {
        public enum TargetAxis { X_And_Z, X_And_Y }

        [BigHeader("Enable State")] // Behavior enable flags
        [Tooltip("Read-only status indicating whether the system is active.\nUse EnableState(bool) to control.")]
        [ReadOnly] public bool Enable; // Quick read-only check for whether this behavior is enabled

        [Tooltip("Read-only flag indicating whether the movement function is currently active.")]
        [ReadOnly] public bool IsMoving; // Indicates if the attached character is moving right now

        [Tooltip("Read-only flag indicating whether the dashing function is currently active.")]
        [ReadOnly] public bool IsDashing; // Indicates if the attached character is currently dashing

        [Tooltip("Enable on Start(), or call EnableState(true) later to enable this behavior.")]
        public bool EnableOnStart;

        [BigHeader("Behavior Enables")] // Behavior function toggles
        [Tooltip("Enable/disable movement, or check whether movement is enabled.")]
        public bool EnableMove = true; // Movement control toggle

        [Tooltip("Enable/disable rotation, or check whether rotation is enabled.")]
        public bool EnableRotation = true; // Rotation control toggle

        [Tooltip("Enable/disable dashing, or check whether dashing is enabled.")]
        public bool EnableDash; // Dash control toggle

        [BigHeader("Control Settings")]
        [Tooltip("Target axes used for direction calculation and application.")]
        public TargetAxis MoveAxis;

        [Tooltip("Target object used for rotation.")]
        [InnerHint("(Optional)")] public GameObject RotateModel; // Object used as the rotation target

        [Line(1, .99f, spacing: 6)]
        [Tooltip("Screen directions depend on the camera's Y-axis rotation.\nWhen enabled, input direction is adjusted to match screen directions.")]
        public bool FixDirectionOnCameraAngle;

        [Tooltip("Camera used to correct direction.\nDefaults to Camera.main.")]
        [InnerHint("(Optional)")][ReadOnlyIf(nameof(FixDirectionOnCameraAngle), false)] public Transform FollowedCamera;

        [BigHeader("Move Settings")]
        [Tooltip("Movement speed applied to the Rigidbody.")]
        [Min(0)] public float Speed = 5;

        [Tooltip("Dashing speed; together with DashDuration determines the dash distance.")]
        [Min(0)] public float DashSpeed = 10;

        [Tooltip("Duration used by animation control to disable the dashing animation.")]
        [Min(0)] public float DashDuration = .9f; // Fit for different animations or modifiers

        [Tooltip("Look rotation speed.")]
        [Min(0)] public float RotationSpeed = 15f;

        [BigHeader("Effects Settings")]
        [Tooltip("Plays while moving; stops when not moving.")]
        [InnerHint("(Optional)")] public ParticleSystem MoveDustFX; // FX that plays when the character moves

        [Tooltip("Plays while moving; stops when not moving.")]
        [InnerHint("(Optional)")] public AudioSource MoveStepSoundEffect; // SFX that plays when the character moves

        [BigHeader("Animation Settings")]
        public bool EnableAnimationControl;
        
        [InnerHint("(Optional)"), Tooltip("You can disable this animation control and use your own.")]
        public Animator AnimationControl;

        [Tooltip("(Optional) Animation parameter names.")]
        public string OnMove_Bool, OnSwipe_Bool;

        Vector2 _swipeTarget, _direction;
        Rigidbody rb;

        void Start()
        {
            rb = GetComponent<Rigidbody>(); // The target Rigidbody must be attached to this GameObject
            EnableState(EnableOnStart);      // Apply EnableOnStart
        }

        public void EnableState(bool enable) // Main behavior enable/disable controller
        {
            Enable = enable; // Adjust this method to fit your behavior mechanism
            if (enable) // On enable
            {

            }
            else // On disable
            {
                VisualControl(false);
            }
        }

        void Update()
        {
            if (Enable) // Main enable
            {
                rb.linearVelocity = Vector3.zero;
                if (EnableMove && IsMoving && !IsDashing) // Movement
                {
                    rb.linearVelocity = Speed * new Vector3(
                        _direction.x,                                       // Apply velocity
                        MoveAxis == TargetAxis.X_And_Y ? _direction.y : rb.linearVelocity.y,  // If target axes are X & Y
                        MoveAxis == TargetAxis.X_And_Z ? _direction.y : rb.linearVelocity.z   // If target axes are X & Z
                    );

                    // Rotate toward direction if enabled
                    if (EnableRotation)
                        RotateModel.transform.rotation = Quaternion.Slerp(
                            RotateModel.transform.rotation,
                            Quaternion.Euler(180 * Mathf.Atan2(_direction.x, _direction.y) * Vector3.up / Mathf.PI),
                            RotationSpeed * Time.deltaTime
                        );
                }
                else if (EnableDash && IsDashing) // Dashing
                {
                    rb.linearVelocity = DashSpeed * new Vector3(
                        _swipeTarget.x,                                      // Apply dash velocity
                        MoveAxis == TargetAxis.X_And_Y ? _swipeTarget.y : rb.linearVelocity.y, // If target axes are X & Y
                        MoveAxis == TargetAxis.X_And_Z ? _swipeTarget.y : rb.linearVelocity.z  // If target axes are X & Z
                    );
                }
            }
        }

        IEnumerator DashControl() // Dashing (auto enable/disable) + handling dash animations
        {
            VisualControl(false); // Stop all moving animations and FX

            IsDashing = true; // Apply dashing and dashing animations
            if (EnableAnimationControl) AnimationControl.SetBool(OnSwipe_Bool, true);

            yield return new WaitForSeconds(DashDuration);

            IsDashing = false; // After DashDuration, stop dashing and dashing animations
            if (EnableAnimationControl) AnimationControl.SetBool(OnSwipe_Bool, false);

            if (_direction.magnitude != 0) VisualControl(true); // If still moving, re-enable moving visuals
        }

        void VisualControl(bool isMoving) // Control animations, FX, and SFX
        {
            if (EnableAnimationControl) AnimationControl.SetBool(OnMove_Bool, isMoving);
            if (MoveStepSoundEffect)
            {
                if (isMoving) MoveStepSoundEffect.Play(); else MoveStepSoundEffect.Stop();
            }
            if (MoveDustFX) // Toggle move FX
            {
                if (isMoving) MoveDustFX.Play(); else MoveDustFX.Stop();
            }
        }

        public void RotationEnable(bool enable) // Rotation enable/disable controller
        {
            EnableRotation = enable;
        }

        #region Input Getter
        /// <summary>
        /// Difference between OnInputDetected() and OnExceedMinRange(Vector2 direction):
        /// - Use OnInputDetected() to start moving immediately on touch; direction is based on current rotation.
        /// - Use OnExceedMinRange(Vector2) to start moving only after the finger moves on screen beyond a minimum distance.
        ///   Configure the minimum distance in the controller (MinDelta).
        /// Hook the desired method in your controller’s events: OnInputDetected or OnExceedMinRange.
        /// </summary>
        public void OnInputDetected() // Enable movement and set direction to current rotation
        {
            IsMoving = true; // Enable movement

            // Calculate direction based on current rotation
            if (RotateModel)
                _direction = new Vector2(
                    RotateModel.transform.TransformDirection(Vector3.forward).x,
                    RotateModel.transform.TransformDirection(Vector3.forward).z
                );
            VisualControl(true); // Enable visual control
        }

        public void OnExceedMinRange(Vector2 direction) // Enable movement and set direction to input direction
        {
            IsMoving = true; // Enable movement

            if (FixDirectionOnCameraAngle)
            {
                float angle = ((FollowedCamera ? FollowedCamera : Camera.main.transform).eulerAngles.y * Mathf.PI / 180)
                              + Mathf.Atan2(direction.x, direction.y); // Correct direction by camera angle
                _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // New fixed direction
            }
            else _direction = direction; // Use raw input direction
            VisualControl(true); // Enable visual control
        }

        public void OnInputUpdated(Vector2 direction) // Call whenever the direction updates
        {
            if (direction.magnitude != 0) // Apply only valid direction; use OnInputReleased() to stop
            {
                if (FixDirectionOnCameraAngle)
                {
                    float angle = ((FollowedCamera ? FollowedCamera : Camera.main.transform).eulerAngles.y * Mathf.PI / 180)
                                  + Mathf.Atan2(direction.x, direction.y); // Correct direction by camera angle
                    _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // New fixed direction
                }
                else _direction = direction; // Use raw input direction
            }
        }

        public void OnRawValueUpdated(Vector2 direction) // Call whenever the raw joystick delta updates
        {
            if (direction.magnitude != 0) // Apply only valid direction; use OnInputReleased() to stop
            {
                if (FixDirectionOnCameraAngle)
                {
                    float angle = ((FollowedCamera ? FollowedCamera : Camera.main.transform).eulerAngles.y * Mathf.PI / 180)
                                  + Mathf.Atan2(direction.x, direction.y); // Correct direction by camera angle
                    _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * direction.magnitude; // Fixed direction scaled by magnitude
                }
                else _direction = direction; // Use raw input direction
            }
        }

        public void OnSwipeDetected([Optional] Vector2 direction)
        {
            if (direction != Vector2.zero)
            {
                if (FixDirectionOnCameraAngle)
                {
                    float angle = ((FollowedCamera ? FollowedCamera : Camera.main.transform).eulerAngles.y * Mathf.PI / 180)
                                  + Mathf.Atan2(direction.x, direction.y); // Correct direction by camera angle
                    direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // New fixed direction
                }
                if (RotateModel)
                    RotateModel.transform.LookAt(new Vector3(
                        direction.x,                                         // Apply dash rotation direction
                        MoveAxis == TargetAxis.X_And_Y ? direction.y : 0,    // If target axes are X & Y
                        MoveAxis == TargetAxis.X_And_Z ? direction.y : 0     // If target axes are X & Z
                    ) + transform.position);
                _swipeTarget = direction; // Apply dashing direction
            }
            else Debug.LogWarning("There is no input direction for swipe.");
            StartCoroutine(DashControl()); // Enable auto dashing controller
        }

        public void OnAutoSwipeDetected()
        {
            if (RotateModel)
                _swipeTarget = new Vector2(
                    RotateModel.transform.TransformDirection(Vector3.forward).x,
                    RotateModel.transform.TransformDirection(Vector3.forward).z
                );
            else Debug.LogWarning("There is no RotateModel for auto swipe direction.");
            StartCoroutine(DashControl()); // Enable auto dashing controller
        }

        public void OnInputReleased([Optional] bool exceeded) // Use with delta scaler or MOST controller if applicable
        {
            _direction = Vector2.zero; // Reset direction
            IsMoving = false;          // Stop moving
            VisualControl(false);      // Stop all moving visuals
        }
        #endregion
    }
}
