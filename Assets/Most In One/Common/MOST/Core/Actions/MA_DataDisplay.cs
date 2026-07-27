using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_DisplayData : MOST_ActionCore
    {
        public enum ExtraInfoDataType { None, Int, Float, String }

        public MOSTAction_DisplayData() { ActionName = "Display Data"; }

        [Line]
        [Tooltip("Reference to the MOST_Database that contains the value to display.")]
        public MOST_Database DatabaseHolder;

        [Tooltip("Name of the data entry in the database (supports IntData, FloatData, or StringData).")]
        public string DataName;

        [Tooltip("")]
        public bool ShowHighestValue;

        [Tooltip("For data that only updated per level, loading or for each scene like current level, called on Awake only and can be update using PlayAction()")]
        public bool StaticData;

        [Line]
        [Tooltip("If enabled, writes to a TextMeshPro component; otherwise uses a legacy UI Text component.")]
        public bool UseTextMeshPro;

        [HideIfAny(nameof(UseTextMeshPro), true,true)]
        [Tooltip("Target legacy UI Text component to display the value when UseTextMeshPro is disabled.")]
        public Text text;

        [HideIfAny(nameof(UseTextMeshPro), true, false)]
        [Tooltip("Target TextMeshPro component to display the value when UseTextMeshPro is enabled.")]
        public TMP_Text Text;

        [Line]
        [Tooltip("Optional extra formatting or math to apply: None (raw), Int (add AddedIntValue), Float (add AddedFloatValue), or String (Prefix + value + Postfix).")]
        public ExtraInfoDataType ExtraInfo;

        [HideIfAny(nameof(ExtraInfo), ExtraInfoDataType.String, false)]
        [Tooltip("String to place before the displayed value when ExtraInfo == String.")]
        public string Prefix;

        [HideIfAny(nameof(ExtraInfo), ExtraInfoDataType.String, false)]
        [Tooltip("String to place after the displayed value when ExtraInfo == String.")]
        public string Postfix;

        [HideIfAny(nameof(ExtraInfo), ExtraInfoDataType.Float, false)]
        [Tooltip("Amount to add to the Float value before display when ExtraInfo == Float.")]
        public float AddedFloatValue;

        [HideIfAny(nameof(ExtraInfo), ExtraInfoDataType.Int, false)]
        [Tooltip("Amount to add to the Int value before display when ExtraInfo == Int.")]
        public float AddedIntValue;

        ExtraInfoDataType _type;

        public override void OnAwake() // As Awake function
        {
            base.OnAwake();

            // Infer the underlying type once for mismatch warnings & math
            var obj = DatabaseHolder.GetByName(DataName).ValueAsObject;
            _type =
                obj is string ? ExtraInfoDataType.String :
                obj is int ? ExtraInfoDataType.Int :
                obj is float ? ExtraInfoDataType.Float :
                                ExtraInfoDataType.None;
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
            if (!IsPlaying) return;

            string val;
            if (ExtraInfo == ExtraInfoDataType.None)
            {
                val = ShowHighestValue? DatabaseHolder.GetByName(DataName).HighestValueAsObject.ToString()
                    : DatabaseHolder.GetByName(DataName).ValueAsObject.ToString();
            }
            else if (ExtraInfo == ExtraInfoDataType.Int || ExtraInfo == ExtraInfoDataType.Float)
            {
                if (ExtraInfo == _type)
                {
                    if (_type == ExtraInfoDataType.Int) // Int
                        val = ((ShowHighestValue? DatabaseHolder.Get<IntData>(DataName).HighestValue : DatabaseHolder.Get<IntData>(DataName).Value)
                            + AddedIntValue).ToString();
                    else // Float
                        val = ((ShowHighestValue ? DatabaseHolder.Get<FloatData>(DataName).HighestValue : DatabaseHolder.Get<FloatData>(DataName).Value)
                            + AddedFloatValue).ToString();
                }
                else
                {
                    val = ShowHighestValue ? DatabaseHolder.GetByName(DataName).HighestValueAsObject.ToString()
                        : DatabaseHolder.GetByName(DataName).ValueAsObject.ToString();
                    Debug.LogWarning("Type mismatch on data display; extra math skipped.");
                }
            }
            else // String
            {
                val = Prefix + (ShowHighestValue ? DatabaseHolder.GetByName(DataName).HighestValueAsObject.ToString()
                    : DatabaseHolder.GetByName(DataName).ValueAsObject.ToString()) + Postfix;
            }

            if (UseTextMeshPro) Text.text = val;
            else text.text = val;
            if (StaticData) IsPlaying = false;
        }

        public override void OnUpdate()  // Called each frame as Update function
        {
            base.OnUpdate();
        }

        protected override void Play() { IsPlaying = true; }
        protected override void Stop() { IsPlaying = false; }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
        }
    }
}
