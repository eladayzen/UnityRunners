using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Linq;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System;
using TMPro;
using TouchCompat = Solo.MOST_IN_ONE.MOSTInputCompat.CompatTouch;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_Controller : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        #region Properties
        // Enums
        public enum RePositionType { ReturnToIdlePostion, StayWhereRelease }
        public enum ControllerType { StaticJoystick, DynamicJoystick, Button, MouseButton }
        public enum ControlArea { FullScreen, RightHalfOfScreen, LeftHalfOfScreen, OnMaxRange, ImageRender_CustomShape }
        public enum ReturnAxis { FreeDirection, Snap_8_Direction, Snap_4_Direction }
        public enum InputControl { ControlledByScreenTouch_Pointer, ControlledByButtons, ControlledByBoth }
        public enum BufferControl { NoBuffer, TimeBuffer, ChargeBuffer, Threshold }

        [HelpBox("MOST_Controller is a ALL-IN-ONE UI input receiver and must be attached to a UI/RectTransform object.\n" +
            "Sprites are optional; most attributes work without them. Check tooltips for details.",
            HelpBoxKind.Info)]
        // Enable / Lifecycle
        [Tooltip("Read-only flag indicating whether this controller is active. Use EnableState(bool) to change it.")]
        [ReadOnly] public bool Enable; // Quick read-only check if this behaviour is enabled

        [Tooltip("Enable this controller on Start(), or call EnableState(true) manually at runtime.")]
        public bool EnableOnStart = true;

        [Tooltip("Start the buffer/cooldown on scene awake (if BufferType is not NoBuffer).")]
        [HideIfAny(nameof(BufferType), BufferControl.NoBuffer, true)]
        public bool EnableBufferOnStart;

        [BigHeader("Input Enables")]
        [Tooltip("Enable or disable screen touch processing for this controller.")]
        public bool EnableTouch = true;

        [Tooltip("Enable or disable keyboard input (arrows/WASD) and CustomButtonKey for this controller.")]
        public bool EnableButtons;

        [Tooltip("Enable or disable swipe detection. (Hidden in Dynamic mode.)")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false, nameof(StaticOnDrag), true, true)]
        public bool EnableSwipe;

        // Controller Settings
        [BigHeader("Controller Settings")]
        [GUIColor("cyan"), Tooltip(
            "Static: Fixed-position joystick.\n" +
            "Dynamic: Joystick origin moves to the touch position (constrained by MaxRange).\n" +
            "Button: Acts like a button with continuous active output (touch/hover).\n" +
            "MouseButton: Like Button, but requires an actual press to activate."
        )]
        public ControllerType Type;

        [Tooltip("Where the joystick returns when touch ends.")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false)]
        public RePositionType OnReleasePositon;

        [Tooltip("Which input sources this controller listens to.")]
        public InputControl Controls;

        [Tooltip("Optional key that also activates this controller when held. (Only used when EnableButtons is true.)")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, true, nameof(Type), ControllerType.StaticJoystick, true)]
        public KeyCode CustomButtonKey;

        [Tooltip("How the output axis is snapped: free, 8-direction, or 4-direction (for Static/Dynamic joystick types).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public ReturnAxis DirectionSnap;

        [Tooltip("Keep the dynamic joystick fixed on its position when moving\nenable this will disable Swipe controller.")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false)]
        public bool StaticOnDrag;

        // Output Data (ReadOnly)
        [BigHeader("Output Data _ Read Only")]
        [Tooltip("True while this controller is currently receiving touch input.")]
        [ReadOnly, HideIfAll(MOSTEdit.RuntimeOnly)] public bool IsTouched;

        [Tooltip("True when the knob magnitude exceeds KnobMinActiveRange. (Touch only)")]
        [ReadOnly, HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        [HideIfAll(MOSTEdit.RuntimeOnly)] public bool ExceedMinRange;

        [Tooltip("The raw scaled output (direction × magnitude), range [-1..1] per axis.")]
        [ReadOnly, HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        [HideIfAll(MOSTEdit.RuntimeOnly)] public Vector2 RawValue;

        [Tooltip("The current normalized direction vector (no magnitude).")]
        [ReadOnly, HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        [HideIfAll(MOSTEdit.RuntimeOnly)] public Vector2 Direction;

        [Tooltip("The current magnitude of the knob, normalized to [0..1].")]
        [ReadOnly, HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        [HideIfAll(MOSTEdit.RuntimeOnly)] public float Magnitude;

        // Buffer Control
        [BigHeader("Buffer Control")]
        [GUIColor("cyan"), Tooltip("Selects the buffer mode: NoBuffer, TimeBuffer, ChargeBuffer, or Threshold.")]
        public BufferControl BufferType;

        [Tooltip("Shown while a buffer is active; hidden when the buffer finishes. (Optional)")]
        [HideIfAny(nameof(BufferType), BufferControl.NoBuffer, true)]
        [InnerHint("(Optional)")] public GameObject BufferImages;

        [Tooltip("Hidden while a buffer is active; shown when the buffer finishes. (Optional)")]
        [HideIfAny(nameof(BufferType), BufferControl.NoBuffer, true)]
        [InnerHint("(Optional)")] public GameObject OnActiveImages;

        [Line, Tooltip("Cooldown time in seconds for TimeBuffer. Set to 0 to disable.")]
        [HideIfAny(nameof(BufferType), BufferControl.TimeBuffer, false)]
        [Min(0)] public float BufferTime;

        [Tooltip("Current charge percentage for ChargeBuffer, clamped to [0..100]. (Read-only at runtime.)")]
        [ReadOnly]
        [HideIfAny(nameof(BufferType), BufferControl.ChargeBuffer, false)]
        [Range(0, 100)] public float CurrentCharge;

        [Tooltip("Animation played during TimeBuffer. Its playback speed is matched to BufferTime.")]
        [HideIfAny(nameof(BufferType), BufferControl.TimeBuffer, false)]
        [InnerHint("(Optional)")] public Animation CoolDownAnimation;

        [Tooltip("Animation played when ChargeBuffer is incremented.")]
        [HideIfAny(nameof(BufferType), BufferControl.ChargeBuffer, false)]
        [InnerHint("(Optional)")] public Animation ChargeAnimation;

        [Tooltip("Fill image for ChargeBuffer progress (0..1).")]
        [HideIfAny(nameof(BufferType), BufferControl.ChargeBuffer, false)]
        [InnerHint("(Optional)")] public Image ChargeBar;

        [Tooltip("Optional text that displays remaining seconds during TimeBuffer.")]
        [HideIfAny(nameof(BufferType), BufferControl.TimeBuffer, false)]
        public TMP_Text TimeRemainText;

        [HideIfAny(nameof(BufferType), BufferControl.Threshold, false)]
        [Tooltip("Database reference used for Threshold mode lookups.")]
        public MOST_Database DatabaseHolder;

        [Tooltip("The FloatData/IntData key used in DatabaseHolder that stores the target value for Threshold.")]
        [HideIfAny(nameof(BufferType), BufferControl.Threshold, false)]
        public string DataName;

        [Tooltip("The target value that must be reached or exceeded to enable this controller (Threshold mode).")]
        [HideIfAny(nameof(BufferType), BufferControl.Threshold, false)]
        public float Threshold;

        [Tooltip("If true, evaluate Threshold once on enable; otherwise, evaluate every frame (Threshold mode).")]
        [HideIfAny(nameof(BufferType), BufferControl.Threshold, false)]
        public bool CheckOnlyOnEnable = true;

        [Tooltip("If true, compare against HighestValue instead of current Value (Threshold mode).")]
        [HideIfAny(nameof(BufferType), BufferControl.Threshold, false)]
        public bool UseHighestValue = true;

        // UI
        [BigHeader("UI ---")]
        [Tooltip("Joystick UI knob image. (Optional)")]
        [InnerHint("(Optional)")] public Image Knob; // Optional attribute

        [Tooltip("Joystick UI background image. (Optional)")]
        [InnerHint("(Optional)")] public Image Background; // Optional attribute

        [Tooltip("defines the active area shape for the joystick\nRequired if Knob = null")]
        [InnerHint("(Required if)")] public GameObject ActivationArea;

        [Tooltip("Dynamic joystick will not move outside this Image's rect. (Optional)")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false, nameof(StaticOnDrag), true, true)]
        [InnerHint("(Optional)")] public GameObject JoystickConsitrains;

        [Tooltip("Distance from center required to trigger OnExceedMinRange(), and the return threshold to trigger OnReturnMinRange().")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        [Min(0)] public float KnobMinActiveRange;

        [Tooltip("Maximum distance (radius in pixels) the knob can travel from its center.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        [Min(0)] public float KnobMaxRange = 150; // Exactly the max distance between knob position and center

        [Tooltip("Touch distance threshold, required to consider the gesture a swipe.")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false, nameof(EnableSwipe), true, false)]
        [Min(0)] public float SwipeSinstivity = 50;

        [Tooltip("Maximum time (seconds) between touch start and end to qualify as a swipe.")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false, nameof(EnableSwipe), true, false)]
        [Min(0)] public float SwipeDisableDelta = .3f; // Both touch move and swipe work together

        //   UI Sprite Color State
        [BigHeader("UI Sprite Control")]
        [Header("_______ On Idle Color _______")]
        [Tooltip("Color applied to the knob while idle (if ApplyIdleColorOnKnob is true).")]
        public Color OnIdleColor_Knob = Color.white;

        [Tooltip("If true, apply OnIdleColor_Knob to the knob; otherwise, keep its start color.")]
        public bool ApplyIdleColorOnKnob;

        [Tooltip("Color applied to the background while idle (if ApplyIdleColorOnBackground is true).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public Color OnIdleColor_Background = Color.white;

        [Tooltip("If true, apply OnIdleColor_Background to the background; otherwise, keep its start color.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public bool ApplyIdleColorOnBackground;

        [Header("_______ On Touch Color _______")]
        [Tooltip("Color applied to the knob while being touched (if ApplyTouchColorOnKnob is true).")]
        public Color OnTouchColor_Knob = Color.white;

        [Tooltip("If true, apply OnTouchColor_Knob to the knob; otherwise, keep its start color.")]
        public bool ApplyTouchColorOnKnob;

        [Tooltip("Color applied to the background while being touched (if ApplyTouchColorOnBackground is true).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public Color OnTouchColor_Background = Color.white;

        [Tooltip("If true, apply OnTouchColor_Background to the background; otherwise, keep its start color.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public bool ApplyTouchColorOnBackground;

        [Header("_______ On Button Input Color _______")]
        [Tooltip("Color applied to the knob during keyboard/button input (if ApplyButtonColorOnKnob is true).")]
        public Color OnButtonInputColor_Knob = Color.white;

        [Tooltip("If true, apply OnButtonInputColor_Knob to the knob; otherwise, keep its start color.")]
        public bool ApplyButtonColorOnKnob;

        [Tooltip("Color applied to the background during keyboard/button input (if ApplyButtonColorOnBackground is true).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public Color OnButtonInputColor_Background = Color.white;

        [Tooltip("If true, apply OnButtonInputColor_Background to the background; otherwise, keep its start color.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public bool ApplyButtonColorOnBackground;

        [Header("_______ On CoolDown (Buffer) Color _______")]
        [Tooltip("Color applied to the knob while a buffer/cooldown is active (if ApplyCoolDownColorOnKnob is true).")]
        [HideIfAny(nameof(BufferType), BufferControl.NoBuffer, true)]
        public Color OnCoolDownColor_Knob = Color.white;

        [Tooltip("If true, apply OnCoolDownColor_Knob to the knob; otherwise, keep its start color.")]
        [HideIfAny(nameof(BufferType), BufferControl.NoBuffer, true)]
        public bool ApplyCoolDownColorOnKnob;

        [Tooltip("Color applied to the background while a buffer/cooldown is active (if ApplyCoolDownColorOnBackground is true).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true, nameof(BufferType), BufferControl.NoBuffer, true)]
        public Color OnCoolDownColor_Background = Color.white;

        [Tooltip("If true, apply OnCoolDownColor_Background to the background; otherwise, keep its start color.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true, nameof(BufferType), BufferControl.NoBuffer, true)]
        public bool ApplyCoolDownColorOnBackground;

        [Header("_______ On Disable Color _______")]
        [Tooltip("Color applied to the knob when this controller is disabled (if ApplyDisableColorOnKnob is true).")]
        public Color OnDisableColor_Knob = Color.white;

        [Tooltip("If true, apply OnDisableColor_Knob to the knob; otherwise, keep its start color.")]
        public bool ApplyDisableColorOnKnob;

        [Tooltip("Color applied to the background when this controller is disabled (if ApplyDisableColorOnBackground is true).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public Color OnDisableColor_Background = Color.white;

        [Tooltip("If true, apply OnDisableColor_Background to the background; otherwise, keep its start color.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public bool ApplyDisableColorOnBackground;

        // Stacked Controllers
        [BigHeader("Stacked Controllers Area Override")]
        [HelpBox(
            "When multiple joysticks/controllers overlap in control areas, entries here can override this joystick's area using their own rules " +
            "(e.g., Activation Area or MaxRange). You can add disabled joysticks to build complex areas.", HelpBoxKind.Info)]
        public List<MOST_Controller> OverridedByList; // You can add disabled joysticks to build complex control areas

        // Events
        [BigHeader("Events Section")]
        [Tooltip("Invoked when input is detected (joystick touched or button clicked/hovered).")]
        public UnityEvent OnInputDetected = new(); // Called when starting to receive inputs (touch or buttons).

        [Tooltip("Invoked when the joystick knob exceeds KnobMinActiveRange (touch only).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public UnityEvent<Vector2> OnExceedMinRange = new(); // Touch-only event; not triggered by buttons.

        [Tooltip("Invoked when the joystick knob returns within KnobMinActiveRange (touch only).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public UnityEvent OnReturnMinRange = new();

        [Tooltip("Invoked when the knob's raw value changes (scaled by magnitude).")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public UnityEvent<Vector2> OnRawValueChange = new(); // Works with OnDirectionChange as well; returns _magnitude-scaled vector.

        [Tooltip("Invoked whenever the controller's output direction changes.")]
        [HideIfAny(nameof(Type), ControllerType.Button, true, nameof(Type), ControllerType.MouseButton, true)]
        public UnityEvent<Vector2> OnDirectionChange = new(); // Called on each direction change.

        [Tooltip("Invoked when a swipe is detected (exactly on touch end) and conditions are met.")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, false)]
        public UnityEvent<Vector2> OnSwipeDetected = new();

        [Tooltip("Invoked when a button graphic is released (hover exit) while touch input still exists.")]
        [HideIfAny(nameof(Type), ControllerType.DynamicJoystick, true, nameof(Type), ControllerType.StaticJoystick, true)]
        public UnityEvent OnSoftRelease = new();

        public bool DontSendFalseRelease;
        [Tooltip("Invoked when all controller input stops (touch ended and button released). Returns true if MinActiveRange was exceeded.")]
        public UnityEvent<bool> OnRelease = new(); // Passes whether MinActiveRange was exceeded.

        RectTransform _spaceRT;        // coordinate space for fp/lp (parent of Background/Knob)
        RectTransform _knobRT;
        RectTransform _bgRT;
        RectTransform _constraintRT;   // JoystickConsitrains RectTransform (rect clamp)
        Canvas _canvas;
        Camera _uiCam;

        Vector2 _startOrigin;          // idle origin (Background anchoredPosition)
        Vector2 _swipeStartScreen;     // swipe measured in screen pixels
        Vector2 fp, lp, _lastDirec, _lastSwipeDirection;

        readonly Vector3[] _constraintWorldCorners = new Vector3[4];
        readonly Vector3[] _constraintLocalCorners = new Vector3[4];

        // More private runtime
        float _horizontal, _vertical, _angle, _magnitude;
        int _fingerId, _pointerId;
        bool _isInitialize, _isTouched, _isSwiped, _exceedRangeState, _OnButtonRange, _softToggle;
        Color _startBackgroundColor, _startKnobColor;
        #endregion

        #region Init + Update
        void OnEnable() // Why OnEnable() instead of Start()? bc if the controller is inactive on scene load, the controller won't init on re-enable or activation
        {
            if (_isInitialize) return;
            _isInitialize = true;

            // Cache start colors
            if (Background) _startBackgroundColor = Background.color;
            if (Knob) _startKnobColor = Knob.color;

            // UI constraint: requires Read/Write image 
            if (JoystickConsitrains)
            {
                try { JoystickConsitrains.GetComponent<Image>().alphaHitTestMinimumThreshold = .1f; }
                catch
                {
                    Debug.LogError(
                        "JoystickConsitrains attached image must be Read/Write (Texture Importer > Advanced > Read/Write Enabled).\n" +
                        "Error: Joystick constraint hit-test disabled."
                    );
                }
            }

            InitAnchoredJoystick();

            // Apply EnableOnStart
            EnableState(EnableOnStart);

            // start buffer on scene awake
            if (EnableBufferOnStart && BufferType != BufferControl.NoBuffer) StartBuffer();
            if (BufferType == BufferControl.Threshold) ThresholdCheck();
        }

        void Update()
        {
            // Threshold mode: evaluate continuously
            if (BufferType == BufferControl.Threshold && !CheckOnlyOnEnable) ThresholdCheck();

            if (Enable) // Main enable
            {
                // If button control is enabled (and joystick type is not Button/MouseButton)
                if (!(Type == ControllerType.Button || Type == ControllerType.MouseButton) &&
                    (Controls == InputControl.ControlledByButtons || Controls == InputControl.ControlledByBoth))
                    if (!_isTouched && EnableButtons) ButtonInputDetector(); // _isTouched overrides buttons if screen input is detected

                // If touch control is enabled
                if (Controls == InputControl.ControlledByScreenTouch_Pointer || Controls == InputControl.ControlledByBoth)
                    if (EnableTouch || (EnableSwipe && !StaticOnDrag))
                    {
                        if (EnableTouch && (Type == ControllerType.Button || Type == ControllerType.MouseButton)) TouchButtonDetector();
                        else if (MOSTInputCompat.TouchSupported) TouchInputDetector();
                    }

                // Expose read-only properties
                IsTouched = _isTouched;
                ExceedMinRange = _magnitude > 0;
                RawValue = GetRawValue();
                Direction = GetAxis();
                Magnitude = _magnitude;
            }
        }
        #endregion

        #region Input Detector
        void OnStart([Optional] TouchCompat touch, [Optional] PointerEventData eventData)
        {
            bool isPointer = eventData != null;
            Vector2 screenPos = isPointer ? (Vector2)eventData.position : touch.position;

            // Compare the input position with the controller's active area
            if (isPointer)
            {
                if (OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, default, screenPos)) _isTouched = true;
            }
            else
            {
                if (OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, touch)) _isTouched = true;
            }

            // Check override joysticks
            foreach (MOST_Controller joy in OverridedByList)
            {
                // feed inputPos for pointer path; keep touch path untouched
                if (isPointer)
                {
                    if (joy.OnPointerCheckUIObject(joy.ActivationArea ? joy.ActivationArea : joy.Knob.gameObject, default, screenPos)) _isTouched = false;
                }
                else
                {
                    if (joy.OnPointerCheckUIObject(joy.ActivationArea ? joy.ActivationArea : joy.Knob.gameObject, touch, touch.position)) _isTouched = false;
                }
            }

            if (!_isTouched) return; // If the input point is not in the controller active area, ignore this input

            if (!isPointer) _fingerId = touch.fingerId; // finger id only matters for touch tracking
            else _pointerId = eventData.pointerId;

            if (Type == ControllerType.DynamicJoystick && EnableSwipe && !StaticOnDrag) StartCoroutine(SwipeDurationSet()); // Start swipe detector timer

            Vector2 pos = ScreenToSpace(screenPos);
            _swipeStartScreen = pos;

            fp = (Type == ControllerType.DynamicJoystick) ? pos : _startOrigin;
            ClampOriginToConstraintRect(ref fp);
            lp = pos; // Last position

            if (Type == ControllerType.StaticJoystick) // Static: limit last position by max range and check if Exeed MinRange
            {
                if (Vector2.Distance(lp, fp) > KnobMaxRange) lp = Vector2.MoveTowards(lp, fp, Vector2.Distance(lp, fp) - KnobMaxRange);
                if (Vector2.Distance(fp, lp) > KnobMinActiveRange) // Exceeded min range
                {
                    _exceedRangeState = true;

                    // Update magnitude (0..1)
                    _magnitude = Mathf.Max(0, (Vector2.Distance(fp, lp) - KnobMinActiveRange) / (KnobMaxRange - KnobMinActiveRange));

                    // Generate direction angle (used to compute direction vector)
                    _angle = Mathf.Atan2((pos - fp).normalized.x, (pos - fp).normalized.y) * 180 / Mathf.PI;

                    if (DirectionSnap == ReturnAxis.Snap_4_Direction) _angle = Mathf.Round(_angle / 90) * 90;   // 4-way snap
                    else if (DirectionSnap == ReturnAxis.Snap_8_Direction) _angle = Mathf.Round(_angle / 45) * 45; // 8-way snap

                    _horizontal = Mathf.Sin(_angle * Mathf.PI / 180); // Output horizontal
                    _vertical = Mathf.Cos(_angle * Mathf.PI / 180); // Output vertical

                    OnExceedMinRange.Invoke(GetAxis());
                }
            }

            // Joystick position update
            DisplayJoyStick("Touch");
            SetJoystickUI(fp, lp); // Update joystick position in UI

            if (!EnableTouch) return; // if only swipe is enabled
            OnInputDetected.Invoke(); // Fire input-received event
        }

        void OnDrag([Optional] TouchCompat touch, [Optional] PointerEventData eventData)
        {
            bool isPointer = eventData != null;
            Vector2 screenPos = isPointer ? (Vector2)eventData.position : touch.position;

            Vector2 pos = ScreenToSpace(screenPos);
            lp = pos;

            float maxR = KnobMaxRange;
            float minR = KnobMinActiveRange;

            if (Type == ControllerType.DynamicJoystick)
            {
                if (StaticOnDrag)
                {
                    lp = ClampToCircle(lp, fp, maxR);
                }
                else
                {
                    float dist = Vector2.Distance(fp, lp);

                    // push origin toward finger if outside radius
                    if (dist > maxR)
                    {
                        fp = Vector2.MoveTowards(fp, lp, dist - maxR);
                        ClampOriginToConstraintRect(ref fp);
                        lp = ClampToCircle(lp, fp, maxR);
                    }
                    else
                    {
                        // keep origin clamped if constraints exist
                        Vector2 clamped = fp;
                        ClampOriginToConstraintRect(ref clamped);
                        if (clamped != fp)
                        {
                            fp = clamped;
                            lp = ClampToCircle(lp, fp, maxR);
                        }
                    }
                }
            }
            else if (Type == ControllerType.StaticJoystick)
            {
                lp = ClampToCircle(lp, fp, maxR);
            }

            Vector2 delta = lp - fp;
            float dist2 = delta.magnitude;

            // direction matches the UI knob
            Vector2 dir = (dist2 > 0.0001f) ? (delta / dist2) : Vector2.zero;

            // snap direction (4/8-way)
            dir = ApplySnap(dir);

            // snap the knob VISUALLY too so UI matches output
            if (DirectionSnap != ReturnAxis.FreeDirection && dir != Vector2.zero) lp = fp + dir * dist2;

            _horizontal = dir.x;
            _vertical = dir.y;

            // magnitude 0..1 above minRange
            if (maxR <= minR + 0.0001f) _magnitude = 0f;
            else _magnitude = Mathf.Clamp01((dist2 - minR) / (maxR - minR));

            SetJoystickUI(fp, lp);

            if (!EnableTouch) return; // if only swipe is enabled

            if (dist2 > minR && !_exceedRangeState)
            {
                _exceedRangeState = true;
                OnExceedMinRange.Invoke(GetAxis());
            }
            else if (dist2 < minR / 1.125f && _exceedRangeState)
            {
                _exceedRangeState = false;
                OnReturnMinRange.Invoke();
            }

            OnRawValueChange.Invoke(GetRawValue());
            OnDirectionChange.Invoke(GetAxis());
        }

        void OnTRelease([Optional] TouchCompat touch, [Optional] PointerEventData eventData)
        {
            bool isPointer = eventData != null;
            Vector2 screenPos = isPointer ? (Vector2)eventData.position : touch.position;

            _isTouched = false;
            if (!isPointer) _fingerId = -1;
            else _pointerId = -1;

            DisplayJoyStick("Idle");

            if (OnReleasePositon == RePositionType.ReturnToIdlePostion)
                SetJoystickUI(_startOrigin, _startOrigin);
            else
                SetJoystickUI(fp, fp);

            if (Type == ControllerType.DynamicJoystick && OnReleasePositon == RePositionType.StayWhereRelease)
                _startOrigin = fp;

            if (_isSwiped && Vector2.Distance(_swipeStartScreen, ScreenToSpace(screenPos)) > SwipeSinstivity)
            {
                _lastSwipeDirection = new Vector2(_horizontal, _vertical);
                OnSwipeDetected.Invoke(_lastSwipeDirection);
            }

            if (!EnableTouch) return; // if only swipe is enabled

            // determine exceeded based on last knob position in anchored space
            bool wasExceeded = Vector2.Distance(fp, lp) > KnobMinActiveRange;

            _exceedRangeState = false;
            _horizontal = _vertical = _magnitude = 0f;

            OnRawValueChange.Invoke(GetRawValue());

            if (!DontSendFalseRelease || wasExceeded)
                OnRelease.Invoke(wasExceeded);
        }

        // Handles touch input for controllers configured as a joystick (Static/Dynamic).
        void TouchInputDetector()
        {
            foreach (var touch in MOSTInputCompat.Touches) // Controller
            {
                if (touch.phase == TouchPhase.Began) // ___________ on Touch ___________
                {
                    if (IsTouched) continue; // Ignore other touches
                    OnStart(touch);
                }

                else if (touch.phase == TouchPhase.Moved) // ___________ on Move ___________
                {
                    if (!_isTouched || touch.fingerId != _fingerId) continue; // Ignore other touches
                    OnDrag(touch);
                }

                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) // ___________ on Release ___________
                {
                    if (!_isTouched || touch.fingerId != _fingerId) continue; // Ignore other touches
                    OnTRelease(touch);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Enable || !(EnableTouch || (EnableSwipe && !StaticOnDrag)) || !(Controls == InputControl.ControlledByScreenTouch_Pointer || Controls == InputControl.ControlledByBoth)) return;// Main enable
            if ((Type == ControllerType.StaticJoystick || Type == ControllerType.DynamicJoystick)
            && !MOSTInputCompat.TouchSupported) OnStart(default, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Enable || !(EnableTouch || (EnableSwipe && !StaticOnDrag)) || !_isTouched || !(Controls == InputControl.ControlledByScreenTouch_Pointer || Controls == InputControl.ControlledByBoth)) return;// Main enable
            if ((Type == ControllerType.StaticJoystick || Type == ControllerType.DynamicJoystick)
                && !MOSTInputCompat.TouchSupported)
            {
                if (eventData.pointerId == _pointerId) OnDrag(default, eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!Enable || !(EnableTouch || (EnableSwipe && !StaticOnDrag)) || !_isTouched || !(Controls == InputControl.ControlledByScreenTouch_Pointer || Controls == InputControl.ControlledByBoth)) return;// Main enable
            if ((Type == ControllerType.StaticJoystick || Type == ControllerType.DynamicJoystick)
                && !MOSTInputCompat.TouchSupported)
            {
                if (eventData.pointerId == _pointerId) OnTRelease(default, eventData);
            }
        }

        bool OnPointerCheckUIObject(GameObject UISprite, TouchCompat inputTouch, [Optional] Vector2 inputPos)
        {
            if (UISprite == null) return false;

            if (EventSystem.current == null)
            {
                Debug.LogWarning("MOST_Controller: No EventSystem in scene; UI raycasts are disabled.");
                return false;
            }

            Vector2 position = inputPos != Vector2.zero ? inputPos : inputTouch.position;

            PointerEventData eventData = new(EventSystem.current) { position = position };
            List<RaycastResult> results = new();

            EventSystem.current.RaycastAll(eventData, results);
            return results.Any(r => r.gameObject == UISprite || r.gameObject.transform.IsChildOf(UISprite.transform));
        }

        void TouchButtonDetector()
        {
            if (CustomButtonKey != KeyCode.None)
            {
                if (MOSTInputCompat.GetKeyDown(CustomButtonKey))
                {
                    _isTouched = true;
                    DisplayJoyStick("Touch");
                    OnInputDetected.Invoke();
                    return;
                }

                if (MOSTInputCompat.GetKey(CustomButtonKey)) { return; }

                if (MOSTInputCompat.GetKeyUp(CustomButtonKey))
                {
                    _isTouched = false;
                    OnRelease.Invoke(true);
                    DisplayJoyStick("Idle");
                    return;
                }
            }

            foreach (var touch in MOSTInputCompat.Touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    _OnButtonRange = OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, touch);
                    if (_OnButtonRange)
                    {
                        _isTouched = true;
                        _fingerId = touch.fingerId;
                        DisplayJoyStick("Touch");
                        OnInputDetected.Invoke();
                        _softToggle = true;
                    }
                }
                if (touch.phase == TouchPhase.Moved)
                {
                    if (_fingerId != touch.fingerId || (Type == ControllerType.MouseButton && !_isTouched)) continue;
                    _OnButtonRange = OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, touch);
                    if (_OnButtonRange && !_isTouched)
                    {
                        _softToggle = true;
                        _isTouched = true;
                        DisplayJoyStick("Touch");
                        OnInputDetected.Invoke();
                    }
                    if (_OnButtonRange && !_softToggle)
                    {
                        _softToggle = true;
                        DisplayJoyStick("Touch");
                    }
                    else if (!_OnButtonRange && _softToggle)
                    {
                        _softToggle = false;
                        OnSoftRelease.Invoke();
                        DisplayJoyStick("Idle");
                    }
                }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (_fingerId != touch.fingerId || (Type == ControllerType.MouseButton && !_isTouched)) continue;
                    _softToggle = false;
                    _isTouched = false;
                    if (!DontSendFalseRelease || _OnButtonRange) OnRelease.Invoke(_OnButtonRange);
                    DisplayJoyStick("Idle");
                    _fingerId = -1;
                }
            }

            if (!MOSTInputCompat.MousePresent) return;

            // --- Hover (no click) ---
            if (Type != ControllerType.MouseButton && !MOSTInputCompat.GetMouseButton(0))
            {
                _OnButtonRange = OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, default, MOSTInputCompat.MousePosition);
                if (_OnButtonRange && !_softToggle)
                {
                    _softToggle = true;
                    DisplayJoyStick("Touch"); // purely visual "hover" state
                }
                else if (!_OnButtonRange && _softToggle)
                {
                    _softToggle = false;
                    OnSoftRelease.Invoke();
                    DisplayJoyStick("Idle");
                }
            }

            // --- Press begin ---
            if (MOSTInputCompat.GetMouseButtonDown(0))
            {
                _OnButtonRange = OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, default, MOSTInputCompat.MousePosition);
                if (_OnButtonRange)
                {
                    _isTouched = true;
                    _softToggle = true;       // ensure pressed visuals
                    DisplayJoyStick("Touch");
                    OnInputDetected.Invoke(); // fires only on actual click
                }
            }

            // --- Press held / move ---
            if (MOSTInputCompat.GetMouseButton(0))
            {
                if (Type == ControllerType.MouseButton && !_isTouched) return;

                _OnButtonRange = OnPointerCheckUIObject(ActivationArea ? ActivationArea : Knob.gameObject, default, MOSTInputCompat.MousePosition);
                if (_OnButtonRange && !_isTouched)
                {
                    _softToggle = true;
                    _isTouched = true;
                    DisplayJoyStick("Touch");
                    OnInputDetected.Invoke();
                }
                if (_OnButtonRange && !_softToggle)
                {
                    _softToggle = true;
                    DisplayJoyStick("Touch");
                }
                else if (!_OnButtonRange && _softToggle)
                {
                    _softToggle = false;
                    OnSoftRelease.Invoke();
                    DisplayJoyStick("Idle");
                }
            }

            // --- Release ---
            if (MOSTInputCompat.GetMouseButtonUp(0))
            {
                if (Type == ControllerType.MouseButton && !_isTouched) return;

                _softToggle = false;
                _isTouched = false;
                if (!DontSendFalseRelease || _OnButtonRange) OnRelease.Invoke(_OnButtonRange);
                DisplayJoyStick("Idle");
            }
        }

        // Handles keyboard input (arrows/WASD) for joystick output.
        void ButtonInputDetector()
        {
            if (DirectionSnap == ReturnAxis.FreeDirection || DirectionSnap == ReturnAxis.Snap_8_Direction)
            {
                if (MOSTInputCompat.GetKey(KeyCode.UpArrow) || MOSTInputCompat.GetKey(KeyCode.W)) _vertical = 1;
                else if (MOSTInputCompat.GetKey(KeyCode.DownArrow) || MOSTInputCompat.GetKey(KeyCode.S)) _vertical = -1;
                else _vertical = 0;

                if (MOSTInputCompat.GetKey(KeyCode.LeftArrow) || MOSTInputCompat.GetKey(KeyCode.A)) _horizontal = -1;
                else if (MOSTInputCompat.GetKey(KeyCode.RightArrow) || MOSTInputCompat.GetKey(KeyCode.D)) _horizontal = 1;
                else _horizontal = 0;
            }
            else // ReturnAxis.Snap_4_Direction
            {
                if (MOSTInputCompat.GetKey(KeyCode.UpArrow) || MOSTInputCompat.GetKey(KeyCode.W)) (_horizontal, _vertical) = (0, 1);
                else if (MOSTInputCompat.GetKey(KeyCode.DownArrow) || MOSTInputCompat.GetKey(KeyCode.S)) (_horizontal, _vertical) = (0, -1);
                else if (MOSTInputCompat.GetKey(KeyCode.LeftArrow) || MOSTInputCompat.GetKey(KeyCode.A)) (_horizontal, _vertical) = (-1, 0);
                else if (MOSTInputCompat.GetKey(KeyCode.RightArrow) || MOSTInputCompat.GetKey(KeyCode.D)) (_horizontal, _vertical) = (1, 0);
                else _horizontal = _vertical = 0;
            }
            Vector2 direction = new Vector2(_horizontal, _vertical).normalized; // Output direction vector
            _horizontal = direction.x;
            _vertical = direction.y;

            if (_lastDirec != direction) // Compare with previous direction
            {
                // Update UI
                DisplayJoyStick(direction.magnitude == 0 ? "Idle" : "Button");
                var maxR = KnobMaxRange;
                SetJoystickUI(_startOrigin, _startOrigin + direction * maxR);
                if (_lastDirec.magnitude == 0) OnInputDetected.Invoke(); // First input detected
                OnDirectionChange.Invoke(GetAxis()); // Direction changed

                if (direction.magnitude == 0) OnRelease.Invoke(true); // Button released

                _lastDirec = direction;
            }
        }
        #endregion

        #region JoyStick UI
        // Applies state-based colors to knob/background without changing logic.
        void DisplayJoyStick(string state) // Same as the commented function above
        {
            var stateColors = new Dictionary<string, (Color knobColor, Color backgroundColor)> {
                { "Disable", (ApplyDisableColorOnKnob ? OnDisableColor_Knob : _startKnobColor, ApplyDisableColorOnBackground ? OnDisableColor_Background : _startBackgroundColor) },
                { "BufferCoolDown", (ApplyCoolDownColorOnKnob ? OnCoolDownColor_Knob : _startKnobColor, ApplyCoolDownColorOnBackground ? OnCoolDownColor_Background : _startBackgroundColor) },
                { "Touch", (ApplyTouchColorOnKnob ? OnTouchColor_Knob : _startKnobColor, ApplyTouchColorOnBackground ? OnTouchColor_Background : _startBackgroundColor) },
                { "Button", (ApplyButtonColorOnKnob ? OnButtonInputColor_Knob : _startKnobColor, ApplyButtonColorOnBackground ? OnButtonInputColor_Background : _startBackgroundColor) },
                { "Idle", (ApplyIdleColorOnKnob ? OnIdleColor_Knob : _startKnobColor, ApplyIdleColorOnBackground ? OnIdleColor_Background : _startBackgroundColor) }
            };
            if (stateColors.TryGetValue(state, out var colors))
            {
                if (Knob) Knob.color = colors.knobColor;
                if (Background) Background.color = colors.backgroundColor;
            }
        }

        // Enables swipe detection for SwipeDisableDelta seconds after touch begins
        IEnumerator SwipeDurationSet() // Swipe checker
        {
            _isSwiped = true;
            yield return new WaitForSeconds(SwipeDisableDelta); // After this duration, if touch hasn't ended, it's not considered a swipe
            _isSwiped = false;
        }

        Vector2 ScreenToSpace(Vector2 screenPos)
        {
            if (_spaceRT == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_spaceRT, screenPos, _uiCam, out var local);
            return local;
        }

        void SetJoystickUI(Vector2 origin, Vector2 knob)
        {
            if (_bgRT) _bgRT.anchoredPosition = origin;
            if (_knobRT) _knobRT.anchoredPosition = knob;
        }

        static Vector2 ClampToCircle(Vector2 p, Vector2 center, float radius)
        {
            var v = p - center;
            float mag = v.magnitude;
            if (mag <= radius || mag <= 0.0001f) return p;
            return center + v * (radius / mag);
        }

        void ClampOriginToConstraintRect(ref Vector2 origin)
        {
            if (_constraintRT == null || _spaceRT == null) return;

            _constraintRT.GetWorldCorners(_constraintWorldCorners);
            for (int i = 0; i < 4; i++)
                _constraintLocalCorners[i] = _spaceRT.InverseTransformPoint(_constraintWorldCorners[i]);

            // corners: 0 = BL, 1 = TL, 2 = TR, 3 = BR
            float minX = _constraintLocalCorners[0].x;
            float maxX = _constraintLocalCorners[2].x;
            float minY = _constraintLocalCorners[0].y;
            float maxY = _constraintLocalCorners[1].y;

            origin.x = Mathf.Clamp(origin.x, minX, maxX);
            origin.y = Mathf.Clamp(origin.y, minY, maxY);
        }

        Vector2 ApplySnap(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return Vector2.zero;

            switch (DirectionSnap)
            {
                case ReturnAxis.Snap_4_Direction:
                    return Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
                        ? new Vector2(Mathf.Sign(dir.x), 0f)
                        : new Vector2(0f, Mathf.Sign(dir.y));

                case ReturnAxis.Snap_8_Direction:
                    {
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        angle = Mathf.Round(angle / 45f) * 45f;
                        float rad = angle * Mathf.Deg2Rad;
                        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    }

                default:
                    return dir;
            }
        }

        void InitAnchoredJoystick()
        {
            _knobRT = Knob ? Knob.rectTransform : null;
            _bgRT = Background ? Background.rectTransform : null;

            // We move knob/background in the space of their parent
            _spaceRT =
                (_bgRT ? _bgRT.parent as RectTransform :
                (_knobRT ? _knobRT.parent as RectTransform :
                transform as RectTransform));

            _canvas = (_spaceRT ? _spaceRT.GetComponentInParent<Canvas>(true) : GetComponentInParent<Canvas>(true));
            _uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera
                : null;

            _constraintRT = JoystickConsitrains ? JoystickConsitrains.GetComponent<RectTransform>() : null;

            _startOrigin = _bgRT ? _bgRT.anchoredPosition :
                          (_knobRT ? _knobRT.anchoredPosition : Vector2.zero);

            // fp/lp are now ANCHORED positions (canvas-local units)
            fp = lp = _startOrigin;
            SetJoystickUI(_startOrigin, _startOrigin);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enable or disable the controller at runtime (also updates visual state/positions)
        /// </summary>
        /// <param name="enable"></param>
        public void EnableState(bool enable) // Main behaviour enable/disable
        {
            Enable = enable; // Adjust this method as it fits your behaviour mechanism
            if (enable) // On enable
            {
                DisplayJoyStick("Idle");
            }
            else // On disable
            {
                DisplayJoyStick("Disable");
                if (OnReleasePositon == RePositionType.StayWhereRelease)
                {
                    var pos = _bgRT ? _bgRT.anchoredPosition : _knobRT ? _knobRT.anchoredPosition : _startOrigin;
                    SetJoystickUI(pos, pos);
                }
                else SetJoystickUI(_startOrigin, _startOrigin);
            }
        }

        /// <summary>
        /// Start the active buffer/cooldown if BufferType is not NoBuffer
        /// </summary>
        /// <param name="exeeded"></param>
        public void StartBuffer(bool exeeded = true)
        {
            if (exeeded && BufferType != BufferControl.NoBuffer) StartCoroutine(ResetBuffer());
        }

        /// <summary>
        /// Returns the current normalized direction of this controller as a Vector2
        /// </summary>
        /// <returns></returns>
        public Vector2 GetAxis() // Return the current direction in Vector2
        {
            return new Vector2(_horizontal, _vertical);
        }

        /// <summary>
        /// Returns the current joystick magnitude in [0..1]
        /// </summary>
        /// <returns></returns>
        public float GetMagnitude() // Return current magnitude
        {
            return _magnitude;
        }

        /// <summary>
        /// Returns the raw output (direction × magnitude), each axis in [-1..1]
        /// </summary>
        /// <returns></returns>
        public Vector2 GetRawValue() // Return current scaled position
        {
            return GetAxis() * _magnitude;
        }

        /// <summary>
        /// Returns the last detected swipe direction (Vector2)
        /// </summary>
        /// <returns></returns>
        public Vector2 GetSwipeDirection() // Return last swipe direction
        {
            return _lastSwipeDirection;
        }

        /// <summary>
        /// Increase ChargeBuffer by 'amount' (0..100). When it reaches 100, the controller is re-enabled
        /// </summary>
        /// <param name="amount"></param>
        public void ChargeBuffer(float amount)
        {
            CurrentCharge = Mathf.Clamp(CurrentCharge + amount, 0f, 100f);
            if (ChargeAnimation) ChargeAnimation.Play();
            if (ChargeBar) ChargeBar.fillAmount = CurrentCharge / 100;
            if (CurrentCharge >= 100)
            {
                Enable = true;
                DisplayJoyStick("Idle");
                if (BufferImages) BufferImages.SetActive(false);
                if (OnActiveImages) OnActiveImages.SetActive(true);
            }
        }

        /// <summary>
        /// Evaluate Threshold mode using DatabaseHolder + DataName and toggle controller enable state accordingly
        /// </summary>
        public void ThresholdCheck()
        {
            if (DatabaseHolder == null)
            {
                Debug.LogError("MOST_Controller: DatabaseHolder is null in Threshold mode. Controller terminated");
                return;
            }

            bool check;
            var data = DatabaseHolder.GetByName(DataName);
            if (data.GetType() == typeof(FloatData))
            {
                FloatData val = (FloatData)data;
                check = (UseHighestValue ? val.HighestValue : val.Value) >= Threshold;
            }
            else if (data.GetType() == typeof(IntData))
            {
                IntData val = (IntData)data;
                check = (UseHighestValue ? val.HighestValue : val.Value) >= Threshold;
            }
            else
            {
                Debug.LogWarning("The Database entry mismatch: the data entry for Threshold must be a FloatData or IntData... Buffer terminated");
                check = true;
            }
            if (Enable != check) DisplayJoyStick(check ? "Idle" : "BufferCoolDown");
            Enable = check;

            if (BufferImages) BufferImages.SetActive(!check);
            if (OnActiveImages) OnActiveImages.SetActive(check);
        }

        // Starts or updates the active buffer based on BufferType.
        IEnumerator ResetBuffer()
        {
            Enable = false;
            DisplayJoyStick("BufferCoolDown");

            if (BufferType == BufferControl.ChargeBuffer)
            {
                if (BufferImages) BufferImages.SetActive(true);
                if (OnActiveImages) OnActiveImages.SetActive(false);
                CurrentCharge = 0;
                if (ChargeBar) ChargeBar.fillAmount = 0;
            }
            else if (BufferType == BufferControl.TimeBuffer)
            {
                float timer = BufferTime;
                if (CoolDownAnimation && timer > 0f)
                {
                    CoolDownAnimation[CoolDownAnimation.clip.name].speed = 1 / BufferTime;
                    CoolDownAnimation.Play();
                }
                if (TimeRemainText)
                {
                    TimeRemainText.gameObject.SetActive(true);
                    TimeRemainText.text = (timer - 1).ToString();
                    while (timer > 1)
                    {
                        yield return new WaitForSeconds(1);
                        timer -= 1;
                        TimeRemainText.text = (timer - 1).ToString();
                    }
                    if (timer > 0) yield return new WaitForSeconds(timer);
                    TimeRemainText.gameObject.SetActive(false);
                }
                else yield return new WaitForSeconds(timer);
                Enable = true;
                DisplayJoyStick("Idle");
            }
            else if (BufferType == BufferControl.Threshold)
            {
                ThresholdCheck();
            }
        }
        #endregion
    }

    #region Old/New Input Compat
    internal static class MOSTInputCompat
    {
        // Touch struct that mimics the fields you actually use from UnityEngine.Touch
        internal struct CompatTouch
        {
            public int fingerId;
            public Vector2 position;
            public TouchPhase phase;
        }

        /// <summary>True if a touch device exists (new system) or legacy touch is supported.</summary>
        public static bool TouchSupported
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
                if (UnityEngine.InputSystem.Touchscreen.current != null) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.touchSupported;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Returns touches from the New Input System if available, otherwise from Legacy.
        /// (Avoids double input when Project Setting = Both)
        /// </summary>
        public static IEnumerable<CompatTouch> Touches
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
                var ts = UnityEngine.InputSystem.Touchscreen.current;
                if (ts != null)
                {
                    foreach (var tc in ts.touches)
                    {
                        var phase = tc.phase.ReadValue();
                        if (phase == UnityEngine.InputSystem.TouchPhase.None)
                            continue;

                        yield return new CompatTouch
                        {
                            fingerId = tc.touchId.ReadValue(),
                            position = tc.position.ReadValue(),
                            phase = MapTouchPhase(phase),
                        };
                    }
                    yield break;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                foreach (var t in Input.touches)
                    yield return new CompatTouch { fingerId = t.fingerId, position = t.position, phase = t.phase };
#endif

                yield break;
            }
        }

        public static bool MousePresent
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
                if (UnityEngine.InputSystem.Mouse.current != null) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePresent;
#else
                return false;
#endif
            }
        }

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
                if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }

        public static bool GetMouseButton(int button)
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                return GetMouseButtonControl(mouse, button)?.isPressed ?? false;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
            return false;
#endif
        }

        public static bool GetMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                return GetMouseButtonControl(mouse, button)?.wasPressedThisFrame ?? false;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        public static bool GetMouseButtonUp(int button)
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                return GetMouseButtonControl(mouse, button)?.wasReleasedThisFrame ?? false;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(button);
#else
            return false;
#endif
        }

        public static bool GetKey(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
            if (TryGetKeyControl(keyCode, out var kc)) return kc.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(keyCode);
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
            if (TryGetKeyControl(keyCode, out var kc)) return kc.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        public static bool GetKeyUp(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
            if (TryGetKeyControl(keyCode, out var kc)) return kc.wasReleasedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(keyCode);
#else
            return false;
#endif
        }


#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM_PACKAGE
        static UnityEngine.InputSystem.Controls.ButtonControl GetMouseButtonControl(UnityEngine.InputSystem.Mouse mouse, int button) =>
            button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                _ => null
            };

        static TouchPhase MapTouchPhase(UnityEngine.InputSystem.TouchPhase p) =>
            p switch
            {
                UnityEngine.InputSystem.TouchPhase.Began => TouchPhase.Began,
                UnityEngine.InputSystem.TouchPhase.Moved => TouchPhase.Moved,
                UnityEngine.InputSystem.TouchPhase.Stationary => TouchPhase.Stationary,
                UnityEngine.InputSystem.TouchPhase.Ended => TouchPhase.Ended,
                UnityEngine.InputSystem.TouchPhase.Canceled => TouchPhase.Canceled,
                _ => TouchPhase.Canceled
            };

        static bool TryGetKeyControl(KeyCode keyCode, out UnityEngine.InputSystem.Controls.KeyControl control)
        {
            control = null;
            if (keyCode == KeyCode.None) return false;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;

            if (TryMapKeyCode(keyCode, out var key))
            {
                control = kb[key];
                return control != null;
            }

            return false;
        }

        static bool TryMapKeyCode(KeyCode keyCode, out UnityEngine.InputSystem.Key key)
        {
            // Many KeyCode names match InputSystem.Key names (W, A, S, D, UpArrow, etc.)
            if (Enum.TryParse(keyCode.ToString(), ignoreCase: true, out key))
                return true;

            // Common mismatches / aliases
            switch (keyCode)
            {
                case KeyCode.Return: key = UnityEngine.InputSystem.Key.Enter; return true;
                case KeyCode.KeypadEnter: key = UnityEngine.InputSystem.Key.NumpadEnter; return true;
                case KeyCode.LeftControl: key = UnityEngine.InputSystem.Key.LeftCtrl; return true;
                case KeyCode.RightControl: key = UnityEngine.InputSystem.Key.RightCtrl; return true;
                case KeyCode.BackQuote: key = UnityEngine.InputSystem.Key.Backquote; return true;

                // Alpha digits (optional)
                case KeyCode.Alpha0: key = UnityEngine.InputSystem.Key.Digit0; return true;
                case KeyCode.Alpha1: key = UnityEngine.InputSystem.Key.Digit1; return true;
                case KeyCode.Alpha2: key = UnityEngine.InputSystem.Key.Digit2; return true;
                case KeyCode.Alpha3: key = UnityEngine.InputSystem.Key.Digit3; return true;
                case KeyCode.Alpha4: key = UnityEngine.InputSystem.Key.Digit4; return true;
                case KeyCode.Alpha5: key = UnityEngine.InputSystem.Key.Digit5; return true;
                case KeyCode.Alpha6: key = UnityEngine.InputSystem.Key.Digit6; return true;
                case KeyCode.Alpha7: key = UnityEngine.InputSystem.Key.Digit7; return true;
                case KeyCode.Alpha8: key = UnityEngine.InputSystem.Key.Digit8; return true;
                case KeyCode.Alpha9: key = UnityEngine.InputSystem.Key.Digit9; return true;

                default:
                    key = default;
                    return false;
            }
        }
#endif
    }
    #endregion
}