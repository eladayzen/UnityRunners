using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_RoadMovement : MonoBehaviour
    {
        [BigHeader("Main Settings")]
        [ReadOnly, GUIColor("cyan")] public string MovementDirection;
        [Tooltip("use this bool to enable or disable or check if this behavior enabled or not")]
        public bool Enable;

        [BigHeader("Movement Settings")]
        [Tooltip("The movement speed on Z axis")]
        [GUIColor("green"), Min(0)] public float ForwardSpeed = 5f;

        [Tooltip("the max speed on x axis using touch (speed if depends on touch TouchSenstivity or max if SmoothMovement enabled)")]
        [Min(0)] public float MaxSideSpeed = 5f;

        [Tooltip("the strength of reaction when touching screen")]
        public float TouchSensitivity = 1f;

        [Tooltip("instead of instant jump from point to anther, movement will be smoother but to instant")]
        public bool SmoothMovement = true;
        [Range(0.01f, 0.5f)] public float MovementSmoothing = 0.1f;

        [BigHeader("Rotation Settings")]
        [Tooltip("if enabled, the object wil rotate toward x movement direction")]
        public bool EnableRotation = true;

        [Tooltip("if enabled, the MaxSideSpeed will be used to rotate the object and moves depending on rotation (using ForwardSpeed)")]
        public bool RotateAffectMoveDirection = true;

        [Tooltip("on cool down, the speed of rotate toward forward direction")]
        [Min(0)] public float ToIdleAngleSpeed = 5f;

        [Tooltip("Delay before start resetting the rotation in seconds")]
        [Range(0, 1)] public float ToIdleDelay = 0.5f;

        [Tooltip("the max angle the object can rotate to (in positive and negative directions)")]
        [Range(0, 90)] public float MaxAngleOffset = 30f;

        [BigHeader("Road Constraints")]
        [GUIColor("red"), Tooltip("Max x axis position can object go to\nX for minimum and Y for maximum")]
        public Vector2 RoadConstraints = new(-2f, 2f);

        Vector3 _targetPosition, _currentVelocity;
        Vector2 _initialTouchPosition;
        float _touchXOffset, _rotY, _coolDown;
        bool _touched;

        void Start()
        {
            _targetPosition = transform.position;
        }

        void Update()
        {
            if (!Enable)
            {
                MovementDirection = "Idle";
                return;
            }

            InputControl();
            RotationControl();
            MoveControl();
        }

        public void EnableState(bool enable) // Main behavior enable/disable controller
        {
            Enable = enable; // Adjust this method to fit your behavior mechanism
            if (enable) // On enable
            {

            }
            else // On disable
            {

            }
        }

        void InputControl()
        {
            _coolDown -= Time.deltaTime;
            _touchXOffset = 0f;

            bool touchActive = false;

            foreach (var touch in MOSTInputCompat.Touches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        touchActive = true;
                        _initialTouchPosition = touch.position;
                        break;

                    case TouchPhase.Moved:
                        touchActive = true;
                        _coolDown = ToIdleDelay;

                        float deltaX = (touch.position.x - _initialTouchPosition.x) / Screen.width;
                        _touchXOffset = deltaX * TouchSensitivity;

                        if (!(RotateAffectMoveDirection && EnableRotation))
                            _targetPosition.x += deltaX * MaxSideSpeed;

                        _initialTouchPosition = touch.position;
                        break;

                    case TouchPhase.Stationary:
                        // still touching, no movement
                        touchActive = true;
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        // touch is not active anymore
                        _touchXOffset = 0f;
                        break;
                }

                if (touchActive) break;
            }

            _touched = touchActive;
            if (_touched) return;

            if (MOSTInputCompat.GetMouseButtonDown(0))
            {
                _initialTouchPosition = MOSTInputCompat.MousePosition;
            }

            if (MOSTInputCompat.GetMouseButton(0))
            {
                _coolDown = ToIdleDelay;

                Vector2 mp = MOSTInputCompat.MousePosition;
                float deltaX = (mp.x - _initialTouchPosition.x) / Screen.width;

                _touchXOffset = deltaX * TouchSensitivity;

                if (!(RotateAffectMoveDirection && EnableRotation))
                    _targetPosition.x += deltaX * MaxSideSpeed;

                _initialTouchPosition = mp;
            }

            if (MOSTInputCompat.GetMouseButtonUp(0))
            {
                _touchXOffset = 0f;
            }

            float keyAxis = 0f;

            if (MOSTInputCompat.GetKey(KeyCode.A) || MOSTInputCompat.GetKey(KeyCode.LeftArrow)) keyAxis -= 1f;
            if (MOSTInputCompat.GetKey(KeyCode.D) || MOSTInputCompat.GetKey(KeyCode.RightArrow)) keyAxis += 1f;

            if (Mathf.Abs(keyAxis) > 0f)
            {
                _coolDown = ToIdleDelay;
                _touchXOffset = keyAxis * TouchSensitivity;

                if (!(RotateAffectMoveDirection && EnableRotation))
                    _targetPosition.x += keyAxis * MaxSideSpeed * Time.deltaTime; // time-based step for keys
            }
        }

        void RotationControl()
        {
            if (!EnableRotation) return;

            if (Mathf.Abs(_touchXOffset) > 0.01f)
                _rotY = Mathf.Clamp(_rotY + (Mathf.Sign(_touchXOffset) * MaxSideSpeed * 100 * Time.deltaTime), -MaxAngleOffset, MaxAngleOffset);
            else if (_coolDown <= 0)
                _rotY = Mathf.MoveTowards(_rotY, 0, ToIdleAngleSpeed * 10 * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0, _rotY, 0);
        }

        void MoveControl()
        {
            MovementDirection = Mathf.Round(transform.position.x * 100) / 100 < Mathf.Round(_targetPosition.x * 100) / 100 ? "Right" :
               Mathf.Round(transform.position.x * 100) / 100 > Mathf.Round(_targetPosition.x * 100) / 100 ? "Left" : "Forward";

            _targetPosition += ForwardSpeed * Time.deltaTime * (RotateAffectMoveDirection && EnableRotation ? transform.forward : Vector3.forward);
            Vector3 newPosition = SmoothMovement ? Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, MovementSmoothing
                ) : _targetPosition;

            newPosition.x = Mathf.Clamp(newPosition.x, RoadConstraints.x, RoadConstraints.y);
            transform.position = newPosition;
            if (newPosition.x == RoadConstraints.x || newPosition.x == RoadConstraints.y) _targetPosition.x = transform.position.x;
        }
    }
}
