using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    // For all details, How to use MOST_Gate and APIs
    // https://solo-player.gitbook.io/most-in-one/most-systems/most-gate

    [HideScriptField]
    public class MOST_Gate : MonoBehaviour
    {
        public enum GateType { Health, FireRate, FireRange, Upgrade, AddChilds, Currency, Other }

        // Structured source of truth. GateText / OutputText are generated legacy strings.
        // Boolean is appended so enum indexes from the previous structured version stay stable.
        public enum GateOperator { Add = 0, Subtract = 1, Multiply = 2, Divide = 3, Custom = 4, Boolean = 5 }

        // Kept for old API compatibility. In the original script, Subtract represented the divide sign (÷).
        public enum GateSign
        {
            Plus = 0,
            Minus = 1,
            Subtract = 2,
            Multiply = 3,
            NoSign = 4,
            Divide = Subtract,
            BooleanTrue = 5,
            BooleanFalse = 6
        }

        [Serializable]
        public struct ParsedGateValue
        {
            [Tooltip("Parsed operation represented by a legacy text value or structured gate value.")]
            public GateOperator Operation;

            [Tooltip("Parsed numeric value. Any legacy trailing k suffixes are baked into this value during parsing.")]
            public float Value;

            [Tooltip("Parsed boolean value when Operation is Boolean.")]
            public bool BooleanValue;

            [Tooltip("Parsed custom text when Operation is Custom.")]
            public string CustomText;

            [Tooltip("True when Operation is Add, Subtract, Multiply, or Divide.")]
            public bool IsMath;

            [Tooltip("True when Operation is Boolean.")]
            public bool IsBoolean;
        }

        [BigHeader("Main Settings")]
        [Tooltip("Runtime state. True after this gate has been collected.")]
        [ReadOnly] public bool IsCollected;

        [Tooltip("Gameplay category for this gate, such as Health, Currency, Upgrade, or a custom project-specific type.")]
        public GateType Type;

        [Tooltip("Structured operation used to generate the visible gate text and calculate the gate effect.")]
        public GateOperator GateOperation = GateOperator.Add;

        [Tooltip("Numeric value used only by Add, Subtract, Multiply, and Divide operations. Legacy k suffixes are baked into this value during migration.")]
        [HideIfAny(nameof(_showGateNumberFields), true, false)]
        [Min(0)] public float GateValue = 1f;

        [Tooltip("Scales Add/Subtract calculation values only. Example: +1000 with AmountRescale 0.01 returns +10. Ignored by Multiply, Divide, Boolean, and Custom gates.")]
        [HideIfAny(nameof(_showAmountRescale), true, false)]
        [Min(0)] public float AmountRescale = 1;

        [Tooltip("Boolean value used only when GateOperation is Boolean. True displays True and uses the positive model; False displays False and uses the negative model.")]
        [HideIfAny(nameof(GateOperation), GateOperator.Boolean, false)]
        public bool GateBooleanValue = true;

        [Tooltip("Custom text used only when GateOperation is Custom. Custom gates are non-math gates and return -1 from Calculation.")]
        [HideIfAny(nameof(GateOperation), GateOperator.Custom, false)]
        public string GateCustomText = string.Empty;

        [Tooltip("Hide the leading + sign on the displayed gate text. Only used by Add gates.")]
        [HideIfAny(nameof(GateOperation), GateOperator.Add, false)]
        public bool HidePositiveSign;

        // Legacy serialized text values. Hidden, but intentionally kept so old prefabs/scripts can be migrated safely.
        [Tooltip("Legacy generated gate text kept for old prefabs and backward-compatible scripts. Do not edit directly in new setup.")]
        [HideInInspector] public string GateText = "+1";

        [Tooltip("Legacy generated fixed output text kept for old prefabs and backward-compatible scripts. Do not edit directly in new setup.")]
        [HideInInspector] public string OutputText = string.Empty;

        [SerializeField, HideInInspector] string _lastSyncedGateText;
        [SerializeField, HideInInspector] string _lastSyncedOutputText;

        // Inspector-only visibility flags used by HideIfAny.
        // They let us hide fields based on the actual active operation, including Fixed Output.
        [SerializeField, HideInInspector] bool _showGateNumberFields = true;
        [SerializeField, HideInInspector] bool _showOutputNumberFields;
        [SerializeField, HideInInspector] bool _showAmountRescale = true;

        [Tooltip("Database used when this gate type writes to a currency entry.")]
        [HideIfAll(nameof(Type), GateType.Currency, false, nameof(Type), GateType.Upgrade, false)]
        [BigHeader("Currency Set")]
        public MOST_Database CurrencyDataHolder;

        [Tooltip("Currency key/name used inside CurrencyDataHolder.")]
        [HideIfAll(nameof(Type), GateType.Currency, false, nameof(Type), GateType.Upgrade, false)]
        public string CurrencyName;

        [BigHeader("Fixed Output")]
        [Tooltip("Use a separate structured value for calculations instead of the main visible gate value.")]
        public bool EnableFixedOutput;

        [Tooltip("When enabled, Positive/Negative/Custom models are selected from the fixed output value instead of the visible gate value.")]
        [HideIfAny(nameof(EnableFixedOutput), true, false)]
        public bool FixedOutputControlGates;

        [Tooltip("Structured operation used by fixed output calculations.")]
        [HideIfAny(nameof(EnableFixedOutput), true, false)]
        public GateOperator OutputOperation = GateOperator.Custom;

        [Tooltip("Numeric fixed output value used only by Add, Subtract, Multiply, and Divide output operations. Legacy k suffixes are baked into this value during migration.")]
        [HideIfAny(nameof(_showOutputNumberFields), true, false)]
        [Min(0)] public float OutputValue = 0f;

        [Tooltip("Boolean fixed output value used only when OutputOperation is Boolean.")]
        [HideIfAny(nameof(EnableFixedOutput), true, false, nameof(OutputOperation), GateOperator.Boolean, false)]
        public bool OutputBooleanValue = false;

        [Tooltip("Custom fixed output text used only when OutputOperation is Custom.")]
        [HideIfAny(nameof(EnableFixedOutput), true, false, nameof(OutputOperation), GateOperator.Custom, false)]
        public string OutputCustomText = string.Empty;

        [BigHeader("Gate Models")]
        [Tooltip("Model enabled when the active value is positive: Add, Multiply, or Boolean True.")]
        [InnerHint("(Optional)")]
        public GameObject PositiveModel;

        [Tooltip("Model enabled when the active value is negative: Subtract, Divide, or Boolean False.")]
        [InnerHint("(Optional)")]
        public GameObject NegativeModel;

        [Tooltip("Model enabled when the active value is Custom or cannot be classified as positive/negative.")]
        [InnerHint("(Optional)")]
        public GameObject CustomModel;

        [Tooltip("Text component used to display the generated main gate text.")]
        [InnerHint("(Optional)")]
        public TMP_Text GatePost;

        [Tooltip("Text component used to display the generated fixed output text.")]
        [HideIfAny(nameof(EnableFixedOutput), true, false)]
        [InnerHint("(Optional)")]
        public TMP_Text FixedOutputText;

        [BigHeader("Upgrade Settings")]
        [Tooltip("Allow runtime trigger objects to change the main numeric gate value. Hidden for Boolean and Custom gates.")]
        [HideIfAny(nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public bool EnableUpgrade;

        [Tooltip("Layers allowed to upgrade this gate when EnableUpgrade is active.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public LayerMask TriggerLayers;

        [Tooltip("Tags allowed to upgrade this gate when EnableUpgrade is active. Matching tag or matching layer is enough.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public string[] TriggerTags;

        [Line]
        [Tooltip("Amount added to positive operations or subtracted from negative operations on each valid upgrade trigger.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public float IncreaseAmount;

        [Tooltip("Cooldown between valid upgrade triggers.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        [Min(0)] public float IncreaseCooldown;

        [Tooltip("Destroy the object that upgraded this gate after a valid trigger hit.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public bool DestroyColliderOnHit = true;

        [Line]
        [Tooltip("Event invoked when runtime upgrades cross or reach zero and the operation flips sign.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public UnityEvent OnZeroEvent;

        [Tooltip("Optional object spawned when OnZeroEvent is invoked.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        [InnerHint("(Optional)")]
        public GameObject OnZeroEventSpawn;

        [Tooltip("World position offset used when spawning OnZeroEventSpawn.")]
        [HideIfAny(nameof(EnableUpgrade), true, false, nameof(GateOperation), GateOperator.Custom, true, nameof(GateOperation), GateOperator.Boolean, true)]
        public Vector3 SpawnOffset;

        [BigHeader("Pair Gates")]
        [Tooltip("Optional paired gate. When this gate is collected, the paired gate is also marked collected and disabled.")]
        [InnerHint("(Optional)")]
        public MOST_Gate PairedGate;

        [BigHeader("Animations")]
        [Tooltip("Optional legacy Animation component used for upgrade and collect clips.")]
        [InnerHint("(Optional)")]
        public Animation Animation;

        [Tooltip("Animation clip name played after this gate is upgraded.")]
        public string OnUpgradeAnimation;

        [Tooltip("Animation clip name played when this gate is collected.")]
        public string OnCollectAnimation;

        [BigHeader("Effects")]
        [Tooltip("Optional object spawned when this gate is collected.")]
        [InnerHint("(Optional)")]
        public GameObject OnCollectSpawn;

        [Tooltip("Travel time used by JumpToward projectile movement.")]
        [Min(0)] public float HitTime;

        [Tooltip("Maximum jump height used by JumpToward projectile movement.")]
        [Min(0)] public float MaxHight;

        GameObject _activeModel;
        float _cooldown;

#if UNITY_EDITOR
        void OnValidate()
        {
            RefreshInspectorVisibilityFlags();
            UnityEditor.EditorApplication.delayCall -= Validate;
            UnityEditor.EditorApplication.delayCall += Validate;
        }
#endif

        void Awake() { Validate(); }
        void Update() { _cooldown -= Time.deltaTime; }

        void Validate()
        {
            if (!this) return;

            RefreshInspectorVisibilityFlags();
            SyncLegacyTextChanges();

            if (PositiveModel) PositiveModel.SetActive(false);
            if (NegativeModel) NegativeModel.SetActive(false);
            if (CustomModel) CustomModel.SetActive(false);

            ParsedGateValue modelSource = GetModelSourceValue();
            if (NegativeModel && IsNegativeModelValue(modelSource))
            {
                NegativeModel.SetActive(true);
                _activeModel = NegativeModel;
            }
            else if (PositiveModel && IsPositiveModelValue(modelSource))
            {
                PositiveModel.SetActive(true);
                _activeModel = PositiveModel;
            }
            else if (CustomModel)
            {
                CustomModel.SetActive(true);
                _activeModel = CustomModel;
            }
            else
            {
                _activeModel = null;
            }

            if (GatePost) GatePost.text = GetVisibleGateText();
            if (FixedOutputText) FixedOutputText.text = GetOutputText();
        }

        void OnTriggerEnter(Collider collider)
        {
            if (IsCollected || !EnableUpgrade || !IsMathOperator(GateOperation)) return;
            if (!ColliderMatchesUpgradeRules(collider)) return;

            if (DestroyColliderOnHit) Destroy(collider.gameObject);
            if (_cooldown > 0) return;

            _cooldown = IncreaseCooldown;

            float nextValue = GateValue;
            if (GateOperation == GateOperator.Subtract || GateOperation == GateOperator.Divide)
                nextValue -= IncreaseAmount;
            else
                nextValue += IncreaseAmount;

            if (nextValue <= 0f)
            {
                GateOperation = FlipOperation(GateOperation);
                GateValue = Mathf.Abs(nextValue);
                OnZeroEvent?.Invoke();
                if (OnZeroEventSpawn)
                    Destroy(Instantiate(OnZeroEventSpawn, transform.position + SpawnOffset, Quaternion.identity), 5);
            }
            else
            {
                GateValue = nextValue;
            }

            SyncLegacyStringsFromValues();
            Validate();

            if (Animation && !string.IsNullOrEmpty(OnUpgradeAnimation))
                Animation.Play(OnUpgradeAnimation);
        }

        public float Calculation(float score)
        {
            Validate();
            CollectOnce();

            ParsedGateValue value = GetActiveValue();
            if (!value.IsMath) return -1; // Boolean/custom gate, same calculation behaviour as old True/False/custom text.

            float amount = value.Value;
            switch (value.Operation)
            {
                case GateOperator.Add:
                    return score + amount * AmountRescale;
                case GateOperator.Subtract:
                    return score - amount * AmountRescale;
                case GateOperator.Multiply:
                    return score * amount;
                case GateOperator.Divide:
                    if (Mathf.Approximately(amount, 0f))
                    {
                        Debug.LogWarning("Gate division by zero ignored.", this);
                        return score;
                    }
                    return score / amount;
                default:
                    return -1;
            }
        }

        public float Calculation()
        {
            Validate();
            ParsedGateValue value = GetActiveValue();
            CollectOnce();

            if (!value.IsMath) return -1; // Boolean/custom gate.

            float amount = value.Value;

            if (Type == GateType.Currency && CurrencyDataHolder != null && !string.IsNullOrEmpty(CurrencyName))
                CurrencyDataHolder.Get<IntData>(CurrencyName).Add(Mathf.RoundToInt(amount));

            switch (value.Operation)
            {
                case GateOperator.Add:
                    return amount * AmountRescale;
                case GateOperator.Subtract:
                    return -amount * AmountRescale;
                case GateOperator.Multiply:
                case GateOperator.Divide:
                    return amount;
                default:
                    return -1;
            }
        }

        public GateSign GetSign()
        {
            ParsedGateValue value = GetActiveValue();
            switch (value.Operation)
            {
                case GateOperator.Add: return GateSign.Plus;
                case GateOperator.Subtract: return GateSign.Minus;
                case GateOperator.Multiply: return GateSign.Multiply;
                case GateOperator.Divide: return GateSign.Divide;
                case GateOperator.Boolean: return value.BooleanValue ? GateSign.BooleanTrue : GateSign.BooleanFalse;
                default: return GateSign.NoSign;
            }
        }

        public GateOperator GetOperator()
        {
            return GetActiveValue().Operation;
        }

        public bool TryGetBooleanValue(out bool value)
        {
            ParsedGateValue activeValue = GetActiveValue();
            if (activeValue.Operation == GateOperator.Boolean)
            {
                value = activeValue.BooleanValue;
                return true;
            }

            value = false;
            return false;
        }

        public bool GetBooleanValue(bool fallback = false)
        {
            bool value;
            return TryGetBooleanValue(out value) ? value : fallback;
        }

        public string GetGateText()
        {
            SyncLegacyTextChanges();
            return FormatGateText(GateOperation, GateValue, GateCustomText, GateBooleanValue);
        }

        public string GetVisibleGateText()
        {
            string text = GetGateText();
            return HidePositiveSign && text.StartsWith("+") ? text.Substring(1) : text;
        }

        public string GetOutputText()
        {
            SyncLegacyTextChanges();
            return FormatGateText(OutputOperation, OutputValue, OutputCustomText, OutputBooleanValue);
        }

        // Preferred setter for math/custom gates. For Boolean, value > 0 becomes True and 0 becomes False.
        public void SetGateValue(GateOperator operation, float value, string customText = null, bool refresh = true)
        {
            bool booleanValue = operation == GateOperator.Boolean
                ? ValueOrTextToBool(value, customText, GateBooleanValue)
                : GateBooleanValue;

            SetGateValue(operation, value, customText, booleanValue, refresh);
        }

        // Compatibility overload for the previous structured version. legacyKCount is baked into value and not stored.
        public void SetGateValue(GateOperator operation, float value, int legacyKCount, string customText = null, bool refresh = true)
        {
            SetGateValue(operation, ApplyLegacyKMultiplier(operation, value, legacyKCount), customText, refresh);
        }

        // Compatibility overload for the previous structured version. legacyKCount is baked into value and not stored.
        public void SetGateValue(GateOperator operation, float value, int legacyKCount, string customText, bool booleanValue, bool refresh)
        {
            SetGateValue(operation, ApplyLegacyKMultiplier(operation, value, legacyKCount), customText, booleanValue, refresh);
        }

        public void SetGateValue(GateOperator operation, float value, string customText, bool booleanValue, bool refresh)
        {
            GateOperation = operation;
            GateValue = value;
            GateBooleanValue = booleanValue;
            if (customText != null) GateCustomText = customText;
            NormalizeValue(ref GateOperation, ref GateValue, ref GateCustomText, ref GateBooleanValue);
            SyncLegacyStringsFromValues();
            if (refresh) Validate();
        }

        public void SetGateValue(ParsedGateValue parsed, bool refresh = true)
        {
            ApplyParsedValue(parsed, toGate: true);
            SyncLegacyStringsFromValues();
            if (refresh) Validate();
        }

        public void SetGateBooleanValue(bool value, bool refresh = true)
        {
            SetGateValue(GateOperator.Boolean, value ? 1f : 0f, null, value, refresh);
        }

        public void SetOutputValue(GateOperator operation, float value, string customText = null, bool refresh = true)
        {
            bool booleanValue = operation == GateOperator.Boolean
                ? ValueOrTextToBool(value, customText, OutputBooleanValue)
                : OutputBooleanValue;

            SetOutputValue(operation, value, customText, booleanValue, refresh);
        }

        // Compatibility overload for the previous structured version. legacyKCount is baked into value and not stored.
        public void SetOutputValue(GateOperator operation, float value, int legacyKCount, string customText = null, bool refresh = true)
        {
            SetOutputValue(operation, ApplyLegacyKMultiplier(operation, value, legacyKCount), customText, refresh);
        }

        // Compatibility overload for the previous structured version. legacyKCount is baked into value and not stored.
        public void SetOutputValue(GateOperator operation, float value, int legacyKCount, string customText, bool booleanValue, bool refresh)
        {
            SetOutputValue(operation, ApplyLegacyKMultiplier(operation, value, legacyKCount), customText, booleanValue, refresh);
        }

        public void SetOutputValue(GateOperator operation, float value, string customText, bool booleanValue, bool refresh)
        {
            OutputOperation = operation;
            OutputValue = value;
            OutputBooleanValue = booleanValue;
            if (customText != null) OutputCustomText = customText;
            NormalizeValue(ref OutputOperation, ref OutputValue, ref OutputCustomText, ref OutputBooleanValue);
            SyncLegacyStringsFromValues();
            if (refresh) Validate();
        }

        public void SetOutputValue(ParsedGateValue parsed, bool refresh = true)
        {
            ApplyParsedValue(parsed, toGate: false);
            SyncLegacyStringsFromValues();
            if (refresh) Validate();
        }

        public void SetOutputBooleanValue(bool value, bool refresh = true)
        {
            SetOutputValue(GateOperator.Boolean, value ? 1f : 0f, null, value, refresh);
        }

        public void SetGateValueFromText(string text, bool refresh = true)
        {
            SetGateValue(ParseGateText(text), refresh);
        }

        public void SetOutputValueFromText(string text, bool refresh = true)
        {
            SetOutputValue(ParseGateText(text), refresh);
        }

        public void ImportLegacyTextsNow(bool refresh = true)
        {
            ParsedGateValue parsedGate = ParseGateText(GateText);
            ParsedGateValue parsedOutput = ParseGateText(OutputText);
            ApplyParsedValue(parsedGate, toGate: true);
            ApplyParsedValue(parsedOutput, toGate: false);
            SyncLegacyStringsFromValues();
            if (refresh) Validate();
        }

        public static ParsedGateValue ParseGateText(string rawText)
        {
            string text = (rawText ?? string.Empty).Trim();
            if (text.Length == 0)
                return MakeCustomParsed(string.Empty);

            bool boolValue;
            if (TryParseBooleanText(text, out boolValue))
                return MakeBooleanParsed(boolValue);

            GateOperator op;
            char sign = text[0];
            if (!TrySignToOperation(sign, out op))
                return MakeCustomParsed(text);

            string valuePart = text.Substring(1).Trim();
            int legacyKCount = 0;
            while (valuePart.EndsWith("k", StringComparison.OrdinalIgnoreCase))
            {
                legacyKCount++;
                valuePart = valuePart.Substring(0, valuePart.Length - 1).TrimEnd();
            }

            float value;
            if (!TryParseFloat(valuePart, out value))
                return MakeCustomParsed(text);

            value = ApplyLegacyKMultiplier(op, value, legacyKCount);

            string custom = string.Empty;
            bool booleanPlaceholder = false;
            NormalizeValue(ref op, ref value, ref custom, ref booleanPlaceholder);

            return new ParsedGateValue
            {
                Operation = op,
                Value = value,
                BooleanValue = booleanPlaceholder,
                CustomText = custom,
                IsMath = IsMathOperator(op),
                IsBoolean = op == GateOperator.Boolean
            };
        }

        public static GateOperator ParseOperator(string signOrName, GateOperator fallback = GateOperator.Multiply)
        {
            if (string.IsNullOrWhiteSpace(signOrName)) return fallback;

            GateOperator op;
            string trimmed = signOrName.Trim();
            if (TrySignToOperation(trimmed[0], out op)) return op;

            string normalized = trimmed.ToLowerInvariant();
            switch (normalized)
            {
                case "add":
                case "plus":
                case "positive":
                    return GateOperator.Add;
                case "subtract":
                case "minus":
                case "negative":
                    return GateOperator.Subtract;
                case "multiply":
                case "times":
                    return GateOperator.Multiply;
                case "divide":
                case "division":
                    return GateOperator.Divide;
                case "bool":
                case "boolean":
                case "true":
                case "false":
                    return GateOperator.Boolean;
                case "custom":
                    return GateOperator.Custom;
                default:
                    return fallback;
            }
        }

        public static string FormatGateText(GateOperator operation, float value, string customText = null)
        {
            bool boolValue = operation == GateOperator.Boolean
                ? ValueOrTextToBool(value, customText, false)
                : false;

            return FormatGateText(operation, value, customText, boolValue);
        }

        // Compatibility overload for the previous structured version. legacyKCount is baked into value and no k suffix is generated.
        public static string FormatGateText(GateOperator operation, float value, int legacyKCount, string customText)
        {
            bool boolValue = operation == GateOperator.Boolean
                ? ValueOrTextToBool(value, customText, false)
                : false;

            return FormatGateText(operation, ApplyLegacyKMultiplier(operation, value, legacyKCount), customText, boolValue);
        }

        // Compatibility overload for the previous structured version. legacyKCount is baked into value and no k suffix is generated.
        public static string FormatGateText(GateOperator operation, float value, int legacyKCount, string customText, bool booleanValue)
        {
            return FormatGateText(operation, ApplyLegacyKMultiplier(operation, value, legacyKCount), customText, booleanValue);
        }

        public static string FormatGateText(GateOperator operation, float value, string customText, bool booleanValue)
        {
            NormalizeValue(ref operation, ref value, ref customText, ref booleanValue);

            if (operation == GateOperator.Boolean)
                return FormatBooleanText(booleanValue);

            if (operation == GateOperator.Custom)
                return customText ?? string.Empty;

            return OperationToSign(operation) + FormatNumber(value);
        }

        public static string FormatGateText(ParsedGateValue parsed)
        {
            return FormatGateText(parsed.Operation, parsed.Value, parsed.CustomText, parsed.BooleanValue);
        }

        public void JumpToward()
        {
            gameObject.AddComponent<Projectile_Movement>();
            GetComponent<Projectile_Movement>().UseGravityAtEnd = false;
            GetComponent<Projectile_Movement>().HitTime = HitTime;
            GetComponent<Projectile_Movement>().MaxHeight = MaxHight + transform.position.y;
            GetComponent<Projectile_Movement>().RandomRotationSpeed = 0;
            GetComponent<Projectile_Movement>().Activate(GameObject.FindGameObjectWithTag("Character").transform);
        }

        public void DestroyGate()
        {
            Destroy(gameObject);
        }

        void RefreshInspectorVisibilityFlags()
        {
            _showGateNumberFields = IsMathOperator(GateOperation);
            _showOutputNumberFields = EnableFixedOutput && IsMathOperator(OutputOperation);

            GateOperator activeOperation = EnableFixedOutput ? OutputOperation : GateOperation;
            _showAmountRescale = activeOperation == GateOperator.Add || activeOperation == GateOperator.Subtract;
        }

        ParsedGateValue GetActiveValue()
        {
            return EnableFixedOutput ? GetOutputValueStruct() : GetGateValueStruct();
        }

        ParsedGateValue GetModelSourceValue()
        {
            return EnableFixedOutput && FixedOutputControlGates ? GetOutputValueStruct() : GetGateValueStruct();
        }

        ParsedGateValue GetGateValueStruct()
        {
            NormalizeValue(ref GateOperation, ref GateValue, ref GateCustomText, ref GateBooleanValue);
            return new ParsedGateValue
            {
                Operation = GateOperation,
                Value = GateValue,
                BooleanValue = GateBooleanValue,
                CustomText = GateCustomText,
                IsMath = IsMathOperator(GateOperation),
                IsBoolean = GateOperation == GateOperator.Boolean
            };
        }

        ParsedGateValue GetOutputValueStruct()
        {
            NormalizeValue(ref OutputOperation, ref OutputValue, ref OutputCustomText, ref OutputBooleanValue);
            return new ParsedGateValue
            {
                Operation = OutputOperation,
                Value = OutputValue,
                BooleanValue = OutputBooleanValue,
                CustomText = OutputCustomText,
                IsMath = IsMathOperator(OutputOperation),
                IsBoolean = OutputOperation == GateOperator.Boolean
            };
        }

        void ApplyParsedValue(ParsedGateValue parsed, bool toGate)
        {
            if (toGate)
            {
                GateOperation = parsed.Operation;
                GateValue = parsed.Value;
                GateBooleanValue = parsed.BooleanValue;
                GateCustomText = parsed.CustomText;
                NormalizeValue(ref GateOperation, ref GateValue, ref GateCustomText, ref GateBooleanValue);
            }
            else
            {
                OutputOperation = parsed.Operation;
                OutputValue = parsed.Value;
                OutputBooleanValue = parsed.BooleanValue;
                OutputCustomText = parsed.CustomText;
                NormalizeValue(ref OutputOperation, ref OutputValue, ref OutputCustomText, ref OutputBooleanValue);
            }
        }

        void SyncLegacyTextChanges()
        {
            SyncOneLegacyValue(ref GateText, ref _lastSyncedGateText, ref GateOperation, ref GateValue, ref GateCustomText, ref GateBooleanValue);
            SyncOneLegacyValue(ref OutputText, ref _lastSyncedOutputText, ref OutputOperation, ref OutputValue, ref OutputCustomText, ref OutputBooleanValue);
        }

        void SyncLegacyStringsFromValues()
        {
            NormalizeValue(ref GateOperation, ref GateValue, ref GateCustomText, ref GateBooleanValue);
            NormalizeValue(ref OutputOperation, ref OutputValue, ref OutputCustomText, ref OutputBooleanValue);

            GateText = FormatGateText(GateOperation, GateValue, GateCustomText, GateBooleanValue);
            OutputText = FormatGateText(OutputOperation, OutputValue, OutputCustomText, OutputBooleanValue);
            _lastSyncedGateText = GateText;
            _lastSyncedOutputText = OutputText;
        }

        static void SyncOneLegacyValue(
            ref string legacyText,
            ref string lastSyncedText,
            ref GateOperator operation,
            ref float value,
            ref string customText,
            ref bool booleanValue)
        {
            legacyText = legacyText ?? string.Empty;
            customText = customText ?? string.Empty;
            NormalizeValue(ref operation, ref value, ref customText, ref booleanValue);

            string composed = FormatGateText(operation, value, customText, booleanValue);
            bool legacyWasChangedExternally = !string.IsNullOrWhiteSpace(legacyText) && legacyText != lastSyncedText && legacyText != composed;

            if (legacyWasChangedExternally)
            {
                ParsedGateValue parsed = ParseGateText(legacyText);
                operation = parsed.Operation;
                value = parsed.Value;
                booleanValue = parsed.BooleanValue;
                customText = parsed.CustomText;
                NormalizeValue(ref operation, ref value, ref customText, ref booleanValue);
                composed = FormatGateText(operation, value, customText, booleanValue);
            }

            legacyText = composed;
            lastSyncedText = legacyText;
        }

        bool ColliderMatchesUpgradeRules(Collider collider)
        {
            if (!collider) return false;

            if (TriggerTags != null)
            {
                foreach (string tag in TriggerTags)
                {
                    if (!string.IsNullOrEmpty(tag) && collider.gameObject.CompareTag(tag))
                        return true;
                }
            }

            return (TriggerLayers.value & (1 << collider.gameObject.layer)) != 0;
        }

        void CollectOnce()
        {
            if (IsCollected) return;

            if (Animation && !string.IsNullOrEmpty(OnCollectAnimation))
                Animation.Play(OnCollectAnimation);

            if (PairedGate)
            {
                PairedGate.IsCollected = true;
                if (PairedGate.Animation && !string.IsNullOrEmpty(PairedGate.OnCollectAnimation))
                    PairedGate.Animation.Play(PairedGate.OnCollectAnimation);

                Collider pairedCollider = PairedGate.GetComponent<Collider>();
                if (pairedCollider) pairedCollider.enabled = false;
            }

            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider) ownCollider.enabled = false;

            if (OnCollectSpawn) Destroy(Instantiate(OnCollectSpawn, transform.position, Quaternion.identity), 5);

            if (_activeModel && _activeModel.activeInHierarchy)
            {
                AudioSource source = _activeModel.GetComponent<AudioSource>();
                if (source) source.Play();
            }

            IsCollected = true;
        }

        static GateOperator FlipOperation(GateOperator operation)
        {
            switch (operation)
            {
                case GateOperator.Add: return GateOperator.Subtract;
                case GateOperator.Subtract: return GateOperator.Add;
                case GateOperator.Multiply: return GateOperator.Divide;
                case GateOperator.Divide: return GateOperator.Multiply;
                default: return operation;
            }
        }

        static bool IsPositiveModelValue(ParsedGateValue value)
        {
            if (value.Operation == GateOperator.Boolean) return value.BooleanValue;
            if (value.Operation == GateOperator.Custom)
            {
                bool boolValue;
                return TryParseBooleanText(value.CustomText, out boolValue) && boolValue;
            }

            return value.Operation == GateOperator.Add || value.Operation == GateOperator.Multiply;
        }

        static bool IsNegativeModelValue(ParsedGateValue value)
        {
            if (value.Operation == GateOperator.Boolean) return !value.BooleanValue;
            if (value.Operation == GateOperator.Custom)
            {
                bool boolValue;
                return TryParseBooleanText(value.CustomText, out boolValue) && !boolValue;
            }

            return value.Operation == GateOperator.Subtract || value.Operation == GateOperator.Divide;
        }

        static bool IsMathOperator(GateOperator operation)
        {
            return operation == GateOperator.Add
                   || operation == GateOperator.Subtract
                   || operation == GateOperator.Multiply
                   || operation == GateOperator.Divide;
        }

        static bool TrySignToOperation(char sign, out GateOperator operation)
        {
            switch (sign)
            {
                case '+':
                    operation = GateOperator.Add;
                    return true;
                case '-':
                case '−':
                    operation = GateOperator.Subtract;
                    return true;
                case 'x':
                case 'X':
                case '*':
                case '×':
                    operation = GateOperator.Multiply;
                    return true;
                case '÷':
                case '/':
                    operation = GateOperator.Divide;
                    return true;
                default:
                    operation = GateOperator.Custom;
                    return false;
            }
        }

        static string OperationToSign(GateOperator operation)
        {
            switch (operation)
            {
                case GateOperator.Add: return "+";
                case GateOperator.Subtract: return "-";
                case GateOperator.Multiply: return "x";
                case GateOperator.Divide: return "÷";
                default: return string.Empty;
            }
        }

        static void NormalizeValue(ref GateOperator operation, ref float value, ref string customText, ref bool booleanValue)
        {
            customText = customText ?? string.Empty;

            bool parsedBool;
            if (operation == GateOperator.Custom && TryParseBooleanText(customText, out parsedBool))
            {
                operation = GateOperator.Boolean;
                booleanValue = parsedBool;
            }

            if (operation == GateOperator.Boolean)
            {
                value = booleanValue ? 1f : 0f;
                customText = string.Empty;
                return;
            }

            if (operation == GateOperator.Custom)
            {
                value = 0f;
                return;
            }

            booleanValue = false;

            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
            if (value < 0f)
            {
                value = Mathf.Abs(value);
                if (operation == GateOperator.Add) operation = GateOperator.Subtract;
                else if (operation == GateOperator.Subtract) operation = GateOperator.Add;
            }
        }

        static ParsedGateValue MakeCustomParsed(string text)
        {
            return new ParsedGateValue
            {
                Operation = GateOperator.Custom,
                Value = 0f,
                BooleanValue = false,
                CustomText = text ?? string.Empty,
                IsMath = false,
                IsBoolean = false
            };
        }

        static ParsedGateValue MakeBooleanParsed(bool value)
        {
            return new ParsedGateValue
            {
                Operation = GateOperator.Boolean,
                Value = value ? 1f : 0f,
                BooleanValue = value,
                CustomText = string.Empty,
                IsMath = false,
                IsBoolean = true
            };
        }

        static bool TryParseFloat(string text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return float.TryParse(text, out value);
        }

        public static bool TryParseBooleanText(string text, out bool value)
        {
            text = (text ?? string.Empty).Trim();
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        public static string FormatBooleanText(bool value)
        {
            return value ? "True" : "False";
        }

        static bool ValueOrTextToBool(float value, string text, bool fallback)
        {
            bool parsed;
            if (TryParseBooleanText(text, out parsed)) return parsed;
            if (!Mathf.Approximately(value, 0f)) return true;
            if (Mathf.Approximately(value, 0f)) return false;
            return fallback;
        }

        static string FormatNumber(float value)
        {
            if (Mathf.Approximately(value, Mathf.Round(value)))
                return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static float ApplyLegacyKMultiplier(GateOperator operation, float value, int legacyKCount)
        {
            if (!IsMathOperator(operation)) return value;

            for (int i = 0; i < Mathf.Max(0, legacyKCount); i++)
                value *= 1000f;

            return value;
        }
    }
}
