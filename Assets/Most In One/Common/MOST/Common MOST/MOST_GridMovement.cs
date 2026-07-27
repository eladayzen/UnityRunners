using System.Runtime.InteropServices;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_GridMovement : MonoBehaviour
    {
        public enum TargetAxis { X_And_Z, X_And_Y }

        [BigHeader("Enable State")] // Behavior enable flags
        [Tooltip("Read-only status indicating whether the system is active.\nUse EnableState(bool) to control.")]
        [ReadOnly] public bool Enable; // Quick read-only check for whether this behavior is enabled

        [Tooltip("Read-only flag indicating whether movement is currently active.")]
        [ReadOnly] public bool IsMoving; // Indicates if the attached character is moving

        [Tooltip("Enable on Start(), or call EnableState(true) later to enable this behavior.")]
        public bool EnableOnStart;

        [BigHeader("Behavior Enables")] // Behavior function toggles
        [Tooltip("Enable/disable movement, or check whether movement is enabled.")]
        public bool EnableMove; // Movement control toggle

        [Tooltip("Enable/disable rotation, or check whether rotation is enabled.")]
        public bool EnableRotation; // Rotation control toggle

        [BigHeader("Control Settings")]
        [Tooltip("Axes used for direction calculation and application.")]
        public TargetAxis MoveAxis;

        [Tooltip("Target object used for rotation.")]
        [InnerHint("(Optional)")] public GameObject RotateModel; // Object used as the rotation target

        [Line(1, .99f, spacing: 6)]
        [Tooltip("Screen directions depend on the camera's Y-axis rotation.\nWhen enabled, input direction is adjusted to match screen directions.")]
        public bool FixDirectionOnCameraAngle;

        [Tooltip("Camera used for direction fixing.\nDefaults to Camera.main.")]
        [InnerHint("(Optional)")][ReadOnlyIf(nameof(FixDirectionOnCameraAngle), false)] public Transform FollowedCamera;

        [BigHeader("Move Settings")]
        [Tooltip("Grid cell size.")]
        [Min(.1f)] public float GridSize = 1f; // Size of each grid cell

        [Tooltip("Movement speed applied to the transform.\nGrid movement does not require a Rigidbody, but you can use one.")]
        [Min(0)] public float Speed = 5f; // Speed of movement

        [Tooltip("Look rotation speed.")]
        [Min(0)] public float RotationSpeed = 10f; // Speed of rotation

        [Tooltip("Layers considered obstacles that block moving to the target cell.")]
        public LayerMask ObstacleLayers; // Layers that block movement

        [Tooltip("If the desired direction is blocked, switch to the last valid direction.")]
        public bool DirectionCorrector;

        [BigHeader("Effects Settings")]
        [Tooltip("Optional: plays while moving; stops when not moving.")]
        [InnerHint("(Optional)")] public ParticleSystem MoveDustFX; // FX that plays when the character moves

        [Tooltip("Optional: plays while moving; stops when not moving.")]
        [InnerHint("(Optional)")] public AudioSource MoveStepSoundEffect; // SFX that plays when the character moves

        [BigHeader("Animation Settings")]
        public bool EnableAnimationControl;

        [Tooltip("(Optional) You can disable this animation control and use your own.")]
        public Animator AnimationControl;

        [Tooltip("(Optional) Animation parameter names.")]
        public string OnMove_Bool;

        Vector3 _targetPosition;
        Quaternion _targetRotation;
        Vector2 _direction, _prevdirec;

        void Start()
        {
            _targetPosition = transform.position;
            _targetRotation = RotateModel.transform.rotation;
            EnableState(EnableOnStart); // Apply EnableOnStart
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
                /// <summary>
                /// The grid system computes only one cell jump at a time. After reaching the target cell,
                /// it resets and starts computing a new jump if movement is still enabled.
                /// This ensures the object always stops aligned to the grid.
                /// </summary>

                // Movement: if not already moving to a cell (or direction flipped), try to start a new move
                if (EnableMove && _direction.magnitude != 0 && (!IsMoving || _direction == -_prevdirec))
                    TryMove(_direction);

                // Continue moving toward the current target cell, or mark movement as finished
                if (transform.position != _targetPosition)
                    transform.position = Vector3.MoveTowards(transform.position, _targetPosition, Time.deltaTime * Speed);
                else
                    IsMoving = false; // Reached target cell; ready to compute next input

                // Rotate toward the target direction if enabled
                if (EnableRotation)
                    RotateModel.transform.rotation = Quaternion.RotateTowards(
                        RotateModel.transform.rotation,
                        _targetRotation,
                        RotationSpeed * 60 * Time.deltaTime
                    );
            }
        }

        void TryMove(Vector2 direction)
        {
            // Build the desired direction as Vector3 based on target axes (X&Y or X&Z)
            Vector3 direc = new(direction.x,
                                MoveAxis == TargetAxis.X_And_Y ? direction.y : 0,
                                MoveAxis == TargetAxis.X_And_Z ? direction.y : 0);

            // Rotation for this direction
            Quaternion rotation = Quaternion.Euler(180 * Mathf.Atan2(direction.x, direction.y) * Vector3.up / Mathf.PI);

            // New target position = current target + one grid step in the desired direction
            Vector3 newPosition = _targetPosition + direc * GridSize;

            // Raycast to detect obstacles in the next cell
            Ray ray = new(transform.position, direc);
            if (!Physics.Raycast(ray, GridSize, ObstacleLayers)) // Next cell is free
            {
                if (_prevdirec != direction) _prevdirec = direction; // Store previous direction for correction

                // Snap next target position to the grid
                _targetPosition = new Vector3(
                    Mathf.Round(newPosition.x / GridSize) * GridSize,
                    MoveAxis == TargetAxis.X_And_Y ? Mathf.Round(newPosition.y / GridSize) * GridSize : _targetPosition.y,
                    MoveAxis == TargetAxis.X_And_Z ? Mathf.Round(newPosition.z / GridSize) * GridSize : _targetPosition.z
                );

                // Store target rotation
                _targetRotation = rotation;

                VisualControl(true); // Enable movement visuals
                IsMoving = true;     // Mark as moving
            }
            else if (DirectionCorrector && _prevdirec != direction) // Blocked: try last valid direction
            {
                TryMove(_prevdirec);
            }
            else
            {
                VisualControl(false); // Failed to move; stop visuals
            }
        }

        void VisualControl(bool isMoving) // Control animations, FX, and SFX
        {
            if (EnableAnimationControl) AnimationControl.SetBool(OnMove_Bool, isMoving);
            if (MoveStepSoundEffect) MoveStepSoundEffect.mute = !isMoving;
            if (MoveDustFX)
            {
                if (isMoving) MoveDustFX.Play(); else MoveDustFX.Stop();
            }
        }

        #region Input Getter
        public void OnInputDetected() // Set direction to current rotation (optional: remove from controller if not needed on start)
        {
            _direction = new Vector2(
                RotateModel.transform.TransformDirection(Vector3.forward).x,
                RotateModel.transform.TransformDirection(Vector3.forward).z
            );
        }

        public void OnInputUpdated(Vector2 direction) // Call whenever the direction updates
        {
            if (FixDirectionOnCameraAngle)
            {
                float angle = ((FollowedCamera ? FollowedCamera.transform : Camera.main.transform).eulerAngles.y * Mathf.PI / 180)
                              + Mathf.Atan2(direction.x, direction.y); // Correct by camera angle
                _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)).normalized; // New fixed direction
            }
            else _direction = direction; // Use raw input direction
        }

        public void OnInputReleased([Optional] bool exceeded) // If using delta scaler / MOST controller, 'exceeded' may be provided
        {
            _direction = Vector2.zero; // Reset direction
            VisualControl(false);      // Stop all moving visuals
        }
        #endregion
    }
}
