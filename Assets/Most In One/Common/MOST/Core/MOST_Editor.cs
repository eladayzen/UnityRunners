using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
#endif

namespace Solo.MOST_IN_ONE
{
    #region Enums
    public enum MOSTEdit { None, RuntimeOnly }
    #endregion

    #region HAS_INPUT_SYSTEM_PACKAGE
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class InputSystemDefineSync
    {
        const string PackageName = "com.unity.inputsystem";
        const string Define = "HAS_INPUT_SYSTEM_PACKAGE";

        static ListRequest _listRequest;
        static bool _isPolling;

        static InputSystemDefineSync()
        {
            EditorApplication.delayCall += Refresh;
            AssemblyReloadEvents.afterAssemblyReload += Refresh;
        }

        [MenuItem("Tools/MOST/Refresh Input System Define")]
        public static void Refresh()
        {
            if (_isPolling) return;

            _isPolling = true;
            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_listRequest == null) { StopPolling(); return; }
            if (!_listRequest.IsCompleted) return;

            try
            {
                bool installed = false;

                if (_listRequest.Status == StatusCode.Success && _listRequest.Result != null)
                {
                    installed = _listRequest.Result.Any(p => string.Equals(p.name, PackageName, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    StopPolling();
                    return;
                }

                ApplyDefineToAllBuildTargetGroups(installed);
            }
            finally
            {
                StopPolling();
            }
        }

        static void StopPolling()
        {
            EditorApplication.update -= Poll;
            _listRequest = null;
            _isPolling = false;
        }

        static void ApplyDefineToAllBuildTargetGroups(bool shouldHaveDefine)
        {
            bool anyChanged = false;

            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown) continue;

                if (!IsValidBuildTargetGroup(group)) continue;

                var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                var set = defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .Where(s => s.Length > 0)
                                 .ToList();

                bool has = set.Contains(Define);

                if (shouldHaveDefine && !has)
                {
                    set.Add(Define);
                    anyChanged = true;
                }
                else if (!shouldHaveDefine && has)
                {
                    set.RemoveAll(s => s == Define);
                    anyChanged = true;
                }

                if (anyChanged)
                {
                    var joined = string.Join(";", set.Distinct());
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
                }
            }
            if (anyChanged) AssetDatabase.Refresh();
        }

        static bool IsValidBuildTargetGroup(BuildTargetGroup group)
        {
            try
            {
                PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
#endif
    #endregion

    #region MOSTRange Property
    [Serializable]
    public struct MOSTRange
    {
        [SerializeField] float min;
        [SerializeField] float max;

        [Tooltip("Minimum range value")]
        public float Min { readonly get { return min > max ? max : min; } set { if (value > max) { min = max; max = value; } else min = value; } }
        [Tooltip("Maximum range value")]
        public float Max { readonly get { return min < max ? max : min; } set { if (value < min) { max = min; min = value; } else max = value; } }

        public MOSTRange(float min, float max) { this.min = min > max ? max : min; this.max = min < max ? max : min; }

        public readonly float GetRandomValue() { return UnityEngine.Random.Range(min, max); }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MOSTRange))]
    public class MOSTRangeDrawer : PropertyDrawer
    {
        const float k_SubLabelWidth = 28f;
        const float k_FieldSpacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with { float Min, float Max }");
                EditorGUI.EndProperty();
                return;
            }

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float half = (position.width - k_FieldSpacing) * 0.5f;
            Rect minFieldRect = new(position.x, position.y, half, position.height);
            Rect maxFieldRect = new(position.x + half + k_FieldSpacing, position.y, half, position.height);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            bool prevWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUIUtility.labelWidth = k_SubLabelWidth;

            float minVal = minProp.floatValue;
            float maxVal = maxProp.floatValue;

            EditorGUI.BeginChangeCheck();

            minVal = EditorGUI.FloatField(minFieldRect, new GUIContent("Min"), minVal);
            maxVal = EditorGUI.FloatField(maxFieldRect, new GUIContent("Max"), maxVal);

            if (EditorGUI.EndChangeCheck())
            {
                if (minVal > maxVal)
                {
                    if (minProp.floatValue != minVal) maxVal = minVal;
                    else minVal = maxVal;
                }
                minProp.floatValue = minVal;
                maxProp.floatValue = maxVal;
                property.serializedObject.ApplyModifiedProperties();
            }
            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUIUtility.wideMode = prevWide;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
#endif
    #endregion

    #region MinMaxSlider Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MinMaxSliderAttribute : PropertyAttribute
    {
        public enum Source { Const, Ref }

        public readonly Source MinSource;
        public readonly Source MaxSource;

        public readonly float MinConst;
        public readonly float MaxConst;

        public readonly string MinRef;
        public readonly string MaxRef;

        public MinMaxSliderAttribute(float minConst, float maxConst)
        { MinSource = Source.Const; MinConst = minConst; MaxSource = Source.Const; MaxConst = maxConst; }

        public MinMaxSliderAttribute(string minRef, string maxRef)
        { MinSource = Source.Ref; MinRef = minRef; MaxSource = Source.Ref; MaxRef = maxRef; }

        public MinMaxSliderAttribute(string minRef, float maxConst)
        { MinSource = Source.Ref; MinRef = minRef; MaxSource = Source.Const; MaxConst = maxConst; }

        public MinMaxSliderAttribute(float minConst, string maxRef)
        { MinSource = Source.Const; MinConst = minConst; MaxSource = Source.Ref; MaxRef = maxRef; }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public class MinMaxSliderDrawer : PropertyDrawer
    {
        const float FieldWidth = 50f;
        const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with MostRange { float Min, float Max }");
                return;
            }

            var attr = (MinMaxSliderAttribute)attribute;
            if (!TryResolveLimit(property, attr.MinSource, attr.MinConst, attr.MinRef, out float hardMin) ||
                !TryResolveLimit(property, attr.MaxSource, attr.MaxConst, attr.MaxRef, out float hardMax))
            {
                EditorGUI.HelpBox(position, "MinMaxSlider: couldn't resolve one/both limits.", MessageType.Error);
                return;
            }
            if (hardMin > hardMax) (hardMin, hardMax) = (hardMax, hardMin);

            float curMin = minProp.floatValue;
            float curMax = maxProp.floatValue;

            float clampedMin = Mathf.Clamp(curMin, hardMin, hardMax);
            float clampedMax = Mathf.Clamp(curMax, hardMin, hardMax);
            if (clampedMin > clampedMax) clampedMin = clampedMax;

            if (!Mathf.Approximately(curMin, clampedMin) || !Mathf.Approximately(curMax, clampedMax))
            {
                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Clamp {label.text}");
                minProp.floatValue = clampedMin;
                maxProp.floatValue = clampedMax;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
                curMin = clampedMin;
                curMax = clampedMax;
            }

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            Rect minRect = new(position.x, position.y, FieldWidth, position.height);
            Rect maxRect = new(position.xMax - FieldWidth, position.y, FieldWidth, position.height);
            Rect sliderRect = new(minRect.xMax + Spacing, position.y,
                                  Mathf.Max(0, maxRect.xMin - (minRect.xMax + Spacing)), position.height);

            float minValue = curMin;
            float maxValue = curMax;

            EditorGUI.BeginChangeCheck();

            minValue = EditorGUI.FloatField(minRect, minValue);
            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, hardMin, hardMax);
            maxValue = EditorGUI.FloatField(maxRect, maxValue);

            if (EditorGUI.EndChangeCheck())
            {
                minValue = Mathf.Clamp(minValue, hardMin, hardMax);
                maxValue = Mathf.Clamp(maxValue, hardMin, hardMax);
                if (minValue > maxValue) minValue = maxValue;

                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Change {label.text}");
                minProp.floatValue = minValue;
                maxProp.floatValue = maxValue;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        static bool TryResolveLimit(SerializedProperty owner, MinMaxSliderAttribute.Source kind,
                                    float constVal, string refPath, out float value)
        {
            if (kind == MinMaxSliderAttribute.Source.Const)
            { value = constVal; return true; }

            var p = owner.serializedObject.FindProperty(BuildSiblingPath(owner, refPath));
            if (p == null) { value = 0f; return false; }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: value = p.floatValue; return true;
                case SerializedPropertyType.Integer: value = p.intValue; return true;
                default: value = 0f; return false;
            }
        }

        static string BuildSiblingPath(SerializedProperty property, string rel)
        {
            int i = property.propertyPath.LastIndexOf('.');
            var parent = i >= 0 ? property.propertyPath[..i] : string.Empty;
            return string.IsNullOrEmpty(parent) ? rel : $"{parent}.{rel}";
        }
    }
#endif
    #endregion

    #region MOSTRangeInt Property
    [Serializable]
    public struct MOSTRangeInt
    {
        [SerializeField] int min;
        [SerializeField] int max;

        public int Min { readonly get { return min > max ? max : min; } set { if (value > max) { min = max; max = value; } else min = value; } }
        public int Max { readonly get { return min < max ? max : min; } set { if (value < min) { max = min; min = value; } else max = value; } }

        public MOSTRangeInt(int min, int max) { this.min = min > max ? max : min; this.max = min < max ? max : min; }

        public readonly int GetRandomValue() { return UnityEngine.Random.Range(min, max); }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MOSTRangeInt))]
    public class MOSTRangeIntDrawer : PropertyDrawer
    {
        const float k_SubLabelWidth = 28f;
        const float k_FieldSpacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with { int Min, int Max }");
                EditorGUI.EndProperty();
                return;
            }

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float half = (position.width - k_FieldSpacing) * 0.5f;
            Rect minFieldRect = new(position.x, position.y, half, position.height);
            Rect maxFieldRect = new(position.x + half + k_FieldSpacing, position.y, half, position.height);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            bool prevWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUIUtility.labelWidth = k_SubLabelWidth;

            int minVal = minProp.intValue;
            int maxVal = maxProp.intValue;

            EditorGUI.BeginChangeCheck();

            minVal = EditorGUI.IntField(minFieldRect, new GUIContent("Min"), minVal);
            maxVal = EditorGUI.IntField(maxFieldRect, new GUIContent("Max"), maxVal);

            if (EditorGUI.EndChangeCheck())
            {
                if (minVal > maxVal)
                {
                    if (minProp.intValue != minVal) maxVal = minVal;
                    else minVal = maxVal;
                }
                minProp.intValue = minVal;
                maxProp.intValue = maxVal;
                property.serializedObject.ApplyModifiedProperties();
            }
            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUIUtility.wideMode = prevWide;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
#endif
    #endregion

    #region MinMaxSliderInt Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MinMaxSliderIntAttribute : PropertyAttribute
    {
        public enum SourceInt { Const, Ref }

        public readonly SourceInt MinSource;
        public readonly SourceInt MaxSource;

        public readonly int MinConst;
        public readonly int MaxConst;

        public readonly string MinRef;
        public readonly string MaxRef;

        public MinMaxSliderIntAttribute(int minConst, int maxConst)
        { MinSource = SourceInt.Const; MinConst = minConst; MaxSource = SourceInt.Const; MaxConst = maxConst; }

        public MinMaxSliderIntAttribute(string minRef, string maxRef)
        { MinSource = SourceInt.Ref; MinRef = minRef; MaxSource = SourceInt.Ref; MaxRef = maxRef; }

        public MinMaxSliderIntAttribute(string minRef, int maxConst)
        { MinSource = SourceInt.Ref; MinRef = minRef; MaxSource = SourceInt.Const; MaxConst = maxConst; }

        public MinMaxSliderIntAttribute(int minConst, string maxRef)
        { MinSource = SourceInt.Const; MinConst = minConst; MaxSource = SourceInt.Ref; MaxRef = maxRef; }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinMaxSliderIntAttribute))]
    public class MinMaxSliderIntDrawer : PropertyDrawer
    {
        const float FieldWidth = 50f;
        const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with MostRangeInt { int Min, int Max }");
                return;
            }

            var attr = (MinMaxSliderIntAttribute)attribute;
            if (!TryResolveLimit(property, attr.MinSource, attr.MinConst, attr.MinRef, out float hardMin) ||
                !TryResolveLimit(property, attr.MaxSource, attr.MaxConst, attr.MaxRef, out float hardMax))
            {
                EditorGUI.HelpBox(position, "MinMaxSlider: couldn't resolve one/both limits.", MessageType.Error);
                return;
            }
            if (hardMin > hardMax) (hardMin, hardMax) = (hardMax, hardMin);

            int curMin = minProp.intValue;
            int curMax = maxProp.intValue;

            int clampedMin = (int)Mathf.Clamp(curMin, hardMin, hardMax);
            int clampedMax = (int)Mathf.Clamp(curMax, hardMin, hardMax);
            if (clampedMin > clampedMax) clampedMin = clampedMax;

            if (!Mathf.Approximately(curMin, clampedMin) || !Mathf.Approximately(curMax, clampedMax))
            {
                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Clamp {label.text}");
                minProp.intValue = clampedMin;
                maxProp.intValue = clampedMax;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
                curMin = clampedMin;
                curMax = clampedMax;
            }

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            Rect minRect = new(position.x, position.y, FieldWidth, position.height);
            Rect maxRect = new(position.xMax - FieldWidth, position.y, FieldWidth, position.height);
            Rect sliderRect = new(minRect.xMax + Spacing, position.y,
                                  Mathf.Max(0, maxRect.xMin - (minRect.xMax + Spacing)), position.height);

            float minValue = curMin;
            float maxValue = curMax;

            EditorGUI.BeginChangeCheck();

            minValue = EditorGUI.IntField(minRect, (int)minValue);
            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, hardMin, hardMax);
            maxValue = EditorGUI.IntField(maxRect, (int)maxValue);

            if (EditorGUI.EndChangeCheck())
            {
                minValue = Mathf.Clamp(minValue, hardMin, hardMax);
                maxValue = Mathf.Clamp(maxValue, hardMin, hardMax);
                if (minValue > maxValue) minValue = maxValue;

                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Change {label.text}");
                minProp.intValue = (int)minValue;
                maxProp.intValue = (int)maxValue;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        static bool TryResolveLimit(SerializedProperty owner, MinMaxSliderIntAttribute.SourceInt kind,
                                    float constVal, string refPath, out float value)
        {
            if (kind == MinMaxSliderIntAttribute.SourceInt.Const)
            { value = constVal; return true; }

            var p = owner.serializedObject.FindProperty(BuildSiblingPath(owner, refPath));
            if (p == null) { value = 0f; return false; }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: value = p.intValue; return true;
                case SerializedPropertyType.Integer: value = p.intValue; return true;
                default: value = 0f; return false;
            }
        }

        static string BuildSiblingPath(SerializedProperty property, string rel)
        {
            int i = property.propertyPath.LastIndexOf('.');
            var parent = i >= 0 ? property.propertyPath[..i] : string.Empty;
            return string.IsNullOrEmpty(parent) ? rel : $"{parent}.{rel}";
        }
    }
#endif
    #endregion

    #region BigHeader Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class BigHeaderAttribute : PropertyAttribute
    {
        public string Text { get; }
        public Color Color { get; }
        [Min(1f)] public int FontSize;

        public BigHeaderAttribute(string text)
        {
            Text = text; Color = new Color32(252, 191, 1, 255); FontSize = 15;
        }

        public BigHeaderAttribute(string text, float r, float g, float b)
        {
            Text = text; Color = new Color(r, g, b, 1f); FontSize = 15;
        }

        public BigHeaderAttribute(string text, byte r, byte g, byte b)
        {
            Text = text; Color = new Color32(r, g, b, 255); FontSize = 15;
        }

        public BigHeaderAttribute(string text, int fontSize)
        {
            Text = text; Color = new Color32(252, 191, 1, 255); FontSize = Mathf.Max(1, fontSize);
        }

        public BigHeaderAttribute(string text, float r, float g, float b, int fontSize)
        {
            Text = text; Color = new Color(r, g, b, 1f); FontSize = Mathf.Max(1, fontSize);
        }

        public BigHeaderAttribute(string text, byte r, byte g, byte b, int fontSize)
        {
            Text = text; Color = new Color32(r, g, b, 255); FontSize = Mathf.Max(1, fontSize);
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(BigHeaderAttribute))]
    public class BigHeaderPropertyDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            BigHeaderAttribute attributeHandle = (BigHeaderAttribute)attribute;

            position.yMin += EditorGUIUtility.singleLineHeight * 0.5f;
            position = EditorGUI.IndentedRect(position);

            GUIStyle headerTextStyle = new()
            {
                fontSize = attributeHandle.FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = attributeHandle.Color }
            };

            GUI.Label(position, attributeHandle.Text, headerTextStyle);
            EditorGUI.DrawRect(new Rect(position.xMin, position.yMin, position.width, 1), attributeHandle.Color);
        }

        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight * 2f;
        }
    }
#endif
    #endregion

    #region Line Attribute
    public class LineAttribute : PropertyAttribute
    {
        public readonly float thickness;
        public readonly float width;
        public readonly Color color;
        public readonly float spacing;

        public LineAttribute(float thickness = 1f, float width = 1f, float r = 0.34f, float g = 0.34f, float b = 0.34f, float a = 1f, float spacing = 7f)
        {
            this.thickness = thickness;
            this.width = Mathf.Clamp01(width);
            this.color = new Color(r, g, b, a);
            this.spacing = spacing;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(LineAttribute))]
    public class LineDrawer : DecoratorDrawer
    {
        public override float GetHeight()
        {
            LineAttribute lineAttribute = (LineAttribute)attribute;
            return lineAttribute.spacing * 2 + lineAttribute.thickness;
        }

        public override void OnGUI(Rect position)
        {
            LineAttribute lineAttribute = (LineAttribute)attribute;

            // Calculate centered position
            float width = position.width * lineAttribute.width;
            float xOffset = (position.width - width) * 0.5f;

            // Spacing above the line
            position.y += lineAttribute.spacing;
            position.height = lineAttribute.thickness;

            // Draw the line
            Rect lineRect = new Rect(
                position.x + xOffset,
                position.y,
                width,
                lineAttribute.thickness
            );

            EditorGUI.DrawRect(lineRect, lineAttribute.color);
        }
    }
#endif
    #endregion

    #region ReadOnly Attribute
    public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
    #endregion

    #region ReadOnlyIf Attribute
    public class ReadOnlyIfAttribute : PropertyAttribute
    {
        public string ConditionField { get; }
        public bool ReadOnlyWhen { get; }
        public object CompareValue { get; }

        public string ConditionFieldSec { get; }
        public bool ReadOnlyWhenSec { get; }
        public object CompareValueSec { get; }

        // Boolean condition
        public ReadOnlyIfAttribute(string boolFieldName, bool readOnlyWhen = true)
        {
            ConditionField = boolFieldName;
            ReadOnlyWhen = readOnlyWhen;
            CompareValue = null;
        }

        // Enum condition
        public ReadOnlyIfAttribute(string enumFieldName, object enumValue, bool readOnlyWhen = true)
        {
            ConditionField = enumFieldName;
            ReadOnlyWhen = readOnlyWhen;
            CompareValue = enumValue;
        }

        public ReadOnlyIfAttribute(string enumFieldName, object enumValue1, object enumValue2)
        {
            ConditionField = enumFieldName;
            ReadOnlyWhen = true;
            CompareValue = enumValue1;
            CompareValueSec = enumValue2;
        }

        public ReadOnlyIfAttribute(string enumFieldName1, object enumValue1, bool ReadOnlyWhen1, string enumFieldName2, object enumValue2, bool ReadOnlyWhen2)
        {
            ConditionField = enumFieldName1;
            ReadOnlyWhen = ReadOnlyWhen1;
            CompareValue = enumValue1;

            ConditionFieldSec = enumFieldName2;
            ReadOnlyWhenSec = ReadOnlyWhen2;
            CompareValueSec = enumValue2;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyIfAttribute))]
    public class ReadOnlyIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (attribute is not ReadOnlyIfAttribute readOnlyIf) return;

            bool shouldBeReadOnly = EvaluateCondition(property, readOnlyIf);
            bool wasEnabled = GUI.enabled;

            GUI.enabled = !shouldBeReadOnly;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = wasEnabled;
        }

        bool EvaluateCondition(SerializedProperty property, ReadOnlyIfAttribute readOnlyIf)
        {
            // For non-MonoBehaviour classes, we need to handle nested property paths
            SerializedProperty conditionProperty = FindConditionProperty(property, readOnlyIf.ConditionField);
            if (conditionProperty == null)
            {
                Debug.LogWarning($"ReadOnlyIf: Condition field '{readOnlyIf.ConditionField}' not found in property path: {property.propertyPath}");
                return false;
            }

            if (readOnlyIf.CompareValue == null) // Boolean condition
            {
                return conditionProperty.boolValue == readOnlyIf.ReadOnlyWhen;
            }
            else if (readOnlyIf.CompareValueSec == null) // Enum condition
            {
                try
                {
                    return conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValue == readOnlyIf.ReadOnlyWhen;
                }
                catch (Exception e)
                {
                    Debug.LogError($"ReadOnlyIf: Invalid enum comparison for field {readOnlyIf.ConditionField}: {e.Message}");
                    return false;
                }
            }
            else
            {
                try
                {
                    if (readOnlyIf.ConditionFieldSec == null || readOnlyIf.ConditionFieldSec == string.Empty)
                        return (conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValue ||
                            conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValueSec) == readOnlyIf.ReadOnlyWhen;
                    else
                    {
                        SerializedProperty conditionPropertySec = FindConditionProperty(property, readOnlyIf.ConditionFieldSec);
                        return (conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValue == readOnlyIf.ReadOnlyWhen) &&
                        (conditionPropertySec.enumValueIndex == (int)readOnlyIf.CompareValueSec == readOnlyIf.ReadOnlyWhenSec);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"ReadOnlyIf: Invalid enum comparison for field {readOnlyIf.ConditionField}: {e.Message}");
                    return false;
                }
            }
        }

        SerializedProperty FindConditionProperty(SerializedProperty property, string conditionFieldName)
        {
            // First try direct finding
            SerializedProperty conditionProperty = property.serializedObject.FindProperty(conditionFieldName);
            if (conditionProperty != null) return conditionProperty;

            // For nested properties (like in arrays), try to find relative to current property
            string basePath = GetBasePropertyPath(property.propertyPath);
            if (!string.IsNullOrEmpty(basePath))
            {
                string fullConditionPath = $"{basePath}.{conditionFieldName}";
                conditionProperty = property.serializedObject.FindProperty(fullConditionPath);
            }

            return conditionProperty;
        }

        string GetBasePropertyPath(string propertyPath)
        {
            // Extract base path from nested property paths
            // Example: "effects.Array.data[0].someField" -> "effects.Array.data[0]"
            int lastDotIndex = propertyPath.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                return propertyPath.Substring(0, lastDotIndex);
            }

            return string.Empty;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
#endif
    #endregion

    #region HelpBox Attribute
    public enum HelpBoxKind { Info, Warning, Error, None }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
    public class HelpBoxAttribute : PropertyAttribute
    {
        public readonly string Message;
        public readonly HelpBoxKind Kind;
        public readonly float Top;
        public readonly float Bottom;
        public readonly bool RichText;

        public HelpBoxAttribute(string message, HelpBoxKind kind = HelpBoxKind.Info, float top = 4f, float bottom = 4f, bool richText = false)
        {
            Message = message ?? string.Empty;
            Kind = kind;
            Top = Mathf.Max(0f, top);
            Bottom = Mathf.Max(0f, bottom);
            RichText = richText;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(HelpBoxAttribute))]
    public class HelpBoxDecoratorDrawer : DecoratorDrawer
    {
        HelpBoxAttribute A => (HelpBoxAttribute)attribute;

        static GUIStyle _rtHelpBoxStyle;
        static GUIStyle RtHelpBoxStyle
        {
            get
            {
                _rtHelpBoxStyle ??= new GUIStyle(EditorStyles.helpBox)
                {
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft,
                };
                return _rtHelpBoxStyle;
            }
        }

        public override float GetHeight()
        {
            if (string.IsNullOrEmpty(A.Message))
                return 0f;
            float viewWidth = EditorGUIUtility.currentViewWidth;
            float indentPx = EditorGUI.indentLevel * 15f; // Unity's indent size approximation
            float contentWidth = Mathf.Max(0f, viewWidth - indentPx - 32f); // 32 ~ padding + scrollbar buffer

            var style = A.RichText ? RtHelpBoxStyle : EditorStyles.helpBox;
            float textHeight = style.CalcHeight(new GUIContent(A.Message), contentWidth);
            return A.Top + textHeight + A.Bottom;
        }

        public override void OnGUI(Rect position)
        {
            if (string.IsNullOrEmpty(A.Message))
                return;

            position = EditorGUI.IndentedRect(position);
            var drawRect = new Rect(position.x, position.y + A.Top, position.width, position.height - (A.Top + A.Bottom));
            MessageType mt = MessageType.Info;
            switch (A.Kind)
            {
                case HelpBoxKind.Warning: mt = MessageType.Warning; break;
                case HelpBoxKind.Error: mt = MessageType.Error; break;
                case HelpBoxKind.None: mt = MessageType.None; break;
            }

            if (A.RichText) GUI.Label(drawRect, new GUIContent(A.Message), RtHelpBoxStyle);
            else EditorGUI.HelpBox(drawRect, A.Message, mt);
        }
    }
#endif
    #endregion

    #region GUIColor Attribute
    public class GUIColorAttribute : PropertyAttribute
    {
        public Color Color { get; }
        public bool ApplyToChildren { get; }

        // Constructor using Color
        public GUIColorAttribute(float r, float g, float b, float a = 1f, bool applyToChildren = false)
        {
            Color = new Color(r, g, b, a);
            ApplyToChildren = applyToChildren;
        }

        // Constructor using predefined color names
        public GUIColorAttribute(string colorName, bool applyToChildren = false)
        {
            ApplyToChildren = applyToChildren;
            Color = GetColorByName(colorName);
        }

        static Color GetColorByName(string name)
        {
            return name.ToLower() switch
            {
                "red" => Color.red,
                "green" => Color.green,
                "blue" => Color.blue,
                "yellow" => Color.yellow,
                "cyan" => Color.cyan,
                "magenta" => Color.magenta,
                "white" => Color.white,
                "black" => Color.black,
                "gray" or "grey" => Color.grey,
                "clear" => Color.clear,
                _ => Color.white
            };
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(GUIColorAttribute))]
    public class GUIColorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var colorAttribute = (GUIColorAttribute)attribute;

            // Save original colors
            Color originalColor = GUI.color;
            Color originalContentColor = GUI.contentColor;
            Color originalBackgroundColor = GUI.backgroundColor;

            // Apply the color
            GUI.color = colorAttribute.Color;
            GUI.contentColor = colorAttribute.Color;
            GUI.backgroundColor = colorAttribute.Color;

            // Draw the property
            if (colorAttribute.ApplyToChildren)
            {
                // For complex properties, we need to handle children differently
                EditorGUI.PropertyField(position, property, label, true);
            }
            else
            {
                // For simple properties, draw just this field
                EditorGUI.PropertyField(position, property, label);
            }

            // Restore original colors
            GUI.color = originalColor;
            GUI.contentColor = originalContentColor;
            GUI.backgroundColor = originalBackgroundColor;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (((GUIColorAttribute)attribute).ApplyToChildren) return EditorGUI.GetPropertyHeight(property, label, true);
            else return EditorGUI.GetPropertyHeight(property, label);
        }
    }
#endif
    #endregion

    #region Required Attribute
    public class RequiredAttribute : PropertyAttribute
    {
        public string errorMessage = "(Require)";

        public RequiredAttribute() { }

        public RequiredAttribute(string errorMessage)
        {
            this.errorMessage = errorMessage;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(RequiredAttribute))]
    public class RequiredDrawer : PropertyDrawer
    {
        static Texture2D _errorIcon;
        static Texture2D ErrorIcon => _errorIcon = _errorIcon != null ? _errorIcon : EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;

        static GUIStyle _errorStyle;
        static GUIStyle ErrorStyle => _errorStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 0.3f, 0.3f, 0.8f) },
            fontStyle = FontStyle.Bold
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // First, draw the property exactly as Unity would without any modifications
            EditorGUI.PropertyField(position, property, label);

            // Then, if it's an object reference and empty, overlay the error indicators
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null)
            {
                RequiredAttribute requiredAttr = (RequiredAttribute)attribute;

                // Calculate the field area (without affecting layout)
                Rect fieldArea = new(
                    position.x + EditorGUIUtility.labelWidth,
                    position.y,
                    position.width - EditorGUIUtility.labelWidth,
                    position.height
                );

                // Draw error icon (to the left of the field)
                Rect iconRect = new(
                    fieldArea.x - 18f,
                    fieldArea.y + (fieldArea.height - 16f) * 0.5f,
                    16f, 16f
                );

                if (Event.current.type == EventType.Repaint)
                {
                    GUI.DrawTexture(iconRect, ErrorIcon, ScaleMode.ScaleToFit);
                }

                string defaultText = GetDefaultNoneText(property);
                float defaultTextWidth = EditorStyles.label.CalcSize(new GUIContent(defaultText)).x;

                // Draw required text (inside the field, after default text)
                Rect textRect = new(
                    fieldArea.x + defaultTextWidth + 6f,
                    fieldArea.y, 80f,
                    fieldArea.height
                );

                GUI.Label(textRect, requiredAttr.errorMessage, ErrorStyle);
            }
        }

        string GetDefaultNoneText(SerializedProperty property)
        {
            string typeName = "Object";

            if (property.type.Contains("GameObject")) typeName = "Game Object";
            else if (property.type.Contains("Transform")) typeName = "Transform";
            else if (property.type.Contains("MonoBehaviour")) typeName = "Mono Behaviour";
            else if (property.type.Contains("Component")) typeName = "Component";
            else if (property.type.Contains("AudioClip")) typeName = "Audio Clip";
            else if (property.type.Contains("Material")) typeName = "Material";
            else if (property.type.Contains("Texture")) typeName = "Texture";
            else
            {
                // Extract type from PPtr<$Type> format
                int start = property.type.IndexOf("PPtr<$") + 6;
                int end = property.type.IndexOf(">");
                if (start > 5 && end > start)
                {
                    typeName = property.type[start..end];
                    // Clean up common Unity type names
                    typeName = typeName.Replace("UnityEngine.", "");
                }
            }

            return "None (" + typeName + ")";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the exact same height as the default property field
            return EditorGUI.GetPropertyHeight(property, label);
        }
    }
#endif
    #endregion

    #region Inner Hint
    public class InnerHintAttribute : PropertyAttribute
    {
        public string customHint;

        public InnerHintAttribute(string customHint)
        {
            this.customHint = customHint;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(InnerHintAttribute))]
    public class InnerHintDrawer : PropertyDrawer
    {
        static GUIStyle _hintStyle;
        static GUIStyle HintStyle
        {
            get
            {
                _hintStyle ??= new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 0.9f) },
                    padding = new RectOffset(2, 2, 0, 0)
                };
                return _hintStyle;
            }
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // First, draw the property exactly as Unity would without any modifications
            EditorGUI.PropertyField(position, property, label);

            // Then, if it's an object reference and empty, overlay the error indicators
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null)
            {
                InnerHintAttribute innerHint = (InnerHintAttribute)attribute;

                // Calculate the field area (without affecting layout)
                Rect fieldArea = new(
                    position.x + EditorGUIUtility.labelWidth,
                    position.y,
                    position.width - EditorGUIUtility.labelWidth,
                    position.height
                );

                string defaultText = GetDefaultNoneText(property);
                float defaultTextWidth = EditorStyles.label.CalcSize(new GUIContent(defaultText)).x;

                // Draw required text (inside the field, after default text)
                Rect textRect = new(
                    fieldArea.x + defaultTextWidth + 6f,
                    fieldArea.y, 80f,
                    fieldArea.height
                );

                GUI.Label(textRect, innerHint.customHint, HintStyle);
            }
        }

        string GetDefaultNoneText(SerializedProperty property)
        {
            string typeName = "Object";

            if (property.type.Contains("GameObject")) typeName = "Game Object";
            else if (property.type.Contains("Transform")) typeName = "Transform";
            else if (property.type.Contains("MonoBehaviour")) typeName = "Mono Behaviour";
            else if (property.type.Contains("Component")) typeName = "Component";
            else if (property.type.Contains("AudioClip")) typeName = "Audio Clip";
            else if (property.type.Contains("Material")) typeName = "Material";
            else if (property.type.Contains("Texture")) typeName = "Texture";
            else
            {
                // Extract type from PPtr<$Type> format
                int start = property.type.IndexOf("PPtr<$") + 6;
                int end = property.type.IndexOf(">");
                if (start > 5 && end > start)
                {
                    typeName = property.type[start..end];
                    // Clean up common Unity type names
                    typeName = typeName.Replace("UnityEngine.", "");
                }
            }

            return "None (" + typeName + ")";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the exact same height as the default property field
            return EditorGUI.GetPropertyHeight(property, label);
        }
    }
#endif
    #endregion

    #region Group Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class GroupAttribute : PropertyAttribute
    {
        public readonly string Name;
        public GroupAttribute(string name) { Name = name; }
    }

    /// Groups any fields marked with [Group("Name")] under a single foldout header per scope.
    /// Scope = the parent path of the field (so the same group name inside different structs/arrays is isolated).
    /// </summary>
#if UNITY_EDITOR
    /// <summary>
    /// Groups any fields marked with [Group("Name")] under a single foldout header per scope.
    /// Scope = the parent path of the field (so the same group name inside different structs/arrays is isolated).
    /// </summary>
    [CustomPropertyDrawer(typeof(GroupAttribute))]
    public class GroupDrawer : PropertyDrawer
    {
        // Foldout state per (object/scope/group)
        static readonly Dictionary<string, bool> Foldout = new();

        // Cache of "first field path" per (object/scope/group) to know who draws the header
        static readonly Dictionary<string, string> FirstPath = new();

        // Reflection flags
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        const float VSP = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var groupName = ((GroupAttribute)attribute).Name;
            var key = GetGroupKey(property, groupName);
            var isHeader = IsHeader(property, groupName, key);

            if (!Foldout.ContainsKey(key)) Foldout[key] = true;

            // Draw header once per group/scope
            if (isHeader)
            {
                var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.18f, 0.18f, 0.85f));
                Foldout[key] = EditorGUI.Foldout(headerRect, Foldout[key], groupName, true);
                position.y += headerRect.height + EditorGUIUtility.standardVerticalSpacing;
            }

            if (!Foldout[key]) return;

            var fieldLabel = new GUIContent(property.displayName, label?.tooltip);

            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUI.BeginProperty(position, fieldLabel, property);
                EditorGUI.PropertyField(position, property, fieldLabel, true);
                EditorGUI.EndProperty();
            }
        }


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var groupName = ((GroupAttribute)attribute).Name;
            var key = GetGroupKey(property, groupName);
            var isHeader = IsHeader(property, groupName, key);

            // Header height (only once per group)
            float h = 0f;
            if (isHeader)
                h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // If folded: only header shows, members collapse to 0
            if (Foldout.ContainsKey(key) && !Foldout[key])
                return isHeader ? h : 0f;

            // Expanded: header (if first) + property height
            h += EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
            return h + VSP;
        }

        // ---------- helpers ----------

        static string GetGroupKey(SerializedProperty p, string groupName)
        {
            // Key combines: target instance id(s) + scope(base path) + group name.
            // For multi-object editing, aggregate IDs to keep states separate per selection set.
            int[] ids = p.serializedObject.targetObjects.Select(o => o.GetInstanceID()).OrderBy(x => x).ToArray();
            string idPart = string.Join(",", ids);
            string scope = GetScopePath(p);
            return $"{idPart}|{scope}|{groupName}";
        }

        static string GetScopePath(SerializedProperty p)
        {
            // Scope = parent path of this field (without the last segment)
            var path = p.propertyPath;
            int lastDot = path.LastIndexOf('.');
            return lastDot > 0 ? path[..lastDot] : string.Empty;
        }

        bool IsHeader(SerializedProperty property, string groupName, string key)
        {
            // Cache and compare the first path for this group in this scope
            if (!FirstPath.TryGetValue(key, out string firstPath) || string.IsNullOrEmpty(firstPath))
            {
                firstPath = FindFirstPathInGroup(property, groupName);
                FirstPath[key] = firstPath ?? property.propertyPath; // fallback to self to remain stable
            }
            return property.propertyPath == FirstPath[key];
        }

        string FindFirstPathInGroup(SerializedProperty property, string groupName)
        {
            // Iterate all visible properties and return the first in the same scope that also has [Group(groupName)]
            string scope = GetScopePath(property);
            var it = property.serializedObject.GetIterator();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Same scope?
                if (GetScopePath(it) != scope) continue;

                // Has GroupAttribute with same name?
                if (HasGroupAttribute(it, groupName))
                    return it.propertyPath;

                // Stop if we left scope block (cheap heuristic: scope prefix no longer matches)
                // Not strictly necessary, but can cut iterations in large inspectors.
            }
            return null;
        }

        bool HasGroupAttribute(SerializedProperty p, string groupName)
        {
            // Reflect the field to read attributes
            var fi = GetFieldInfo(p, out _);
            if (fi == null) return false;
            var attrs = fi.GetCustomAttributes(typeof(GroupAttribute), false);
            return attrs.Length > 0 && ((GroupAttribute)attrs[0]).Name == groupName;
        }

        static FieldInfo GetFieldInfo(SerializedProperty prop, out Type fieldType)
        {
            fieldType = null;
            var obj = prop.serializedObject.targetObject;
            if (!obj) return null;

            var t = obj.GetType();
            string path = prop.propertyPath.Replace(".Array.data[", "[");
            var segments = path.Split('.');
            FieldInfo fi = null;
            Type parent = t;

            foreach (var seg in segments)
            {
                if (seg.Contains("["))
                {
                    string name = seg[..seg.IndexOf('[')];
                    fi = parent.GetField(name, BF);
                    if (fi == null) return null;
                    parent = GetElementType(fi.FieldType) ?? fi.FieldType;
                }
                else
                {
                    fi = parent.GetField(seg, BF);
                    if (fi == null) return null;
                    parent = fi.FieldType;
                }
            }
            fieldType = parent;
            return fi;
        }

        static Type GetElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return t.GetGenericArguments()[0];
            return null;
        }
    }
#endif
    #endregion

    #region HideScriptField
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class HideScriptFieldAttribute : Attribute { }
#if UNITY_EDITOR
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class HideScriptFieldEditor_MB : HideScriptFieldEditorBase { }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public class HideScriptFieldEditor_SO : HideScriptFieldEditorBase { }

    public abstract class HideScriptFieldEditorBase : Editor
    {
        public override void OnInspectorGUI()
        {
            // Hide only if ALL selected targets have the attribute
            bool hideScript = targets.All(t => t.GetType()
                                                .GetCustomAttributes(typeof(HideScriptFieldAttribute), true)
                                                .Any());

            // If nothing to hide, just use default inspector
            if (!hideScript)
            {
                base.OnInspectorGUI();
                return;
            }

            serializedObject.Update();
            var prop = serializedObject.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Skip the script reference on both MonoBehaviours and ScriptableObjects
                if (prop.propertyPath == "m_Script") continue;

                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
    #endregion

    #region Vec3AnimationCurve
    [Serializable]
    public struct Vector3AnimationCurves
    {
        public bool enableX;
        public bool enableY;
        public bool enableZ;

        public AnimationCurve x;
        public AnimationCurve y;
        public AnimationCurve z;

        public Vector3AnimationCurves(AnimationCurve xCurve, AnimationCurve yCurve, AnimationCurve zCurve)
        {
            x = xCurve;
            y = yCurve;
            z = zCurve;
            enableX = enableY = enableZ = true;
        }

        public readonly Vector3 Evaluate(float time)
        {
            return new Vector3(
                enableX && x != null ? x.Evaluate(time) : 0,
                enableY && y != null ? y.Evaluate(time) : 0,
                enableZ && z != null ? z.Evaluate(time) : 0
            );
        }

        public readonly float GetDuration()
        {
            float duration = 0f;

            if (enableX && x != null && x.length > 0)
                duration = Mathf.Max(duration, x.keys[x.length - 1].time);

            if (enableY && y != null && y.length > 0)
                duration = Mathf.Max(duration, y.keys[y.length - 1].time);

            if (enableZ && z != null && z.length > 0)
                duration = Mathf.Max(duration, z.keys[z.length - 1].time);

            return duration;
        }

        public readonly bool IsEnabled()
        {
            return enableX || enableY || enableZ;
        }

        public void Reset()
        {
            x = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
            y = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
            z = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
            enableX = enableY = enableZ = true;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Vector3AnimationCurves))]
    public class Vector3AnimationCurvesDrawer : PropertyDrawer
    {
        const float SPACING = 5f;
        const float CURVE_HEIGHT = 50f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 4 + CURVE_HEIGHT + SPACING * 4;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            // Get properties
            SerializedProperty enableX = property.FindPropertyRelative("enableX");
            SerializedProperty enableY = property.FindPropertyRelative("enableY");
            SerializedProperty enableZ = property.FindPropertyRelative("enableZ");
            SerializedProperty xCurve = property.FindPropertyRelative("x");
            SerializedProperty yCurve = property.FindPropertyRelative("y");
            SerializedProperty zCurve = property.FindPropertyRelative("z");

            float yOffset = EditorGUIUtility.singleLineHeight + SPACING;

            // Draw toggle buttons row
            Rect toggleRowRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
            DrawToggleRow(toggleRowRect, enableX, enableY, enableZ);

            yOffset += EditorGUIUtility.singleLineHeight + SPACING;

            // Draw curve fields
            Rect curvesRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight * 2);
            DrawCurveFields(curvesRect, enableX, enableY, enableZ, xCurve, yCurve, zCurve);

            yOffset += EditorGUIUtility.singleLineHeight * 2 + SPACING;

            // Draw preview
            Rect previewRect = new Rect(position.x, position.y + yOffset, position.width, CURVE_HEIGHT);
            DrawPreview(previewRect, enableX, enableY, enableZ, xCurve, yCurve, zCurve);

            EditorGUI.EndProperty();
        }

        void DrawToggleRow(Rect rect, SerializedProperty enableX, SerializedProperty enableY, SerializedProperty enableZ)
        {
            float toggleWidth = rect.width / 3f;

            Rect xRect = new(rect.x, rect.y, toggleWidth, rect.height);
            Rect yRect = new(rect.x + toggleWidth, rect.y, toggleWidth, rect.height);
            Rect zRect = new(rect.x + toggleWidth * 2, rect.y, toggleWidth, rect.height);

            enableX.boolValue = GUI.Toggle(xRect, enableX.boolValue, "X", "Button");
            enableY.boolValue = GUI.Toggle(yRect, enableY.boolValue, "Y", "Button");
            enableZ.boolValue = GUI.Toggle(zRect, enableZ.boolValue, "Z", "Button");
        }

        void DrawCurveFields(Rect rect, SerializedProperty enableX, SerializedProperty enableY, SerializedProperty enableZ,
                                    SerializedProperty xCurve, SerializedProperty yCurve, SerializedProperty zCurve)
        {
            float curveWidth = rect.width / 3f;

            GUI.enabled = enableX.boolValue;
            Rect xRect = new(rect.x, rect.y, curveWidth, rect.height);
            EditorGUI.PropertyField(xRect, xCurve, GUIContent.none);

            GUI.enabled = enableY.boolValue;
            Rect yRect = new(rect.x + curveWidth, rect.y, curveWidth, rect.height);
            EditorGUI.PropertyField(yRect, yCurve, GUIContent.none);

            GUI.enabled = enableZ.boolValue;
            Rect zRect = new(rect.x + curveWidth * 2, rect.y, curveWidth, rect.height);
            EditorGUI.PropertyField(zRect, zCurve, GUIContent.none);

            GUI.enabled = true;
        }

        void DrawPreview(Rect rect, SerializedProperty enableX, SerializedProperty enableY, SerializedProperty enableZ,
                               SerializedProperty xCurve, SerializedProperty yCurve, SerializedProperty zCurve)
        {
            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            Handles.BeginGUI();

            // Draw each enabled curve
            if (enableX.boolValue && xCurve.animationCurveValue != null && xCurve.animationCurveValue.length > 0)
            {
                Handles.color = Color.red;
                DrawNormalizedCurve(rect, xCurve.animationCurveValue);
            }

            if (enableY.boolValue && yCurve.animationCurveValue != null && yCurve.animationCurveValue.length > 0)
            {
                Handles.color = Color.green;
                DrawNormalizedCurve(rect, yCurve.animationCurveValue);
            }

            if (enableZ.boolValue && zCurve.animationCurveValue != null && zCurve.animationCurveValue.length > 0)
            {
                Handles.color = Color.blue;
                DrawNormalizedCurve(rect, zCurve.animationCurveValue);
            }

            Handles.EndGUI();

            // Draw border
            Handles.BeginGUI();
            Handles.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            Handles.DrawPolyLine(
                new Vector3(rect.x, rect.y, 0),
                new Vector3(rect.x + rect.width, rect.y, 0),
                new Vector3(rect.x + rect.width, rect.y + rect.height, 0),
                new Vector3(rect.x, rect.y + rect.height, 0),
                new Vector3(rect.x, rect.y, 0)
            );
            Handles.EndGUI();
        }

        void DrawNormalizedCurve(Rect rect, AnimationCurve curve)
        {
            int segments = Mathf.Min(50, (int)rect.width);
            Vector3[] points = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1);
                float value = curve.Evaluate(t);

                // Normalize value for display
                float min = 0f, max = 1f;
                foreach (Keyframe key in curve.keys)
                {
                    min = Mathf.Min(min, key.value);
                    max = Mathf.Max(max, key.value);
                }

                float normalizedValue = Mathf.InverseLerp(min, max, value);

                points[i] = new Vector3(
                    rect.x + t * rect.width,
                    rect.y + rect.height - normalizedValue * rect.height,
                    0
                );
            }

            Handles.DrawAAPolyLine(2f, points);
        }
    }
#endif
    #endregion

    #region HideIf Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class HideIfAnyAttribute : PropertyAttribute
    {
        public readonly object[] Triplets; // (field, value, hideWhen) x N
        public MOSTEdit EditState;
        public HideIfAnyAttribute(params object[] triplets) => Triplets = triplets;
        public HideIfAnyAttribute(MOSTEdit edi) => EditState = edi;
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class HideIfAllAttribute : PropertyAttribute
    {
        public readonly object[] Triplets; // (field, value, hideWhen) x N
        public MOSTEdit EditState;
        public HideIfAllAttribute(params object[] triplets) => Triplets = triplets;
        public HideIfAllAttribute(MOSTEdit edi) => EditState = edi;
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(HideIfAnyAttribute), true)]
    [CustomPropertyDrawer(typeof(HideIfAllAttribute), true)]
    public class HideIfMultiDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldHide(property))
                return;

            var fieldLabel = new GUIContent(property.displayName, label?.tooltip);
            EditorGUI.BeginProperty(position, fieldLabel, property);
            EditorGUI.PropertyField(position, property, fieldLabel, true);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ShouldHide(property)
                ? 0f
                : EditorGUI.GetPropertyHeight(property, new GUIContent(property.displayName, label?.tooltip), true);
        }

        bool ShouldHide(SerializedProperty property)
        {
            //// Extract rules
            //MOSTEdit ed = attribute switch
            //{
            //    HideIfAnyAttribute a => a.EditState,
            //    HideIfAllAttribute a => a.EditState,
            //    _ => MOSTEdit.None
            //};
            //if (ed == MOSTEdit.RuntimeOnly && EditorApplication.isPlaying) return true;

            object[] triplets = attribute switch
            {
                HideIfAnyAttribute a => a.Triplets,
                HideIfAllAttribute a => a.Triplets,
                _ => Array.Empty<object>()
            };

            if (triplets == null || triplets.Length == 0) return false;
            if (triplets.Length % 3 != 0)
            {
                Debug.LogError($"[{attribute.GetType().Name}] Expected triplets: (field, value, hideWhen)*N");
                return false;
            }

            bool isAll = attribute is HideIfAllAttribute;
            bool anyHit = false;

            for (int i = 0; i < triplets.Length; i += 3)
            {
                string fieldName = triplets[i] as string;
                object compare = triplets[i + 1];
                bool hideWhen = triplets[i + 2] is bool b && b;

                if (string.IsNullOrEmpty(fieldName))
                    continue;

                var condProp = ResolveCondition(property, fieldName);
                if (condProp == null) // unresolved → treat as not hiding
                {
                    if (isAll) return false; // ALL fails if any can't be evaluated
                    continue;                // ANY just skips this rule
                }

                bool hit;
                if (compare is bool)
                {
                    // boolean rule: hide when (cond == hideWhen)
                    bool cond = ReadBool(condProp);
                    hit = cond == hideWhen;
                }
                else
                {
                    int c = ReadInt(condProp);
                    int v = ToInt(compare);
                    // enum/int rule: hide when ((c == v) == hideWhen)
                    hit = c == v == hideWhen;
                }

                if (isAll)
                {
                    if (!hit) return false; // ALL requires all rules hit
                }
                else
                {
                    if (hit) { anyHit = true; break; } // ANY short-circuit
                }
            }

            return isAll || anyHit;
        }

        static SerializedProperty ResolveCondition(SerializedProperty context, string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;

            // 0) dotted or absolute path as-is
            var p = context.serializedObject.FindProperty(nameOrPath);
            if (p != null) return p;

            // 1) basePath.name
            string basePath = GetBase(context.propertyPath);
            if (!string.IsNullOrEmpty(basePath))
            {
                p = context.serializedObject.FindProperty($"{basePath}.{nameOrPath}");
                if (p != null) return p;
            }

            // 2) bubble up owners
            string path = basePath;
            while (!string.IsNullOrEmpty(path))
            {
                int lastDot = path.LastIndexOf('.');
                if (lastDot <= 0) { path = string.Empty; break; }
                path = path[..lastDot];
                p = context.serializedObject.FindProperty($"{path}.{nameOrPath}");
                if (p != null) return p;
            }

            // 3) root fallback
            return context.serializedObject.FindProperty(nameOrPath);
        }

        static string GetBase(string propPath)
        {
            int i = propPath.LastIndexOf('.');
            return i > 0 ? propPath[..i] : string.Empty;
        }

        static bool ReadBool(SerializedProperty p) =>
            p.propertyType switch
            {
                SerializedPropertyType.Boolean => p.boolValue,
                SerializedPropertyType.Enum => p.enumValueIndex != 0,
                SerializedPropertyType.Integer => p.intValue != 0,
                SerializedPropertyType.ObjectReference => p.objectReferenceValue != null,
                SerializedPropertyType.String => !string.IsNullOrEmpty(p.stringValue),
                _ => ReadInt(p) != 0
            };

        static int ReadInt(SerializedProperty p) =>
            p.propertyType switch
            {
                SerializedPropertyType.Enum => p.enumValueIndex,
                SerializedPropertyType.Integer => p.intValue,
                SerializedPropertyType.Boolean => p.boolValue ? 1 : 0,
                SerializedPropertyType.Character => p.intValue,
                SerializedPropertyType.ObjectReference => p.objectReferenceValue ? 1 : 0,
                SerializedPropertyType.String => string.IsNullOrEmpty(p.stringValue) ? int.MinValue : p.stringValue.GetHashCode(),
                _ => 0
            };

        static int ToInt(object o) =>
            o switch
            {
                null => int.MinValue,
                Enum e => Convert.ToInt32(e),
                bool b => b ? 1 : 0,
                sbyte or byte or short or ushort or int => Convert.ToInt32(o),
                long l => unchecked((int)l),
                string s => string.IsNullOrEmpty(s) ? int.MinValue : s.GetHashCode(),
                _ => o.GetHashCode()
            };
    }
#endif
    #endregion

    #region Inspector Button Property

    [Serializable]
    public struct InspectorButton
    {
        // Unity doesn't reliably serialize an empty struct; keep a hidden dummy.
        [SerializeField, HideInInspector] private int _dummy;
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class InspectorButtonAttribute : PropertyAttribute
    {
        public string MethodName { get; }

        // Named arguments in attributes require settable properties/fields:
        public string Label { get; set; }
        public string Tooltip { get; set; }

        public bool EnabledInEditMode { get; set; } = true;
        public bool EnabledInPlayMode { get; set; } = false;

        public bool AllowMultiObject { get; set; } = true;
        public bool RecordUndo { get; set; } = true;

        /// <summary>Optional confirmation dialog text. Null/empty disables.</summary>
        public string Confirm { get; set; } = null;

        /// <summary>0 = default single line height</summary>
        public float Height { get; set; } = 0f;

        public InspectorButtonAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(InspectorButton))]
    public class InspectorButtonDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = fieldInfo?.GetCustomAttribute<InspectorButtonAttribute>(true);
            if (attr == null)
            {
                EditorGUI.HelpBox(position, "InspectorButton field requires [InspectorButton(\"MethodName\")] attribute.", MessageType.Warning);
                return;
            }

            bool isPlaying = Application.isPlaying;
            bool enabled = isPlaying ? attr.EnabledInPlayMode : attr.EnabledInEditMode;

            var targets = property.serializedObject.targetObjects;
            if (!attr.AllowMultiObject && targets != null && targets.Length > 1)
                enabled = false;

            string btnText = string.IsNullOrWhiteSpace(attr.Label)
                ? ObjectNames.NicifyVariableName(property.name)
                : attr.Label;

            var content = new GUIContent(btnText, attr.Tooltip);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (GUI.Button(position, content))
                {
                    // flush any pending edits first
                    property.serializedObject.ApplyModifiedProperties();

                    if (!string.IsNullOrWhiteSpace(attr.Confirm))
                    {
                        if (!EditorUtility.DisplayDialog("Confirm", attr.Confirm, "OK", "Cancel"))
                            return;
                    }

                    InvokeOnTargets(property, attr);
                    property.serializedObject.Update();
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attr = fieldInfo?.GetCustomAttribute<InspectorButtonAttribute>(true);
            if (attr == null) return EditorGUIUtility.singleLineHeight;

            return (attr.Height > 0f) ? attr.Height : EditorGUIUtility.singleLineHeight;
        }

        private static void InvokeOnTargets(SerializedProperty property, InspectorButtonAttribute attr)
        {
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return;

            UnityEngine.Object firstReturned = null;

            foreach (var t in targets)
            {
                if (t == null) continue;

                var type = t.GetType();
                var method = FindCallableMethod(type, attr.MethodName);

                if (method == null)
                {
                    Debug.LogError($"[InspectorButton] Method '{attr.MethodName}' not found on {type.Name}.", t);
                    continue;
                }

                try
                {
                    if (attr.RecordUndo)
                        Undo.RecordObject(t, $"Invoke {attr.MethodName}");

                    object result = Invoke(method, t, property.serializedObject);

                    EditorUtility.SetDirty(t);

                    if (firstReturned == null && result is UnityEngine.Object uo && uo != null)
                        firstReturned = uo;
                }
                catch (TargetInvocationException tie)
                {
                    Debug.LogException(tie.InnerException ?? tie, t);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, t);
                }
            }

            // Nice UX: if method returned an asset/object (e.g., generated prefab), select it
            if (firstReturned != null)
            {
                Selection.activeObject = firstReturned;
                EditorGUIUtility.PingObject(firstReturned);
            }
        }

        private static object Invoke(MethodInfo method, UnityEngine.Object target, SerializedObject so)
        {
            var parms = method.GetParameters();

            if (parms.Length == 0)
                return method.Invoke(method.IsStatic ? null : target, null);

            if (parms.Length == 1 && parms[0].ParameterType == typeof(UnityEngine.Object))
                return method.Invoke(method.IsStatic ? null : target, new object[] { target });

            if (parms.Length == 1 && parms[0].ParameterType == typeof(SerializedObject))
                return method.Invoke(method.IsStatic ? null : target, new object[] { so });

            throw new InvalidOperationException(
                $"Unsupported signature for '{method.Name}'. Use: () or (UnityEngine.Object) or (SerializedObject)."
            );
        }

        private static MethodInfo FindCallableMethod(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name)) return null;

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;

            // () signature
            var m = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (m != null) return m;

            // (UnityEngine.Object)
            m = type.GetMethod(name, flags, null, new[] { typeof(UnityEngine.Object) }, null);
            if (m != null) return m;

            // (SerializedObject)
            m = type.GetMethod(name, flags, null, new[] { typeof(SerializedObject) }, null);
            if (m != null) return m;

            return null;
        }
    }
#endif

    #endregion

    #region ShapeGrid Property

    public enum ShapeGridRightClickMode
    {
        Clear = 0,           // always clears to Empty
        ToggleTriggered = 1, // prefab -> clear, empty <-> triggered
        ContextMenu = 2,     // opens the full edit menu
        Disabled = 3
    }

    public enum ShapeGridBrushMode
    {
        Select = 0,
        PaintPrefab = 1,
        Block = 2,
        Erase = 3,
        Start = 4,
        End = 5,
        Pick = 6
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ShapeGridOptionsAttribute : PropertyAttribute
    {
        public Type objectType;
        public bool allowSceneObjects;
        public float maxCellSize;
        public float minCellSize;
        public float spacing;

        public bool enableTriggeredState = true;
        public ShapeGridRightClickMode rightClickMode = ShapeGridRightClickMode.ToggleTriggered;
        public bool shiftRightClickAlwaysClears = true;

        // Extra editor UX toggles. Existing usages of the attribute keep working.
        public bool enableBrushToolbar = true;
        public bool enableKeyboardShortcuts = true;
        public bool enableRectangleFill = true;
        public bool enablePatternTools = true;
        public bool enableTextCopyPaste = true;
        public bool enableMultiObjectDragDrop = true;

        public ShapeGridOptionsAttribute(
            Type objectType = null,
            bool allowSceneObjects = false,
            float maxCellSize = 72f,
            float minCellSize = 14f,
            float spacing = 4f)
        {
            this.objectType = objectType ?? typeof(GameObject);
            this.allowSceneObjects = allowSceneObjects;
            this.maxCellSize = maxCellSize;
            this.minCellSize = minCellSize;
            this.spacing = spacing;
        }
    }

    [Serializable]
    public class ShapeGrid : ISerializationCallbackReceiver
    {
        public enum CellState
        {
            Empty = 0,
            Triggered = 1, // displayed as "Blocked" in the editor UX
            Prefab = 2
        }

        [Min(1)] public int columns = 3;
        [Min(1)] public int rows = 3;
        public bool UseMiniIcon;

        [SerializeField] List<UnityEngine.Object> cells = new List<UnityEngine.Object>();
        [SerializeField] List<CellState> states = new List<CellState>();

        // Bool flags keep old assets from suddenly becoming Start=(0,0) or End=(0,0).
        [SerializeField] bool hasStartCell = false;
        [SerializeField] Vector2Int startCell = Vector2Int.zero;

        [SerializeField] bool hasEndCell = false;
        [SerializeField] Vector2Int endCell = Vector2Int.zero;

        public int Count { get { return Mathf.Max(1, columns) * Mathf.Max(1, rows); } }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < columns && y >= 0 && y < rows;
        }

        public int Index(int x, int y)
        {
            return y * columns + x;
        }

        public bool HasStartCell { get { return hasStartCell && InBounds(startCell.x, startCell.y); } }
        public bool HasEndCell { get { return hasEndCell && InBounds(endCell.x, endCell.y); } }

        public Vector2Int StartCell { get { return HasStartCell ? startCell : new Vector2Int(-1, -1); } }
        public Vector2Int EndCell { get { return HasEndCell ? endCell : new Vector2Int(-1, -1); } }

        public UnityEngine.Object StartCellObject { get { return HasStartCell ? Get(startCell.x, startCell.y) : null; } }
        public UnityEngine.Object EndCellObject { get { return HasEndCell ? Get(endCell.x, endCell.y) : null; } }

        public bool IsStartCell(int x, int y)
        {
            return HasStartCell && startCell.x == x && startCell.y == y;
        }

        public bool IsEndCell(int x, int y)
        {
            return HasEndCell && endCell.x == x && endCell.y == y;
        }

        public void ClearStartCell()
        {
            hasStartCell = false;
            startCell = Vector2Int.zero;
        }

        public void ClearEndCell()
        {
            hasEndCell = false;
            endCell = Vector2Int.zero;
        }

        public void MarkStartCell(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y))
            {
                ClearStartCell();
                return;
            }

            int i = Index(x, y);
            if (cells[i] == null && states[i] == CellState.Triggered)
                states[i] = CellState.Empty;

            hasStartCell = true;
            startCell = new Vector2Int(x, y);

            if (HasEndCell && endCell == startCell)
                ClearEndCell();
        }

        public void MarkEndCell(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y))
            {
                ClearEndCell();
                return;
            }

            int i = Index(x, y);
            if (cells[i] == null && states[i] == CellState.Triggered)
                states[i] = CellState.Empty;

            hasEndCell = true;
            endCell = new Vector2Int(x, y);

            if (HasStartCell && startCell == endCell)
                ClearStartCell();
        }

        void ClearSpecialIfMatches(int x, int y)
        {
            if (HasStartCell && startCell.x == x && startCell.y == y)
                ClearStartCell();
            if (HasEndCell && endCell.x == x && endCell.y == y)
                ClearEndCell();
        }

        public UnityEngine.Object Get(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y)) return null;
            return cells[Index(x, y)];
        }

        public T Get<T>(int x, int y) where T : UnityEngine.Object
        {
            return Get(x, y) as T;
        }

        public CellState GetState(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y)) return CellState.Empty;

            int i = Index(x, y);
            if (cells[i] != null) return CellState.Prefab;
            return states[i] == CellState.Prefab ? CellState.Empty : states[i];
        }

        public bool IsTriggered(int x, int y)
        {
            return GetState(x, y) == CellState.Triggered;
        }

        public void Set(int x, int y, UnityEngine.Object value)
        {
            EnsureSize();
            if (!InBounds(x, y)) return;

            int i = Index(x, y);
            cells[i] = value;
            states[i] = value != null ? CellState.Prefab : CellState.Empty;
        }

        public void SetTriggered(int x, int y, bool triggered)
        {
            EnsureSize();
            if (!InBounds(x, y)) return;

            int i = Index(x, y);
            if (cells[i] != null)
                return;

            if (triggered)
                ClearSpecialIfMatches(x, y);

            states[i] = triggered ? CellState.Triggered : CellState.Empty;
        }

        public void ToggleTriggered(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y)) return;

            int i = Index(x, y);
            if (cells[i] != null) return;

            bool willBeTriggered = states[i] != CellState.Triggered;
            if (willBeTriggered)
                ClearSpecialIfMatches(x, y);

            states[i] = willBeTriggered ? CellState.Triggered : CellState.Empty;
        }

        public void ClearCell(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y)) return;

            int i = Index(x, y);
            cells[i] = null;
            states[i] = CellState.Empty;
            ClearSpecialIfMatches(x, y);
        }

        public void ClearAll()
        {
            EnsureSize();
            for (int i = 0; i < cells.Count; i++)
            {
                cells[i] = null;
                states[i] = CellState.Empty;
            }

            ClearStartCell();
            ClearEndCell();
        }

        public void Resize(int newColumns, int newRows, bool preserve = true)
        {
            newColumns = Mathf.Max(1, newColumns);
            newRows = Mathf.Max(1, newRows);

            EnsureSize();

            int oldCols = columns;
            int oldRows = rows;

            List<UnityEngine.Object> oldCells = new List<UnityEngine.Object>(cells);
            List<CellState> oldStates = new List<CellState>(states);

            columns = newColumns;
            rows = newRows;

            int newCount = Count;
            cells = new List<UnityEngine.Object>(newCount);
            states = new List<CellState>(newCount);

            for (int i = 0; i < newCount; i++)
            {
                cells.Add(null);
                states.Add(CellState.Empty);
            }

            if (preserve)
            {
                int copyCols = Mathf.Min(oldCols, newColumns);
                int copyRows = Mathf.Min(oldRows, newRows);

                for (int y = 0; y < copyRows; y++)
                {
                    for (int x = 0; x < copyCols; x++)
                    {
                        int oldIndex = y * oldCols + x;
                        int newIndex = y * newColumns + x;

                        if (oldIndex >= 0 && oldIndex < oldCells.Count)
                        {
                            cells[newIndex] = oldCells[oldIndex];

                            CellState st = oldIndex < oldStates.Count ? oldStates[oldIndex] : CellState.Empty;
                            states[newIndex] = st;

                            if (cells[newIndex] != null) states[newIndex] = CellState.Prefab;
                            if (cells[newIndex] == null && states[newIndex] == CellState.Prefab) states[newIndex] = CellState.Empty;
                        }
                    }
                }
            }

            if (hasStartCell && !InBounds(startCell.x, startCell.y)) ClearStartCell();
            if (hasEndCell && !InBounds(endCell.x, endCell.y)) ClearEndCell();
            if (HasStartCell && HasEndCell && startCell == endCell) ClearEndCell();
        }

        public IEnumerable<(int x, int y, UnityEngine.Object value)> EnumerateFilled()
        {
            EnsureSize();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = Index(x, y);
                    UnityEngine.Object v = cells[i];
                    if (v != null)
                        yield return (x, y, v);
                }
            }
        }

        public IEnumerable<(int x, int y)> EnumerateTriggered()
        {
            EnsureSize();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = Index(x, y);
                    if (cells[i] == null && states[i] == CellState.Triggered)
                        yield return (x, y);
                }
            }
        }

        void EnsureSize(bool sanitizeUnityObjects = true)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            int needed = Count;

            if (cells == null) cells = new List<UnityEngine.Object>(needed);
            if (states == null) states = new List<CellState>(needed);

            while (cells.Count < needed) cells.Add(null);
            while (states.Count < needed) states.Add(CellState.Empty);

            if (cells.Count > needed) cells.RemoveRange(needed, cells.Count - needed);
            if (states.Count > needed) states.RemoveRange(needed, states.Count - needed);

            if (sanitizeUnityObjects)
            {
                for (int i = 0; i < needed; i++)
                {
                    if (cells[i] != null)
                        states[i] = CellState.Prefab;
                    else if (states[i] == CellState.Prefab)
                        states[i] = CellState.Empty;
                }
            }

            if (hasStartCell && !InBounds(startCell.x, startCell.y)) ClearStartCell();
            if (hasEndCell && !InBounds(endCell.x, endCell.y)) ClearEndCell();
            if (HasStartCell && HasEndCell && startCell == endCell) ClearEndCell();

            if (HasStartCell)
            {
                int si = Index(startCell.x, startCell.y);
                if (cells[si] == null && states[si] == CellState.Triggered) ClearStartCell();
            }

            if (HasEndCell)
            {
                int ei = Index(endCell.x, endCell.y);
                if (cells[ei] == null && states[ei] == CellState.Triggered) ClearEndCell();
            }
        }

        public void OnBeforeSerialize()
        {
            EnsureSize(false);
        }

        public void OnAfterDeserialize()
        {
            EnsureSize(false);
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ShapeGrid))]
    public class ShapeGridDrawer : PropertyDrawer
    {
        const float OuterPadding = 6f;
        const float InnerPadding = 6f;
        const float VSpace = 6f;
        const float RowGap = 2f;
        const float ButtonGap = 4f;
        const int PreviewCacheLimit = 512;

        class DrawerState
        {
            public ShapeGridBrushMode brushMode = ShapeGridBrushMode.Select;
            public UnityEngine.Object activeBrushObject;

            public bool isPainting;
            public int lastPaintedIndex = -1;

            public bool isRectSelecting;
            public Vector2Int rectStart;
            public Vector2Int rectEnd;

            public Vector2Int hoveredCell = new Vector2Int(-1, -1);
            public string lastStatus = string.Empty;
        }

        static readonly Dictionary<string, DrawerState> s_stateByKey = new Dictionary<string, DrawerState>();
        static readonly Dictionary<int, Texture> s_previewCache = new Dictionary<int, Texture>();

        static int s_pickerControlId = -1;
        static string s_pickerPropertyPath;
        static int s_pickerCellIndex = -1;
        static string s_pickerStateKey;

        static GUIStyle s_badgeStyle;
        static GUIStyle s_centerMiniStyle;
        static GUIStyle s_smallButtonStyle;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty hasStartProp = property.FindPropertyRelative("hasStartCell");
            SerializedProperty startCellProp = property.FindPropertyRelative("startCell");
            SerializedProperty hasEndProp = property.FindPropertyRelative("hasEndCell");
            SerializedProperty endCellProp = property.FindPropertyRelative("endCell");

            SerializedProperty colsProp = property.FindPropertyRelative("columns");
            SerializedProperty rowsProp = property.FindPropertyRelative("rows");
            SerializedProperty cellsProp = property.FindPropertyRelative("cells");
            SerializedProperty statesProp = property.FindPropertyRelative("states");
            SerializedProperty miniIconProp = property.FindPropertyRelative("UseMiniIcon");

            int cols = Mathf.Max(1, colsProp.intValue);
            int rows = Mathf.Max(1, rowsProp.intValue);

            ShapeGridOptionsAttribute opts = GetOptionsOrDefault();
            Type objType = opts.objectType ?? typeof(GameObject);
            if (!typeof(UnityEngine.Object).IsAssignableFrom(objType))
                objType = typeof(UnityEngine.Object);

            DrawerState state = GetState(property);

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            Rect inner = new Rect(
                position.x + OuterPadding,
                position.y + OuterPadding,
                position.width - OuterPadding * 2f,
                position.height - OuterPadding * 2f
            );

            float line = EditorGUIUtility.singleLineHeight;
            Rect row = new Rect(inner.x, inner.y, inner.width, line);

            DrawHeaderRow(row, property, label, colsProp, rowsProp, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, ref cols, ref rows);
            row.y += line + RowGap;

            if (opts.enableBrushToolbar)
            {
                DrawBrushObjectRow(row, state, objType, opts.allowSceneObjects);
                row.y += line + RowGap;

                DrawBrushToolbarRow(row, state, opts.enableTriggeredState);
                row.y += line + RowGap;
            }

            DrawMiniStatusRow(row, state, miniIconProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, cellsProp, statesProp);
            row.y += line + RowGap;


            if (opts.enablePatternTools)
            {
                DrawPatternRow(row, state, property, cols, rows, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, opts.enableTriggeredState);
                row.y += line + RowGap;
            }

            DrawShortcutHelpRow(row, opts);
            row.y += line + VSpace;

            EnsureSizes(cellsProp, statesProp, cols * rows);
            SanitizeStartEnd(hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
            SanitizeStates(cellsProp, statesProp);

            Rect gridArea = new Rect(
                inner.x + InnerPadding,
                row.y,
                inner.width - InnerPadding * 2f,
                inner.yMax - row.y
            );

            float spacing = Mathf.Max(0f, opts.spacing);
            float rawCell = Mathf.Floor((gridArea.width - spacing * (cols - 1)) / cols);
            float cellSize = Mathf.Clamp(rawCell, opts.minCellSize, opts.maxCellSize);
            if (rawCell < opts.minCellSize)
                cellSize = Mathf.Max(10f, rawCell);

            float usedW = cols * cellSize + (cols - 1) * spacing;
            float usedH = rows * cellSize + (rows - 1) * spacing;

            float startX = gridArea.x + Mathf.Max(0f, (gridArea.width - usedW) * 0.5f);
            float startY = gridArea.y;

            Rect actualGridRect = new Rect(startX, startY, usedW, usedH);

            int hoverX;
            int hoverY;
            int hoverIndex;
            bool hasHover = TryGetCellAtMouse(Event.current.mousePosition, startX, startY, cols, rows, cellSize, spacing, out hoverX, out hoverY, out hoverIndex);
            state.hoveredCell = hasHover ? new Vector2Int(hoverX, hoverY) : new Vector2Int(-1, -1);

            HandleObjectPickerEvent(property, cellsProp, statesProp, objType, GetStateKey(property));
            HandleKeyboardShortcuts(position, state, property, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, hasHover, hoverX, hoverY, hoverIndex, opts);
            HandleGridInteraction(state, property, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, hasHover, hoverX, hoverY, hoverIndex, objType, opts);

            DrawCells(
                state,
                cellsProp,
                statesProp,
                hasStartProp,
                startCellProp,
                hasEndProp,
                endCellProp,
                cols,
                rows,
                startX,
                startY,
                cellSize,
                spacing,
                miniIconProp.boolValue,
                objType
            );

            DrawRectSelectionPreview(state, actualGridRect, cols, rows, startX, startY, cellSize, spacing);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty colsProp = property.FindPropertyRelative("columns");
            SerializedProperty rowsProp = property.FindPropertyRelative("rows");

            int cols = Mathf.Max(1, colsProp.intValue);
            int rows = Mathf.Max(1, rowsProp.intValue);

            ShapeGridOptionsAttribute opts = GetOptionsOrDefault();
            float spacing = Mathf.Max(0f, opts.spacing);

            float viewW = Mathf.Max(220f, EditorGUIUtility.currentViewWidth);
            float approxInnerW = viewW - 40f;

            float rawCell = Mathf.Floor((approxInnerW - spacing * (cols - 1)) / cols);
            float cellSize = Mathf.Clamp(rawCell, opts.minCellSize, opts.maxCellSize);
            if (rawCell < opts.minCellSize)
                cellSize = Mathf.Max(10f, rawCell);

            float line = EditorGUIUtility.singleLineHeight;
            int controlRows = 3; // header, mini/status, shortcut help
            if (opts.enableBrushToolbar) controlRows += 2;
            if (opts.enablePatternTools) controlRows += 1;

            float controlsH = controlRows * line + Mathf.Max(0, controlRows - 1) * RowGap + VSpace;
            float gridH = rows * cellSize + (rows - 1) * spacing;

            return OuterPadding * 2f
                 + controlsH
                 + InnerPadding * 2f
                 + gridH
                 + VSpace;
        }

        static void DrawHeaderRow(
            Rect row,
            SerializedProperty property,
            GUIContent label,
            SerializedProperty colsProp,
            SerializedProperty rowsProp,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            ref int cols,
            ref int rows)
        {
            Rect titleRect = new Rect(row.x, row.y, Mathf.Min(row.width, 220f), row.height);
            EditorGUI.LabelField(titleRect, label, EditorStyles.boldLabel);

            float clearW = 58f;
            float fieldW = 62f;
            float gap = 6f;

            Rect clearRect = new Rect(row.xMax - clearW, row.y, clearW, row.height);
            Rect rowsRect = new Rect(clearRect.xMin - gap - fieldW, row.y, fieldW, row.height);
            Rect colsRect = new Rect(rowsRect.xMin - gap - fieldW, row.y, fieldW, row.height);

            int oldCols = cols;
            int oldRows = rows;

            EditorGUI.BeginChangeCheck();
            int newCols = Mathf.Max(1, MiniIntField(colsRect, "C", cols));
            int newRows = Mathf.Max(1, MiniIntField(rowsRect, "R", rows));

            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo(property.serializedObject, "Resize Shape Grid");

                colsProp.intValue = newCols;
                rowsProp.intValue = newRows;

                ResizePreserveByXY(cellsProp, statesProp, oldCols, oldRows, newCols, newRows);
                SanitizeStartEnd(hasStartProp, startCellProp, hasEndProp, endCellProp, newCols, newRows);

                cols = newCols;
                rows = newRows;
                property.serializedObject.ApplyModifiedProperties();
            }

            if (GUI.Button(clearRect, "Clear"))
            {
                if (EditorUtility.DisplayDialog("Clear Shape Grid", "Clear all cells, Start, and End?", "Clear", "Cancel"))
                {
                    RecordUndo(property.serializedObject, "Clear Shape Grid");
                    ClearGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        static void DrawBrushObjectRow(Rect row, DrawerState state, Type objType, bool allowSceneObjects)
        {
            Rect labelRect = new Rect(row.x, row.y, 84f, row.height);
            EditorGUI.LabelField(labelRect, "Active Prefab", EditorStyles.miniBoldLabel);

            Rect fieldRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelRect.width - 4f, row.height);
            UnityEngine.Object next = EditorGUI.ObjectField(fieldRect, state.activeBrushObject, objType, allowSceneObjects);
            state.activeBrushObject = ValidateObjectForBrush(next, objType, allowSceneObjects);
        }

        static void DrawBrushToolbarRow(Rect row, DrawerState state, bool enableTriggered)
        {
            string[] labels;
            ShapeGridBrushMode[] modes;

            if (enableTriggered)
            {
                labels = new[] { "Select", "Paint", "Block", "Erase", "Start", "End", "Pick" };
                modes = new[]
                {
                    ShapeGridBrushMode.Select,
                    ShapeGridBrushMode.PaintPrefab,
                    ShapeGridBrushMode.Block,
                    ShapeGridBrushMode.Erase,
                    ShapeGridBrushMode.Start,
                    ShapeGridBrushMode.End,
                    ShapeGridBrushMode.Pick
                };
            }
            else
            {
                labels = new[] { "Select", "Paint", "Erase", "Start", "End", "Pick" };
                modes = new[]
                {
                    ShapeGridBrushMode.Select,
                    ShapeGridBrushMode.PaintPrefab,
                    ShapeGridBrushMode.Erase,
                    ShapeGridBrushMode.Start,
                    ShapeGridBrushMode.End,
                    ShapeGridBrushMode.Pick
                };

                if (state.brushMode == ShapeGridBrushMode.Block)
                    state.brushMode = ShapeGridBrushMode.Erase;
            }

            int selected = 0;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == state.brushMode)
                {
                    selected = i;
                    break;
                }
            }

            int next = GUI.Toolbar(row, selected, labels);
            if (next >= 0 && next < modes.Length)
                state.brushMode = modes[next];
        }

        static void DrawMiniStatusRow(
            Rect row,
            DrawerState state,
            SerializedProperty miniIconProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            SerializedProperty cellsProp,
            SerializedProperty statesProp)
        {
            Rect miniToggleRect = new Rect(row.x, row.y, 104f, row.height);
            miniIconProp.boolValue = EditorGUI.ToggleLeft(miniToggleRect, "Mini Icon", miniIconProp.boolValue);

            int filled;
            int blocked;
            int empty;
            CountCells(cellsProp, statesProp, out filled, out blocked, out empty);

            string hover = state.hoveredCell.x >= 0 ? string.Format("Hover: ({0},{1})", state.hoveredCell.x, state.hoveredCell.y) : "Hover: -";
            string startEnd = FormatStartEnd(hasStartProp, startCellProp, hasEndProp, endCellProp);
            string summary = string.Format("{0}x{1} | Filled {2} | Blocked {3} | Empty {4} | {5} | {6}", cols, rows, filled, blocked, empty, hover, ObjectNames.NicifyVariableName(state.brushMode.ToString()));

            if (!string.IsNullOrEmpty(startEnd))
                summary += " | " + startEnd;

            Rect summaryRect = new Rect(miniToggleRect.xMax + 8f, row.y, row.xMax - miniToggleRect.xMax - 8f, row.height);
            EditorGUI.LabelField(summaryRect, summary, EditorStyles.miniLabel);
        }

        static void DrawPatternRow(
            Rect row,
            DrawerState state,
            SerializedProperty gridProperty,
            int cols,
            int rows,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            bool enableTriggered)
        {
            Rect r = row;
            float w = Mathf.Floor((row.width - ButtonGap * 5f) / 6f);

            if (SmallButton(ref r, w, "Fill"))
            {
                RecordUndo(gridProperty.serializedObject, "Fill Shape Grid");
                FillAllWithBrush(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered);
                gridProperty.serializedObject.ApplyModifiedProperties();
            }

            if (SmallButton(ref r, w, "Border"))
            {
                RecordUndo(gridProperty.serializedObject, "Block Shape Grid Border");
                FillBorder(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered);
                gridProperty.serializedObject.ApplyModifiedProperties();
            }

            if (SmallButton(ref r, w, "Invert"))
            {
                RecordUndo(gridProperty.serializedObject, "Invert Shape Grid Blocks");
                InvertBlocked(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered);
                gridProperty.serializedObject.ApplyModifiedProperties();
            }

            if (SmallButton(ref r, w, "Mirror H"))
            {
                RecordUndo(gridProperty.serializedObject, "Mirror Shape Grid Horizontally");
                MirrorHorizontal(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                gridProperty.serializedObject.ApplyModifiedProperties();
            }

            if (SmallButton(ref r, w, "Mirror V"))
            {
                RecordUndo(gridProperty.serializedObject, "Mirror Shape Grid Vertically");
                MirrorVertical(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                gridProperty.serializedObject.ApplyModifiedProperties();
            }

            if (SmallButton(ref r, w, "More"))
            {
                ShowGridContextMenu(state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered);
            }
        }

        static void DrawShortcutHelpRow(Rect row, ShapeGridOptionsAttribute opts)
        {
            string text = opts.enableKeyboardShortcuts
                ? "Shortcuts: P Paint, B Block, E Erase, S Start, F End, I Pick, Ctrl/Cmd+Click Picker, Shift+Drag Rectangle, Ctrl/Cmd+C/V Copy/Paste"
                : "Tip: Ctrl/Cmd+Click opens the object picker. Right-click opens quick actions depending on the field options.";

            EditorGUI.LabelField(row, text, EditorStyles.miniLabel);
        }

        static bool SmallButton(ref Rect row, float width, string label)
        {
            if (s_smallButtonStyle == null)
                s_smallButtonStyle = new GUIStyle(EditorStyles.miniButton);

            Rect rect = new Rect(row.x, row.y, width, row.height);
            row.x += width + ButtonGap;
            row.width -= width + ButtonGap;
            return GUI.Button(rect, label, s_smallButtonStyle);
        }

        static void HandleKeyboardShortcuts(
            Rect position,
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool hasHover,
            int hoverX,
            int hoverY,
            int hoverIndex,
            ShapeGridOptionsAttribute opts)
        {
            Event e = Event.current;
            if (!opts.enableKeyboardShortcuts || e == null || e.type != EventType.KeyDown)
                return;

            if (!position.Contains(e.mousePosition))
                return;

            bool used = true;
            bool actionEdit = false;

            if (e.control || e.command)
            {
                if (e.keyCode == KeyCode.C && opts.enableTextCopyPaste)
                {
                    EditorGUIUtility.systemCopyBuffer = CopyGridAsText(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                }
                else if (e.keyCode == KeyCode.V && opts.enableTextCopyPaste)
                {
                    RecordUndo(gridProperty.serializedObject, "Paste Shape Grid Text");
                    if (TryPasteGridFromText(EditorGUIUtility.systemCopyBuffer, state.activeBrushObject, cols, rows, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, out int newCols, out int newRows))
                    {
                        gridProperty.serializedObject.ApplyModifiedProperties();
                        actionEdit = true;
                    }
                }
                else
                {
                    used = false;
                }
            }
            else
            {
                switch (e.keyCode)
                {
                    case KeyCode.P:
                        state.brushMode = ShapeGridBrushMode.PaintPrefab;
                        break;
                    case KeyCode.B:
                        if (opts.enableTriggeredState) state.brushMode = ShapeGridBrushMode.Block;
                        break;
                    case KeyCode.E:
                        state.brushMode = ShapeGridBrushMode.Erase;
                        break;
                    case KeyCode.S:
                        state.brushMode = ShapeGridBrushMode.Start;
                        break;
                    case KeyCode.F:
                        state.brushMode = ShapeGridBrushMode.End;
                        break;
                    case KeyCode.I:
                        state.brushMode = ShapeGridBrushMode.Pick;
                        break;
                    case KeyCode.C:
                        if (hasHover)
                        {
                            RecordUndo(gridProperty.serializedObject, "Clear Shape Grid Cell");
                            ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, hoverX, hoverY, hoverIndex);
                            gridProperty.serializedObject.ApplyModifiedProperties();
                            actionEdit = true;
                        }
                        else
                        {
                            used = false;
                        }
                        break;
                    default:
                        used = false;
                        break;
                }
            }

            if (used)
            {
                if (actionEdit) GUI.changed = true;
                e.Use();
            }
        }

        static void HandleGridInteraction(
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool hasHover,
            int hoverX,
            int hoverY,
            int hoverIndex,
            Type objType,
            ShapeGridOptionsAttribute opts)
        {
            Event e = Event.current;
            if (e == null)
                return;

            HandleDragAndDrop(state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, hasHover, hoverX, hoverY, hoverIndex, objType, opts);

            if (e.type == EventType.MouseDown && e.button == 1 && hasHover)
            {
                HandleRightClick(state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, hoverX, hoverY, hoverIndex, opts);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && hasHover)
            {
                if (e.control || e.command || state.brushMode == ShapeGridBrushMode.Select)
                {
                    OpenObjectPicker(gridProperty, hoverIndex, GetStateKey(gridProperty), cellsProp.GetArrayElementAtIndex(hoverIndex), opts.allowSceneObjects);
                    e.Use();
                    return;
                }

                if (opts.enableRectangleFill && e.shift && state.brushMode != ShapeGridBrushMode.Pick)
                {
                    state.isRectSelecting = true;
                    state.rectStart = new Vector2Int(hoverX, hoverY);
                    state.rectEnd = state.rectStart;
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    e.Use();
                    return;
                }

                RecordUndo(gridProperty.serializedObject, "Paint Shape Grid Cell");
                state.isPainting = true;
                state.lastPaintedIndex = -1;
                ApplyBrushToCell(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, hoverX, hoverY, hoverIndex, opts.enableTriggeredState);
                gridProperty.serializedObject.ApplyModifiedProperties();
                state.lastPaintedIndex = hoverIndex;
                GUI.changed = true;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (state.isRectSelecting)
                {
                    if (hasHover)
                        state.rectEnd = new Vector2Int(hoverX, hoverY);
                    e.Use();
                    return;
                }

                if (state.isPainting && hasHover && hoverIndex != state.lastPaintedIndex)
                {
                    RecordUndo(gridProperty.serializedObject, "Paint Shape Grid Cell");
                    ApplyBrushToCell(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, hoverX, hoverY, hoverIndex, opts.enableTriggeredState);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                    state.lastPaintedIndex = hoverIndex;
                    GUI.changed = true;
                    e.Use();
                    return;
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (state.isRectSelecting)
                {
                    RecordUndo(gridProperty.serializedObject, "Rectangle Fill Shape Grid");
                    ApplyBrushToRect(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, opts.enableTriggeredState);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                    state.isRectSelecting = false;
                    state.lastPaintedIndex = -1;
                    GUIUtility.hotControl = 0;
                    GUI.changed = true;
                    e.Use();
                    return;
                }

                if (state.isPainting)
                {
                    state.isPainting = false;
                    state.lastPaintedIndex = -1;
                    e.Use();
                }
            }
        }

        static void HandleDragAndDrop(
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool hasHover,
            int hoverX,
            int hoverY,
            int hoverIndex,
            Type objType,
            ShapeGridOptionsAttribute opts)
        {
            Event e = Event.current;
            if (!hasHover || e == null || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform))
                return;

            List<UnityEngine.Object> accepted = GetAcceptedDraggedObjects(objType, opts.allowSceneObjects);
            if (accepted.Count == 0)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                RecordUndo(gridProperty.serializedObject, "Drop Objects Into Shape Grid");

                int placed = 0;
                if (opts.enableMultiObjectDragDrop)
                {
                    for (int i = 0; i < accepted.Count; i++)
                    {
                        int idx = hoverIndex + i;
                        if (idx >= cellsProp.arraySize)
                            break;

                        int x = idx % cols;
                        int y = idx / cols;
                        PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, idx, accepted[i]);
                        placed++;
                    }
                }
                else
                {
                    PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, hoverX, hoverY, hoverIndex, accepted[0]);
                    placed = 1;
                }

                if (placed > 0)
                {
                    state.activeBrushObject = accepted[0];
                    state.brushMode = ShapeGridBrushMode.PaintPrefab;
                    gridProperty.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                }
            }

            e.Use();
        }

        static void HandleRightClick(
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            int x,
            int y,
            int index,
            ShapeGridOptionsAttribute opts)
        {
            Event e = Event.current;

            if (opts.shiftRightClickAlwaysClears && e != null && e.shift)
            {
                RecordUndo(gridProperty.serializedObject, "Clear Shape Grid Cell");
                ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                gridProperty.serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
                return;
            }

            switch (opts.rightClickMode)
            {
                case ShapeGridRightClickMode.Clear:
                    RecordUndo(gridProperty.serializedObject, "Clear Shape Grid Cell");
                    ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                    break;

                case ShapeGridRightClickMode.ToggleTriggered:
                    RecordUndo(gridProperty.serializedObject, "Toggle Shape Grid Blocked Cell");
                    ToggleBlockedCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, opts.enableTriggeredState);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                    break;

                case ShapeGridRightClickMode.ContextMenu:
                    ShowCellContextMenu(state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, x, y, index, opts.enableTriggeredState, opts.enableTextCopyPaste);
                    break;

                case ShapeGridRightClickMode.Disabled:
                    break;
            }
        }

        static void ApplyBrushToCell(
            DrawerState state,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y,
            int index,
            bool enableTriggered)
        {
            switch (state.brushMode)
            {
                case ShapeGridBrushMode.PaintPrefab:
                    if (state.activeBrushObject != null)
                        PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, state.activeBrushObject);
                    break;

                case ShapeGridBrushMode.Block:
                    if (enableTriggered)
                        BlockCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                    break;

                case ShapeGridBrushMode.Erase:
                    ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                    break;

                case ShapeGridBrushMode.Start:
                    MarkSpecialCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, true);
                    break;

                case ShapeGridBrushMode.End:
                    MarkSpecialCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, false);
                    break;

                case ShapeGridBrushMode.Pick:
                    PickBrushFromCell(state, cellsProp, statesProp, index);
                    break;
            }
        }

        static void ApplyBrushToRect(
            DrawerState state,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            int minX = Mathf.Clamp(Mathf.Min(state.rectStart.x, state.rectEnd.x), 0, cols - 1);
            int maxX = Mathf.Clamp(Mathf.Max(state.rectStart.x, state.rectEnd.x), 0, cols - 1);
            int minY = Mathf.Clamp(Mathf.Min(state.rectStart.y, state.rectEnd.y), 0, rows - 1);
            int maxY = Mathf.Clamp(Mathf.Max(state.rectStart.y, state.rectEnd.y), 0, rows - 1);

            // Start/End are unique, so rectangle mode places them at the final cell only.
            if (state.brushMode == ShapeGridBrushMode.Start || state.brushMode == ShapeGridBrushMode.End)
            {
                int x = Mathf.Clamp(state.rectEnd.x, 0, cols - 1);
                int y = Mathf.Clamp(state.rectEnd.y, 0, rows - 1);
                int index = y * cols + x;
                ApplyBrushToCell(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, enableTriggered);
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * cols + x;
                    ApplyBrushToCell(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, enableTriggered);
                }
            }
        }

        static void PaintPrefabCell(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y,
            int index,
            UnityEngine.Object obj)
        {
            if (index < 0 || index >= cellsProp.arraySize || obj == null)
                return;

            cellsProp.GetArrayElementAtIndex(index).objectReferenceValue = obj;
            statesProp.GetArrayElementAtIndex(index).enumValueIndex = (int)ShapeGrid.CellState.Prefab;
        }

        static void BlockCell(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y,
            int index)
        {
            if (index < 0 || index >= cellsProp.arraySize)
                return;

            cellsProp.GetArrayElementAtIndex(index).objectReferenceValue = null;
            statesProp.GetArrayElementAtIndex(index).enumValueIndex = (int)ShapeGrid.CellState.Triggered;
            ClearSpecialIfMatches(hasStartProp, startCellProp, hasEndProp, endCellProp, x, y);
        }

        static void ToggleBlockedCell(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y,
            int index,
            bool enableTriggered)
        {
            if (index < 0 || index >= cellsProp.arraySize)
                return;

            SerializedProperty objProp = cellsProp.GetArrayElementAtIndex(index);
            SerializedProperty stateProp = statesProp.GetArrayElementAtIndex(index);

            if (objProp.objectReferenceValue != null || !enableTriggered)
            {
                ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                return;
            }

            bool isBlocked = stateProp.enumValueIndex == (int)ShapeGrid.CellState.Triggered;
            if (isBlocked)
            {
                stateProp.enumValueIndex = (int)ShapeGrid.CellState.Empty;
            }
            else
            {
                stateProp.enumValueIndex = (int)ShapeGrid.CellState.Triggered;
                ClearSpecialIfMatches(hasStartProp, startCellProp, hasEndProp, endCellProp, x, y);
            }
        }

        static void ClearCellProps(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y,
            int index)
        {
            if (index < 0 || index >= cellsProp.arraySize)
                return;

            cellsProp.GetArrayElementAtIndex(index).objectReferenceValue = null;
            statesProp.GetArrayElementAtIndex(index).enumValueIndex = (int)ShapeGrid.CellState.Empty;
            ClearSpecialIfMatches(hasStartProp, startCellProp, hasEndProp, endCellProp, x, y);
        }

        static void MarkSpecialCell(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y,
            int index,
            bool start)
        {
            if (index < 0 || index >= cellsProp.arraySize)
                return;

            Vector2Int v = new Vector2Int(x, y);

            if (start)
            {
                hasStartProp.boolValue = true;
                startCellProp.vector2IntValue = v;
                if (hasEndProp.boolValue && endCellProp.vector2IntValue == v)
                    hasEndProp.boolValue = false;
            }
            else
            {
                hasEndProp.boolValue = true;
                endCellProp.vector2IntValue = v;
                if (hasStartProp.boolValue && startCellProp.vector2IntValue == v)
                    hasStartProp.boolValue = false;
            }

            if (cellsProp.GetArrayElementAtIndex(index).objectReferenceValue == null &&
                statesProp.GetArrayElementAtIndex(index).enumValueIndex == (int)ShapeGrid.CellState.Triggered)
            {
                statesProp.GetArrayElementAtIndex(index).enumValueIndex = (int)ShapeGrid.CellState.Empty;
            }
        }

        static void PickBrushFromCell(DrawerState state, SerializedProperty cellsProp, SerializedProperty statesProp, int index)
        {
            if (index < 0 || index >= cellsProp.arraySize)
                return;

            UnityEngine.Object obj = cellsProp.GetArrayElementAtIndex(index).objectReferenceValue;
            if (obj != null)
            {
                state.activeBrushObject = obj;
                state.brushMode = ShapeGridBrushMode.PaintPrefab;
                return;
            }

            ShapeGrid.CellState cellState = (ShapeGrid.CellState)statesProp.GetArrayElementAtIndex(index).enumValueIndex;
            state.brushMode = cellState == ShapeGrid.CellState.Triggered ? ShapeGridBrushMode.Block : ShapeGridBrushMode.Erase;
        }

        static void DrawCells(
            DrawerState state,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            float startX,
            float startY,
            float cellSize,
            float spacing,
            bool useMiniIcon,
            Type objType)
        {
            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                float cy = startY + r * (cellSize + spacing);
                for (int c = 0; c < cols; c++)
                {
                    Rect cellRect = new Rect(startX + c * (cellSize + spacing), cy, cellSize, cellSize);

                    SerializedProperty cellObjProp = cellsProp.GetArrayElementAtIndex(index);
                    SerializedProperty cellStateProp = statesProp.GetArrayElementAtIndex(index);

                    UnityEngine.Object obj = cellObjProp.objectReferenceValue;
                    ShapeGrid.CellState cellState = (ShapeGrid.CellState)cellStateProp.enumValueIndex;
                    if (obj != null) cellState = ShapeGrid.CellState.Prefab;
                    if (obj == null && cellState == ShapeGrid.CellState.Prefab) cellState = ShapeGrid.CellState.Empty;

                    Vector2Int cell = new Vector2Int(c, r);
                    bool isStart = hasStartProp.boolValue && startCellProp.vector2IntValue == cell;
                    bool isEnd = hasEndProp.boolValue && endCellProp.vector2IntValue == cell;
                    bool hovered = state.hoveredCell == cell;

                    DrawCellBox(cellRect, hovered, cellState, isStart, isEnd);

                    if (obj != null)
                    {
                        if (useMiniIcon)
                            DrawMiniObjectIcon(cellRect, obj, objType);
                        else
                            DrawPreview(cellRect, obj);
                    }

                    index++;
                }
            }
        }

        static void DrawCellBox(Rect rect, bool hovered, ShapeGrid.CellState state, bool isStart, bool isEnd)
        {
            bool pro = EditorGUIUtility.isProSkin;

            Color emptyFill = pro ? new Color(0.14f, 0.14f, 0.14f, 1f) : new Color(0.98f, 0.98f, 0.98f, 1f);
            Color filledFill = pro ? new Color(0.17f, 0.17f, 0.17f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f);
            Color border = pro ? new Color(0f, 0f, 0f, 0.55f) : new Color(0f, 0f, 0f, 0.20f);

            bool hasPrefab = state == ShapeGrid.CellState.Prefab;
            Handles.DrawSolidRectangleWithOutline(rect, hasPrefab ? filledFill : emptyFill, border);

            if (state == ShapeGrid.CellState.Triggered)
            {
                Color overlay = pro ? new Color(1f, 0.25f, 0.25f, 0.10f) : new Color(0.75f, 0.10f, 0.10f, 0.10f);
                EditorGUI.DrawRect(rect, overlay);
                DrawBadge(rect, "X", pro ? new Color(1f, 0.35f, 0.35f, 0.90f) : new Color(0.75f, 0.15f, 0.15f, 0.85f));
            }

            if (hovered)
            {
                Color hover = pro ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.05f);
                EditorGUI.DrawRect(rect, hover);
            }

            if (isStart)
            {
                Color outline = pro ? new Color(0.20f, 1f, 0.45f, 0.95f) : new Color(0.10f, 0.70f, 0.25f, 0.95f);
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, outline);
                DrawBadge(rect, "S", outline);
            }

            if (isEnd)
            {
                Color outline = pro ? new Color(1f, 0.75f, 0.20f, 0.95f) : new Color(0.85f, 0.55f, 0.05f, 0.95f);
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, outline);
                DrawBadge(rect, "E", outline);
            }
        }

        static void DrawRectSelectionPreview(DrawerState state, Rect gridRect, int cols, int rows, float startX, float startY, float cellSize, float spacing)
        {
            if (!state.isRectSelecting)
                return;

            int minX = Mathf.Clamp(Mathf.Min(state.rectStart.x, state.rectEnd.x), 0, cols - 1);
            int maxX = Mathf.Clamp(Mathf.Max(state.rectStart.x, state.rectEnd.x), 0, cols - 1);
            int minY = Mathf.Clamp(Mathf.Min(state.rectStart.y, state.rectEnd.y), 0, rows - 1);
            int maxY = Mathf.Clamp(Mathf.Max(state.rectStart.y, state.rectEnd.y), 0, rows - 1);

            Rect rect = new Rect(
                startX + minX * (cellSize + spacing),
                startY + minY * (cellSize + spacing),
                (maxX - minX + 1) * cellSize + (maxX - minX) * spacing,
                (maxY - minY + 1) * cellSize + (maxY - minY) * spacing
            );

            rect = ClipRect(rect, gridRect);

            Color fill = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.6f, 1f, 0.12f) : new Color(0.1f, 0.35f, 1f, 0.10f);
            Color outline = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.75f, 1f, 0.95f) : new Color(0.1f, 0.35f, 1f, 0.90f);
            EditorGUI.DrawRect(rect, fill);
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, outline);
        }

        static Rect ClipRect(Rect rect, Rect bounds)
        {
            float xMin = Mathf.Max(rect.xMin, bounds.xMin);
            float yMin = Mathf.Max(rect.yMin, bounds.yMin);
            float xMax = Mathf.Min(rect.xMax, bounds.xMax);
            float yMax = Mathf.Min(rect.yMax, bounds.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static void DrawBadge(Rect rect, string text, Color bg)
        {
            if (s_badgeStyle == null)
            {
                s_badgeStyle = new GUIStyle(EditorStyles.boldLabel);
                s_badgeStyle.alignment = TextAnchor.MiddleCenter;
                s_badgeStyle.fontSize = 10;
            }

            Rect badge = new Rect(rect.x + 3f, rect.y + 3f, Mathf.Min(16f, rect.width - 6f), Mathf.Min(16f, rect.height - 6f));
            if (badge.width <= 6f || badge.height <= 6f)
                return;

            EditorGUI.DrawRect(badge, bg);

            Color prev = GUI.color;
            GUI.color = Color.white;
            GUI.Label(badge, text, s_badgeStyle);
            GUI.color = prev;
        }

        static void DrawPreview(Rect rect, UnityEngine.Object obj)
        {
            Rect inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            Texture tex = GetCachedPreview(obj);
            if (tex != null)
                GUI.DrawTexture(inner, tex, ScaleMode.ScaleToFit, true);
        }

        static void DrawMiniObjectIcon(Rect cellRect, UnityEngine.Object obj, Type objType)
        {
            if (obj == null) return;

            GUIContent content = EditorGUIUtility.ObjectContent(obj, objType);
            Texture icon = content.image != null ? content.image : GetCachedPreview(obj);
            if (icon == null) return;

            Rect r = new Rect(cellRect.x + 4f, cellRect.y + 4f, cellRect.width - 8f, cellRect.height - 8f);
            GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit, true);
        }

        static Texture GetCachedPreview(UnityEngine.Object obj)
        {
            if (obj == null) return null;

            int id = obj.GetInstanceID();
            Texture tex;
            if (s_previewCache.TryGetValue(id, out tex) && tex != null)
                return tex;

            tex = AssetPreview.GetAssetPreview(obj);
            if (tex == null)
                tex = AssetPreview.GetMiniThumbnail(obj);

            if (tex != null)
            {
                if (s_previewCache.Count > PreviewCacheLimit)
                    s_previewCache.Clear();

                s_previewCache[id] = tex;
            }

            return tex;
        }

        static void ShowCellContextMenu(
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            int x,
            int y,
            int index,
            bool enableTriggered,
            bool enableTextCopyPaste)
        {
            GenericMenu menu = new GenericMenu();
            Vector2Int v = new Vector2Int(x, y);
            bool isStart = hasStartProp.boolValue && startCellProp.vector2IntValue == v;
            bool isEnd = hasEndProp.boolValue && endCellProp.vector2IntValue == v;
            bool hasPrefab = cellsProp.GetArrayElementAtIndex(index).objectReferenceValue != null;
            bool isBlocked = !hasPrefab && statesProp.GetArrayElementAtIndex(index).enumValueIndex == (int)ShapeGrid.CellState.Triggered;

            menu.AddItem(new GUIContent("Cell/Mark as Start"), isStart, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Mark Shape Grid Start");
                if (isStart) hasStartProp.boolValue = false;
                else MarkSpecialCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, true);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Cell/Mark as End"), isEnd, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Mark Shape Grid End");
                if (isEnd) hasEndProp.boolValue = false;
                else MarkSpecialCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, false);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("Cell/");

            menu.AddItem(new GUIContent("Cell/Pick Into Brush"), false, delegate
            {
                PickBrushFromCell(state, cellsProp, statesProp, index);
            });

            if (state.activeBrushObject != null)
            {
                menu.AddItem(new GUIContent("Cell/Paint Active Prefab"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Paint Shape Grid Cell");
                    PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, state.activeBrushObject);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Cell/Paint Active Prefab"));
            }

            if (enableTriggered)
            {
                menu.AddItem(new GUIContent("Cell/Block"), isBlocked, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Block Shape Grid Cell");
                    BlockCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });

                menu.AddItem(new GUIContent("Cell/Toggle Blocked"), isBlocked, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Toggle Shape Grid Cell");
                    ToggleBlockedCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, true);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Cell/Block"));
                menu.AddDisabledItem(new GUIContent("Cell/Toggle Blocked"));
            }

            menu.AddItem(new GUIContent("Cell/Clear"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Clear Shape Grid Cell");
                ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("");

            AddRowColumnMenu(menu, "Row", y, true, state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered);
            AddRowColumnMenu(menu, "Column", x, false, state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered);

            menu.AddSeparator("");

            AddGridMenu(menu, state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered, enableTextCopyPaste);

            menu.ShowAsContext();
        }

        static void ShowGridContextMenu(
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            GenericMenu menu = new GenericMenu();
            AddGridMenu(menu, state, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, enableTriggered, true);
            menu.ShowAsContext();
        }

        static void AddRowColumnMenu(
            GenericMenu menu,
            string label,
            int rowOrColumn,
            bool isRow,
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            string prefix = label + "/";

            if (state.activeBrushObject != null)
            {
                menu.AddItem(new GUIContent(prefix + "Fill Active Prefab"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Fill Shape Grid " + label);
                    ForEachRowColumn(rowOrColumn, isRow, cols, rows, delegate (int x, int y, int i)
                    {
                        PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i, state.activeBrushObject);
                    });
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Fill Active Prefab"));
            }

            if (enableTriggered)
            {
                menu.AddItem(new GUIContent(prefix + "Block"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Block Shape Grid " + label);
                    ForEachRowColumn(rowOrColumn, isRow, cols, rows, delegate (int x, int y, int i)
                    {
                        BlockCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i);
                    });
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Block"));
            }

            menu.AddItem(new GUIContent(prefix + "Clear"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Clear Shape Grid " + label);
                ForEachRowColumn(rowOrColumn, isRow, cols, rows, delegate (int x, int y, int i)
                {
                    ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i);
                });
                gridProperty.serializedObject.ApplyModifiedProperties();
            });
        }

        static void AddGridMenu(
            GenericMenu menu,
            DrawerState state,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered,
            bool enableTextCopyPaste)
        {
            if (state.activeBrushObject != null)
            {
                menu.AddItem(new GUIContent("Grid/Fill All With Active Prefab"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Fill Shape Grid");
                    for (int y = 0; y < rows; y++)
                    {
                        for (int x = 0; x < cols; x++)
                        {
                            int i = y * cols + x;
                            PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i, state.activeBrushObject);
                        }
                    }
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Grid/Fill All With Active Prefab"));
            }

            if (enableTriggered)
            {
                menu.AddItem(new GUIContent("Grid/Block Border"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Block Shape Grid Border");
                    FillBorder(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, true);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });

                menu.AddItem(new GUIContent("Grid/Checkerboard Blocked"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Checkerboard Shape Grid");
                    CheckerboardBlocked(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, true);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });

                menu.AddItem(new GUIContent("Grid/Invert Blocked"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Invert Shape Grid Blocks");
                    InvertBlocked(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, true);
                    gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Grid/Block Border"));
                menu.AddDisabledItem(new GUIContent("Grid/Checkerboard Blocked"));
                menu.AddDisabledItem(new GUIContent("Grid/Invert Blocked"));
            }

            menu.AddItem(new GUIContent("Grid/Clear Inner"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Clear Shape Grid Inner Cells");
                ClearInner(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Grid/Clear All"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Clear Shape Grid");
                ClearGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("Grid/");

            menu.AddItem(new GUIContent("Grid/Mirror Horizontal"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Mirror Shape Grid Horizontally");
                MirrorHorizontal(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Grid/Mirror Vertical"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Mirror Shape Grid Vertically");
                MirrorVertical(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Grid/Shift Up"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Shift Shape Grid Up");
                ShiftGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, 0, -1);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Grid/Shift Down"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Shift Shape Grid Down");
                ShiftGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, 0, 1);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Grid/Shift Left"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Shift Shape Grid Left");
                ShiftGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, -1, 0);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Grid/Shift Right"), false, delegate
            {
                RecordUndo(gridProperty.serializedObject, "Shift Shape Grid Right");
                ShiftGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows, 1, 0);
                gridProperty.serializedObject.ApplyModifiedProperties();
            });

            if (enableTextCopyPaste)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Clipboard/Copy Layout Text"), false, delegate
                {
                    EditorGUIUtility.systemCopyBuffer = CopyGridAsText(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, cols, rows);
                });

                menu.AddItem(new GUIContent("Clipboard/Paste Layout Text"), false, delegate
                {
                    RecordUndo(gridProperty.serializedObject, "Paste Shape Grid Text");
                    int newCols;
                    int newRows;
                    if (TryPasteGridFromText(EditorGUIUtility.systemCopyBuffer, state.activeBrushObject, cols, rows, gridProperty, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, out newCols, out newRows))
                        gridProperty.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        static void FillAllWithBrush(
            DrawerState state,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    ApplyBrushToCell(state, cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i, enableTriggered);
                }
            }
        }

        static void FillBorder(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            if (!enableTriggered)
                return;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    bool border = x == 0 || y == 0 || x == cols - 1 || y == rows - 1;
                    if (!border) continue;
                    int i = y * cols + x;
                    BlockCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i);
                }
            }
        }

        static void ClearInner(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows)
        {
            for (int y = 1; y < rows - 1; y++)
            {
                for (int x = 1; x < cols - 1; x++)
                {
                    int i = y * cols + x;
                    ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i);
                }
            }
        }

        static void CheckerboardBlocked(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            if (!enableTriggered) return;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (((x + y) & 1) == 0)
                        BlockCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i);
                    else
                        ClearCellProps(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, i);
                }
            }
        }

        static void InvertBlocked(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            bool enableTriggered)
        {
            if (!enableTriggered) return;

            int n = Mathf.Min(cellsProp.arraySize, statesProp.arraySize);
            for (int i = 0; i < n; i++)
            {
                if (cellsProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    continue;

                SerializedProperty st = statesProp.GetArrayElementAtIndex(i);
                st.enumValueIndex = st.enumValueIndex == (int)ShapeGrid.CellState.Triggered
                    ? (int)ShapeGrid.CellState.Empty
                    : (int)ShapeGrid.CellState.Triggered;
            }

            ClearSpecialIfNowBlocked(cellsProp, statesProp, hasStartProp, startCellProp, cols, rows);
            ClearSpecialIfNowBlocked(cellsProp, statesProp, hasEndProp, endCellProp, cols, rows);
        }

        static void ClearSpecialIfNowBlocked(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasProp,
            SerializedProperty cellProp,
            int cols,
            int rows)
        {
            if (!hasProp.boolValue) return;

            Vector2Int v = cellProp.vector2IntValue;
            if (v.x < 0 || v.y < 0 || v.x >= cols || v.y >= rows)
            {
                hasProp.boolValue = false;
                return;
            }

            int index = v.y * cols + v.x;
            if (index < 0 || index >= cellsProp.arraySize || index >= statesProp.arraySize)
            {
                hasProp.boolValue = false;
                return;
            }

            bool blocked = cellsProp.GetArrayElementAtIndex(index).objectReferenceValue == null &&
                           statesProp.GetArrayElementAtIndex(index).enumValueIndex == (int)ShapeGrid.CellState.Triggered;

            if (blocked)
                hasProp.boolValue = false;
        }

        static void MirrorHorizontal(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows)
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols / 2; x++)
                {
                    int a = y * cols + x;
                    int b = y * cols + (cols - 1 - x);
                    SwapCells(cellsProp, statesProp, a, b);
                }
            }

            if (hasStartProp.boolValue)
            {
                Vector2Int s = startCellProp.vector2IntValue;
                startCellProp.vector2IntValue = new Vector2Int(cols - 1 - s.x, s.y);
            }

            if (hasEndProp.boolValue)
            {
                Vector2Int e = endCellProp.vector2IntValue;
                endCellProp.vector2IntValue = new Vector2Int(cols - 1 - e.x, e.y);
            }
        }

        static void MirrorVertical(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows)
        {
            for (int y = 0; y < rows / 2; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int a = y * cols + x;
                    int b = (rows - 1 - y) * cols + x;
                    SwapCells(cellsProp, statesProp, a, b);
                }
            }

            if (hasStartProp.boolValue)
            {
                Vector2Int s = startCellProp.vector2IntValue;
                startCellProp.vector2IntValue = new Vector2Int(s.x, rows - 1 - s.y);
            }

            if (hasEndProp.boolValue)
            {
                Vector2Int e = endCellProp.vector2IntValue;
                endCellProp.vector2IntValue = new Vector2Int(e.x, rows - 1 - e.y);
            }
        }

        static void ShiftGrid(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows,
            int dx,
            int dy)
        {
            UnityEngine.Object[] oldObjects = ReadObjects(cellsProp);
            int[] oldStates = ReadStates(statesProp);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int dst = y * cols + x;
                    int sx = x - dx;
                    int sy = y - dy;

                    if (sx < 0 || sx >= cols || sy < 0 || sy >= rows)
                    {
                        cellsProp.GetArrayElementAtIndex(dst).objectReferenceValue = null;
                        statesProp.GetArrayElementAtIndex(dst).enumValueIndex = (int)ShapeGrid.CellState.Empty;
                    }
                    else
                    {
                        int src = sy * cols + sx;
                        cellsProp.GetArrayElementAtIndex(dst).objectReferenceValue = oldObjects[src];
                        statesProp.GetArrayElementAtIndex(dst).enumValueIndex = oldStates[src];
                    }
                }
            }

            ShiftSpecialCell(hasStartProp, startCellProp, cols, rows, dx, dy);
            ShiftSpecialCell(hasEndProp, endCellProp, cols, rows, dx, dy);
        }

        static void ShiftSpecialCell(SerializedProperty hasProp, SerializedProperty cellProp, int cols, int rows, int dx, int dy)
        {
            if (!hasProp.boolValue) return;

            Vector2Int v = cellProp.vector2IntValue;
            v.x += dx;
            v.y += dy;

            if (v.x < 0 || v.x >= cols || v.y < 0 || v.y >= rows)
            {
                hasProp.boolValue = false;
                cellProp.vector2IntValue = Vector2Int.zero;
            }
            else
            {
                cellProp.vector2IntValue = v;
            }
        }

        static void SwapCells(SerializedProperty cellsProp, SerializedProperty statesProp, int a, int b)
        {
            UnityEngine.Object objA = cellsProp.GetArrayElementAtIndex(a).objectReferenceValue;
            UnityEngine.Object objB = cellsProp.GetArrayElementAtIndex(b).objectReferenceValue;
            int stateA = statesProp.GetArrayElementAtIndex(a).enumValueIndex;
            int stateB = statesProp.GetArrayElementAtIndex(b).enumValueIndex;

            cellsProp.GetArrayElementAtIndex(a).objectReferenceValue = objB;
            cellsProp.GetArrayElementAtIndex(b).objectReferenceValue = objA;
            statesProp.GetArrayElementAtIndex(a).enumValueIndex = stateB;
            statesProp.GetArrayElementAtIndex(b).enumValueIndex = stateA;
        }

        static UnityEngine.Object[] ReadObjects(SerializedProperty cellsProp)
        {
            UnityEngine.Object[] values = new UnityEngine.Object[cellsProp.arraySize];
            for (int i = 0; i < values.Length; i++)
                values[i] = cellsProp.GetArrayElementAtIndex(i).objectReferenceValue;
            return values;
        }

        static int[] ReadStates(SerializedProperty statesProp)
        {
            int[] values = new int[statesProp.arraySize];
            for (int i = 0; i < values.Length; i++)
                values[i] = statesProp.GetArrayElementAtIndex(i).enumValueIndex;
            return values;
        }

        delegate void CellAction(int x, int y, int index);

        static void ForEachRowColumn(int rowOrColumn, bool isRow, int cols, int rows, CellAction action)
        {
            if (action == null) return;

            if (isRow)
            {
                int y = Mathf.Clamp(rowOrColumn, 0, rows - 1);
                for (int x = 0; x < cols; x++)
                    action(x, y, y * cols + x);
            }
            else
            {
                int x = Mathf.Clamp(rowOrColumn, 0, cols - 1);
                for (int y = 0; y < rows; y++)
                    action(x, y, y * cols + x);
            }
        }

        static void ClearGrid(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp)
        {
            EnsureSizes(cellsProp, statesProp, Mathf.Max(cellsProp.arraySize, statesProp.arraySize));

            int n = Mathf.Min(cellsProp.arraySize, statesProp.arraySize);
            for (int i = 0; i < n; i++)
            {
                cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                statesProp.GetArrayElementAtIndex(i).enumValueIndex = (int)ShapeGrid.CellState.Empty;
            }

            hasStartProp.boolValue = false;
            hasEndProp.boolValue = false;
            startCellProp.vector2IntValue = Vector2Int.zero;
            endCellProp.vector2IntValue = Vector2Int.zero;
        }

        static void CountCells(SerializedProperty cellsProp, SerializedProperty statesProp, out int filled, out int blocked, out int empty)
        {
            filled = 0;
            blocked = 0;
            empty = 0;

            int n = Mathf.Min(cellsProp.arraySize, statesProp.arraySize);
            for (int i = 0; i < n; i++)
            {
                UnityEngine.Object obj = cellsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj != null)
                {
                    filled++;
                    continue;
                }

                int st = statesProp.GetArrayElementAtIndex(i).enumValueIndex;
                if (st == (int)ShapeGrid.CellState.Triggered)
                    blocked++;
                else
                    empty++;
            }
        }

        static string FormatStartEnd(SerializedProperty hasStartProp, SerializedProperty startCellProp, SerializedProperty hasEndProp, SerializedProperty endCellProp)
        {
            string text = string.Empty;
            if (hasStartProp.boolValue)
            {
                Vector2Int s = startCellProp.vector2IntValue;
                text += string.Format("Start ({0},{1})", s.x, s.y);
            }

            if (hasEndProp.boolValue)
            {
                Vector2Int e = endCellProp.vector2IntValue;
                if (text.Length > 0) text += " | ";
                text += string.Format("End ({0},{1})", e.x, e.y);
            }

            return text;
        }

        static string CopyGridAsText(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format("MOST_SHAPE_GRID {0} {1}", cols, rows));

            Vector2Int start = startCellProp.vector2IntValue;
            Vector2Int end = endCellProp.vector2IntValue;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    char ch = '.';

                    if (hasStartProp.boolValue && start.x == x && start.y == y)
                        ch = 'S';
                    else if (hasEndProp.boolValue && end.x == x && end.y == y)
                        ch = 'E';
                    else if (cellsProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                        ch = 'P';
                    else if (statesProp.GetArrayElementAtIndex(i).enumValueIndex == (int)ShapeGrid.CellState.Triggered)
                        ch = '#';

                    sb.Append(ch);
                }
                sb.AppendLine();
            }

            sb.AppendLine("// . empty | # blocked | P active prefab on paste | S start | E end");
            return sb.ToString();
        }

        static bool TryPasteGridFromText(
            string text,
            UnityEngine.Object activeBrushObject,
            int currentCols,
            int currentRows,
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            out int newCols,
            out int newRows)
        {
            newCols = currentCols;
            newRows = currentRows;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.Replace("\r", "");
            string[] rawLines = normalized.Split('\n');
            List<string> rows = new List<string>();

            for (int i = 0; i < rawLines.Length; i++)
            {
                string line = rawLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("//")) continue;

                if (line.StartsWith("MOST_SHAPE_GRID", StringComparison.OrdinalIgnoreCase))
                    continue;

                System.Text.StringBuilder cleaned = new System.Text.StringBuilder();
                for (int c = 0; c < line.Length; c++)
                {
                    char ch = char.ToUpperInvariant(line[c]);
                    if (ch == '.' || ch == '_' || ch == '-' || ch == '0' || ch == '#'
                        || ch == 'X' || ch == 'B' || ch == 'P' || ch == 'S' || ch == 'E')
                    {
                        cleaned.Append(ch);
                    }
                }

                if (cleaned.Length > 0)
                    rows.Add(cleaned.ToString());
            }

            if (rows.Count == 0)
                return false;

            newRows = rows.Count;
            newCols = 1;
            for (int i = 0; i < rows.Count; i++)
                newCols = Mathf.Max(newCols, rows[i].Length);

            SerializedProperty colsProp = gridProperty.FindPropertyRelative("columns");
            SerializedProperty rowsProp = gridProperty.FindPropertyRelative("rows");
            if (colsProp != null) colsProp.intValue = newCols;
            if (rowsProp != null) rowsProp.intValue = newRows;

            ResizePreserveByXY(cellsProp, statesProp, currentCols, currentRows, newCols, newRows);
            ClearGrid(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp);
            EnsureSizes(cellsProp, statesProp, newCols * newRows);

            for (int y = 0; y < newRows; y++)
            {
                string line = rows[y];
                for (int x = 0; x < newCols; x++)
                {
                    char ch = x < line.Length ? char.ToUpperInvariant(line[x]) : '.';
                    int index = y * newCols + x;

                    switch (ch)
                    {
                        case '#':
                        case 'X':
                        case 'B':
                            BlockCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index);
                            break;

                        case 'P':
                            if (activeBrushObject != null)
                                PaintPrefabCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, activeBrushObject);
                            break;

                        case 'S':
                            MarkSpecialCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, true);
                            break;

                        case 'E':
                            MarkSpecialCell(cellsProp, statesProp, hasStartProp, startCellProp, hasEndProp, endCellProp, x, y, index, false);
                            break;
                    }
                }
            }

            return true;
        }

        static void OpenObjectPicker(SerializedProperty gridProperty, int cellIndex, string stateKey, SerializedProperty cellObjProp, bool allowSceneObjects)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            s_pickerControlId = controlId;
            s_pickerPropertyPath = gridProperty.propertyPath;
            s_pickerCellIndex = cellIndex;
            s_pickerStateKey = stateKey;

            EditorGUIUtility.ShowObjectPicker<UnityEngine.Object>(
                cellObjProp.objectReferenceValue,
                allowSceneObjects,
                string.Empty,
                controlId
            );
        }

        static void HandleObjectPickerEvent(
            SerializedProperty gridProperty,
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            Type objType,
            string stateKey)
        {
            Event e = Event.current;
            if (e == null) return;

            if (e.type != EventType.ExecuteCommand)
                return;

            if (e.commandName != "ObjectSelectorUpdated" && e.commandName != "ObjectSelectorClosed")
                return;

            int pickerId = EditorGUIUtility.GetObjectPickerControlID();
            if (pickerId != s_pickerControlId) return;
            if (gridProperty.propertyPath != s_pickerPropertyPath) return;
            if (s_pickerCellIndex < 0 || s_pickerCellIndex >= cellsProp.arraySize) return;

            UnityEngine.Object picked = EditorGUIUtility.GetObjectPickerObject();
            UnityEngine.Object finalObj = NormalizeObjectForType(picked, objType);

            cellsProp.GetArrayElementAtIndex(s_pickerCellIndex).objectReferenceValue = finalObj;
            statesProp.GetArrayElementAtIndex(s_pickerCellIndex).enumValueIndex = finalObj != null
                ? (int)ShapeGrid.CellState.Prefab
                : (int)ShapeGrid.CellState.Empty;

            DrawerState state;
            if (!string.IsNullOrEmpty(s_pickerStateKey) && s_stateByKey.TryGetValue(s_pickerStateKey, out state) && finalObj != null)
            {
                state.activeBrushObject = finalObj;
                state.brushMode = ShapeGridBrushMode.PaintPrefab;
            }

            gridProperty.serializedObject.ApplyModifiedProperties();

            if (e.commandName == "ObjectSelectorClosed")
            {
                s_pickerControlId = -1;
                s_pickerPropertyPath = null;
                s_pickerCellIndex = -1;
                s_pickerStateKey = null;
            }

            e.Use();
        }

        static UnityEngine.Object ValidateObjectForBrush(UnityEngine.Object obj, Type objType, bool allowSceneObjects)
        {
            if (obj == null) return null;
            if (!allowSceneObjects && !EditorUtility.IsPersistent(obj)) return null;
            return NormalizeObjectForType(obj, objType);
        }

        static UnityEngine.Object NormalizeObjectForType(UnityEngine.Object obj, Type objType)
        {
            if (obj == null) return null;
            if (objType == null) return obj;

            if (objType.IsAssignableFrom(obj.GetType()))
                return obj;

            if (objType == typeof(GameObject) && obj is Component)
                return ((Component)obj).gameObject;

            return null;
        }

        static List<UnityEngine.Object> GetAcceptedDraggedObjects(Type objType, bool allowSceneObjects)
        {
            List<UnityEngine.Object> accepted = new List<UnityEngine.Object>();
            UnityEngine.Object[] refs = DragAndDrop.objectReferences;
            if (refs == null) return accepted;

            for (int i = 0; i < refs.Length; i++)
            {
                UnityEngine.Object r = refs[i];
                if (r == null) continue;
                if (!allowSceneObjects && !EditorUtility.IsPersistent(r)) continue;

                UnityEngine.Object normalized = NormalizeObjectForType(r, objType);
                if (normalized != null && !accepted.Contains(normalized))
                    accepted.Add(normalized);
            }

            return accepted;
        }

        static bool TryGetCellAtMouse(Vector2 mouse, float startX, float startY, int cols, int rows, float cellSize, float spacing, out int x, out int y, out int index)
        {
            x = -1;
            y = -1;
            index = -1;

            float step = cellSize + spacing;
            if (step <= 0f) return false;

            float localX = mouse.x - startX;
            float localY = mouse.y - startY;

            if (localX < 0f || localY < 0f)
                return false;

            int cx = Mathf.FloorToInt(localX / step);
            int cy = Mathf.FloorToInt(localY / step);

            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows)
                return false;

            float insideX = localX - cx * step;
            float insideY = localY - cy * step;

            if (insideX > cellSize || insideY > cellSize)
                return false;

            x = cx;
            y = cy;
            index = y * cols + x;
            return true;
        }

        static void ClearSpecialIfMatches(
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int x,
            int y)
        {
            Vector2Int v = new Vector2Int(x, y);
            if (hasStartProp.boolValue && startCellProp.vector2IntValue == v)
                hasStartProp.boolValue = false;
            if (hasEndProp.boolValue && endCellProp.vector2IntValue == v)
                hasEndProp.boolValue = false;
        }

        static void SanitizeStartEnd(
            SerializedProperty hasStartProp,
            SerializedProperty startCellProp,
            SerializedProperty hasEndProp,
            SerializedProperty endCellProp,
            int cols,
            int rows)
        {
            if (hasStartProp.boolValue)
            {
                Vector2Int s = startCellProp.vector2IntValue;
                if (s.x < 0 || s.y < 0 || s.x >= cols || s.y >= rows)
                    hasStartProp.boolValue = false;
            }

            if (hasEndProp.boolValue)
            {
                Vector2Int e = endCellProp.vector2IntValue;
                if (e.x < 0 || e.y < 0 || e.x >= cols || e.y >= rows)
                    hasEndProp.boolValue = false;
            }

            if (hasStartProp.boolValue && hasEndProp.boolValue && startCellProp.vector2IntValue == endCellProp.vector2IntValue)
                hasEndProp.boolValue = false;
        }

        static void EnsureSizes(SerializedProperty cellsProp, SerializedProperty statesProp, int needed)
        {
            needed = Mathf.Max(1, needed);

            if (cellsProp.arraySize != needed) cellsProp.arraySize = needed;
            if (statesProp.arraySize != needed) statesProp.arraySize = needed;

            for (int i = 0; i < statesProp.arraySize; i++)
            {
                SerializedProperty st = statesProp.GetArrayElementAtIndex(i);
                if (st.enumValueIndex < 0 || st.enumValueIndex > (int)ShapeGrid.CellState.Prefab)
                    st.enumValueIndex = (int)ShapeGrid.CellState.Empty;
            }
        }

        static void SanitizeStates(SerializedProperty cellsProp, SerializedProperty statesProp)
        {
            int n = Mathf.Min(cellsProp.arraySize, statesProp.arraySize);
            for (int i = 0; i < n; i++)
            {
                UnityEngine.Object obj = cellsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                SerializedProperty st = statesProp.GetArrayElementAtIndex(i);

                if (obj != null && st.enumValueIndex != (int)ShapeGrid.CellState.Prefab)
                    st.enumValueIndex = (int)ShapeGrid.CellState.Prefab;

                if (obj == null && st.enumValueIndex == (int)ShapeGrid.CellState.Prefab)
                    st.enumValueIndex = (int)ShapeGrid.CellState.Empty;
            }
        }

        static void ResizePreserveByXY(
            SerializedProperty cellsProp,
            SerializedProperty statesProp,
            int oldCols,
            int oldRows,
            int newCols,
            int newRows)
        {
            oldCols = Mathf.Max(1, oldCols);
            oldRows = Mathf.Max(1, oldRows);
            newCols = Mathf.Max(1, newCols);
            newRows = Mathf.Max(1, newRows);

            int oldCount = oldCols * oldRows;
            int newCount = newCols * newRows;

            UnityEngine.Object[] oldCells = new UnityEngine.Object[Mathf.Max(oldCount, cellsProp.arraySize)];
            int[] oldStates = new int[Mathf.Max(oldCount, statesProp.arraySize)];

            for (int i = 0; i < Mathf.Min(oldCells.Length, cellsProp.arraySize); i++)
                oldCells[i] = cellsProp.GetArrayElementAtIndex(i).objectReferenceValue;

            for (int i = 0; i < Mathf.Min(oldStates.Length, statesProp.arraySize); i++)
                oldStates[i] = statesProp.GetArrayElementAtIndex(i).enumValueIndex;

            cellsProp.arraySize = newCount;
            statesProp.arraySize = newCount;

            for (int i = 0; i < newCount; i++)
            {
                cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                statesProp.GetArrayElementAtIndex(i).enumValueIndex = (int)ShapeGrid.CellState.Empty;
            }

            int copyCols = Mathf.Min(oldCols, newCols);
            int copyRows = Mathf.Min(oldRows, newRows);

            for (int y = 0; y < copyRows; y++)
            {
                for (int x = 0; x < copyCols; x++)
                {
                    int oldIndex = y * oldCols + x;
                    int newIndex = y * newCols + x;

                    if (oldIndex >= 0 && oldIndex < oldCells.Length && newIndex >= 0 && newIndex < newCount)
                    {
                        UnityEngine.Object obj = oldCells[oldIndex];
                        int st = oldIndex < oldStates.Length ? oldStates[oldIndex] : (int)ShapeGrid.CellState.Empty;

                        cellsProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = obj;
                        statesProp.GetArrayElementAtIndex(newIndex).enumValueIndex = st;

                        if (obj != null)
                            statesProp.GetArrayElementAtIndex(newIndex).enumValueIndex = (int)ShapeGrid.CellState.Prefab;
                        else if (st == (int)ShapeGrid.CellState.Prefab)
                            statesProp.GetArrayElementAtIndex(newIndex).enumValueIndex = (int)ShapeGrid.CellState.Empty;
                    }
                }
            }
        }

        static int MiniIntField(Rect rect, string shortLabel, int value)
        {
            const float labelW = 14f;
            const float pad = 2f;

            Rect lRect = new Rect(rect.x, rect.y, labelW, rect.height);
            Rect fRect = new Rect(rect.x + labelW + pad, rect.y, rect.width - labelW - pad, rect.height);

            GUI.Label(lRect, shortLabel, EditorStyles.miniLabel);
            return EditorGUI.DelayedIntField(fRect, value);
        }

        static void RecordUndo(SerializedObject serializedObject, string name)
        {
            if (serializedObject == null || serializedObject.targetObjects == null)
                return;

            Undo.RecordObjects(serializedObject.targetObjects, name);
        }

        ShapeGridOptionsAttribute GetOptionsOrDefault()
        {
            ShapeGridOptionsAttribute attr = fieldInfo != null ? fieldInfo.GetCustomAttribute<ShapeGridOptionsAttribute>(true) : null;
            if (attr != null) return attr;

            return new ShapeGridOptionsAttribute(typeof(GameObject), false, 72f, 16f, 4f)
            {
                enableTriggeredState = true,
                rightClickMode = ShapeGridRightClickMode.ToggleTriggered,
                shiftRightClickAlwaysClears = true,
                enableBrushToolbar = true,
                enableKeyboardShortcuts = true,
                enableRectangleFill = true,
                enablePatternTools = true,
                enableTextCopyPaste = true,
                enableMultiObjectDragDrop = true
            };
        }

        static DrawerState GetState(SerializedProperty property)
        {
            string key = GetStateKey(property);
            DrawerState state;
            if (!s_stateByKey.TryGetValue(key, out state) || state == null)
            {
                state = new DrawerState();
                s_stateByKey[key] = state;
            }
            return state;
        }

        static string GetStateKey(SerializedProperty property)
        {
            if (property == null || property.serializedObject == null || property.serializedObject.targetObject == null)
                return "null";

            return property.serializedObject.targetObject.GetInstanceID() + "|" + property.propertyPath;
        }
    }
#endif

    #endregion
}