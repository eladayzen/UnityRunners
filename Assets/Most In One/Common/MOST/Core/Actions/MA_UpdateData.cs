using System;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_UpdateData : MOST_ActionCore
    {
        public MOSTAction_UpdateData() { ActionName = "Update Data"; }

        public enum NumOpreation { Set, Add, Subtract, Multiply, divide, Increment, Decrment, ToMax, ToMin, ToHighest }
        public enum LogicOpreation { Set, Toggle } // String: Set only

        [Line]
        [Tooltip("The ScriptableObject database to read/write.")]
        public MOST_Database DataHolder;

        [Tooltip("Which value type you intend to modify.")]
        public MOST_Database.DataTypes DataType;

        [Tooltip("The key (DataName) inside the database.")]
        public string DataName;

        [Line]
        [Tooltip("Numeric operation for Int data.")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.Int, false)]
        public NumOpreation IntOperation = NumOpreation.Set;

        [Tooltip("Operand used by Set/Add/Subtract/Multiply/divide (ignored for Increment/Decrment).")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.Int, false, nameof(IntOperation), NumOpreation.Increment, true
        , nameof(IntOperation), NumOpreation.Decrment, true, nameof(IntOperation), NumOpreation.ToMax, true
        , nameof(IntOperation), NumOpreation.ToMin, true, nameof(IntOperation), NumOpreation.ToHighest, true)]
        public int IntOperand = 0;

        [Tooltip("Numeric operation for Float data.")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.Float, false)]
        public NumOpreation FloatOperation = NumOpreation.Set;

        [Tooltip("Operand used by Set/Add/Subtract/Multiply/divide (ignored for Increment/Decrment).")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.Float, false, nameof(FloatOperation), NumOpreation.Increment, true
        , nameof(FloatOperation), NumOpreation.Decrment, true, nameof(FloatOperation), NumOpreation.ToMax, true
        , nameof(FloatOperation), NumOpreation.ToMin, true, nameof(FloatOperation), NumOpreation.ToHighest, true)]
        public float FloatOperand = 0f;

        [Tooltip("Logical operation for Bool data.")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.Bool, false)]
        public LogicOpreation BoolOperation = LogicOpreation.Set;

        [Tooltip("Value used when BoolOperation = Set.")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.Bool, false)]
        public bool BoolSetValue = true;

        [Tooltip("New string value (String only supports Set).")]
        [HideIfAny(nameof(DataType), MOST_Database.DataTypes.String, false)]
        public string StringValue = "";

        [Line, Tooltip("Call MOST_Database.SaveToJson() after a successful write.")]
        public bool SaveAfterApply = true;

        public override void OnAwake() { base.OnAwake(); }
        public override void OnValidate()
        {
#if UNITY_EDITOR
            base.OnValidate();
#endif
            TrimKey();
        }
        public override void OnLateUpdate() { base.OnLateUpdate(); }

        public override void OnUpdate()
        {
            base.OnUpdate();
        }

        protected override void Play()
        {
            if (!DataHolder)
            {
                Debug.LogWarning("[MOST] UpdateData: DataHolder is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(DataName))
            {
                Debug.LogWarning("[MOST] UpdateData: DataName is empty.");
                return;
            }

            bool success = false;

            switch (DataType)
            {

                case MOST_Database.DataTypes.Int:
                    {
                        var d = DataHolder.Get<IntData>(DataName);
                        if (d == null) { MissingKeyWarn("Int"); break; }
                        success = ApplyInt(d);
                        break;
                    }
                case MOST_Database.DataTypes.Float:
                    {
                        var d = DataHolder.Get<FloatData>(DataName);
                        if (d == null) { MissingKeyWarn("Float"); break; }
                        success = ApplyFloat(d);
                        break;
                    }
                case MOST_Database.DataTypes.Bool:
                    {
                        var d = DataHolder.Get<BoolData>(DataName);
                        if (d == null) { MissingKeyWarn("Bool"); break; }
                        success = ApplyBool(d);
                        break;
                    }
                case MOST_Database.DataTypes.String:
                    {
                        var d = DataHolder.Get<StringData>(DataName);
                        if (d == null) { MissingKeyWarn("String"); break; }
                        d.ValueAsObject = StringValue ?? "";
                        success = true;
                        break;
                    }
            }

            if (success && SaveAfterApply)
                DataHolder.SaveToJson();
        }

        protected override void Stop() { }

        public override void OnDestroy() { base.OnDestroy(); }

        bool ApplyInt(IntData d)
        {
            try
            {
                switch (IntOperation)
                {
                    case NumOpreation.Set: d.Set(IntOperand); break;
                    case NumOpreation.Add: d.Add(IntOperand); break;
                    case NumOpreation.Subtract: d.Subtract(IntOperand); break;
                    case NumOpreation.Multiply: d.Set(checked(d.Get() * IntOperand)); break;
                    case NumOpreation.divide:
                        if (IntOperand == 0) { Debug.LogWarning("[MOST] UpdateData Int: divide by zero."); return false; }
                        d.Set(d.Get() / IntOperand);
                        break;
                    case NumOpreation.Increment: d.Increment(); break;
                    case NumOpreation.Decrment: d.Decrement(); break;
                    case NumOpreation.ToMax: d.Set(d.MaxValue); break;
                    case NumOpreation.ToMin: d.Set(d.MinValue); break;
                    case NumOpreation.ToHighest: d.Set(d.HighestValue); break;
                    default: return false;
                }
                return true;
            }
            catch (OverflowException)
            {
                Debug.LogWarning("[MOST] UpdateData Int: overflow in Multiply.");
                return false;
            }
        }

        bool ApplyFloat(FloatData d)
        {
            switch (FloatOperation)
            {
                case NumOpreation.Set: d.Set(FloatOperand); break;
                case NumOpreation.Add: d.Add(FloatOperand); break;
                case NumOpreation.Subtract: d.Subtract(FloatOperand); break;
                case NumOpreation.Multiply: d.Set(d.Get() * FloatOperand); break;
                case NumOpreation.divide:
                    if (Mathf.Approximately(FloatOperand, 0f)) { Debug.LogWarning("[MOST] UpdateData Float: divide by zero."); return false; }
                    d.Set(d.Get() / FloatOperand);
                    break;
                case NumOpreation.Increment: d.Increment(); break;
                case NumOpreation.ToMax: d.Set(d.MaxValue); break;
                case NumOpreation.ToMin: d.Set(d.MinValue); break;
                case NumOpreation.ToHighest: d.Set(d.HighestValue); break;
                default: return false;
            }
            return true;
        }

        bool ApplyBool(BoolData d)
        {
            switch (BoolOperation)
            {
                case LogicOpreation.Toggle: d.ToggleValue(); break;
                case LogicOpreation.Set: d.SetBool(BoolSetValue); break;
                default: return false;
            }
            return true;
        }

        void MissingKeyWarn(string kind)
        {
            Debug.LogWarning($"[MOST] UpdateData: '{DataName}' not found or wrong type ({kind}).");
        }

        void TrimKey()
        {
            if (!string.IsNullOrEmpty(DataName))
                DataName = DataName.Trim();
        }
    }
}
