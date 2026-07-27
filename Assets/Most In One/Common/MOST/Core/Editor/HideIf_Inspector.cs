#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;

[CanEditMultipleObjects]
[CustomEditor(typeof(MonoBehaviour), true)]
public sealed class SmartInspector_MB : SmartInspector_HideIfAA_AndScript { }

[CanEditMultipleObjects]
// Do NOT make this fallback. If it is fallback, Unity can skip it for ScriptableObjects
// whenever another non-fallback SO editor exists. MonoBehaviour was already non-fallback,
// so this makes ScriptableObject behave the same way.
[CustomEditor(typeof(ScriptableObject), true)]
public sealed class SmartInspector_SO : SmartInspector_HideIfAA_AndScript { }

public abstract class SmartInspector_HideIfAA_AndScript : Editor
{
    //static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // if true → hide the Script row when ANY selected object has [HideScriptField]
    // if false → require ALL selected to have it
    static readonly bool HIDE_SCRIPT_IF_ANY_SELECTED = false;

    public override void OnInspectorGUI()
    {
        var inspectedType = targets[0].GetType();

        bool usesHideIf = TypeHasHideIfAA(inspectedType);
        bool hideScript = ShouldHideScriptForSelection(targets);

        if (!usesHideIf && !hideScript)
        {
            base.OnInspectorGUI();
            return;
        }

        serializedObject.Update();

        var it = serializedObject.GetIterator();
        bool enterChildren = true;

        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;

            // 1) Hide the Script row when requested
            if (hideScript && it.propertyPath == "m_Script")
                continue;

            // 2) Skip fields hidden by HideIfAny/HideIfAll (now includes MOSTEdit.RuntimeOnly support)
            if (usesHideIf && FieldHiddenByHideIfAA(it))
                continue;

            // 3) If a list/array and ALL children would be hidden → hide the foldout/header as well
            if (usesHideIf && IsArrayButNotString(it) && AllArrayChildrenHidden(it))
                continue;

            EditorGUILayout.PropertyField(it, includeChildren: true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ---------- External bridge for custom inspectors ----------
    // MOST_ActionEditor is a custom editor, so the global SmartInspector cannot draw
    // the [SerializeReference] action elements by itself. Call this method from that
    // editor instead of EditorGUILayout.PropertyField(...). It keeps Unity's normal
    // PropertyField UI, but applies the same HideIf/list-header skip checks.
    public static bool ShouldHidePropertyForSmartInspector(SerializedProperty prop)
    {
        if (prop == null) return false;

        if (FieldHiddenByHideIfAA(prop))
            return true;

        if (IsArrayButNotString(prop) && AllArrayChildrenHidden(prop))
            return true;

        return false;
    }

    public static void DrawPropertyForSmartInspector(SerializedProperty prop, bool includeChildren = true)
    {
        if (prop == null) return;
        if (ShouldHidePropertyForSmartInspector(prop)) return;

        EditorGUILayout.PropertyField(prop, includeChildren);
    }

    // ---------- [HideScriptField] ----------
    static bool ShouldHideScriptForSelection(UnityEngine.Object[] objs)
    {
        if (objs == null || objs.Length == 0) return false;

        bool HasAttr(Type x) =>
            Attribute.IsDefined(x, typeof(HideScriptFieldAttribute), true) ||
            x.GetCustomAttributes(true).Any(a => a.GetType().Name == "HideScriptFeildAttribute"); // legacy typo tolerance

        return HIDE_SCRIPT_IF_ANY_SELECTED
            ? objs.Any(o => HasAttr(o.GetType()))
            : objs.All(o => HasAttr(o.GetType()));
    }

    // ---------- Type scan to decide if we need HideIf processing ----------
    static bool TypeHasHideIfAA(Type t)
    {
        return TypeHasHideIfAARecursive(t, new HashSet<Type>(), 0);
    }

    static bool TypeHasHideIfAARecursive(Type t, HashSet<Type> visited, int depth)
    {
        if (t == null) return false;
        if (depth > 8) return false;
        if (t == typeof(string) || t.IsPrimitive || t.IsEnum) return false;
        if (!visited.Add(t)) return false;

        foreach (var f in GetUnitySerializableFields(t))
        {
            if (f.GetCustomAttributes(typeof(HideIfAnyAttribute), true).Length > 0 ||
                f.GetCustomAttributes(typeof(HideIfAllAttribute), true).Length > 0)
                return true;
        }

        foreach (var f in GetUnitySerializableFields(t))
        {
            var ft = f.FieldType;
            if (IsList(ft)) ft = GetElementType(ft);
            if (ft == null) continue;

            // Object references are drawn as references, not inline child fields.
            // Do not scan every referenced ScriptableObject/Texture/etc from here.
            if (typeof(UnityEngine.Object).IsAssignableFrom(ft))
                continue;

            if (TypeHasHideIfAARecursive(ft, visited, depth + 1))
                return true;
        }

        return false;
    }

    static IEnumerable<FieldInfo> GetUnitySerializableFields(Type t)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        for (Type current = t; current != null; current = current.BaseType)
        {
            foreach (var f in current.GetFields(flags))
            {
                if (f.IsStatic) continue;
                if (f.IsNotSerialized) continue;

                bool unitySerializes =
                    f.IsPublic ||
                    f.GetCustomAttribute<SerializeField>() != null;

#if UNITY_2019_3_OR_NEWER
                unitySerializes = unitySerializes || f.GetCustomAttribute<SerializeReference>() != null;
#endif

                if (unitySerializes)
                    yield return f;
            }
        }
    }

    // ---------- Per-property evaluation (now honors MOSTEdit.RuntimeOnly) ----------
    static bool FieldHiddenByHideIfAA(SerializedProperty prop)
    {
        var fi = GetFieldInfoFromProperty(prop, out _);
        if (fi == null) return false;

        var any = (HideIfAnyAttribute)fi.GetCustomAttributes(typeof(HideIfAnyAttribute), true).FirstOrDefault();
        var all = (HideIfAllAttribute)fi.GetCustomAttributes(typeof(HideIfAllAttribute), true).FirstOrDefault();

        // MOSTEdit.RuntimeOnly: hide during Play mode even with NO triplets
        if (IsRuntimeOnly(any) || IsRuntimeOnly(all))
            return true;

        // If triplets exist, keep your ALL-first, then ANY logic
        if (all != null && all.Triplets != null && all.Triplets.Length > 0 && EvaluateTriplets(prop, all.Triplets, true))
            return true;

        if (any != null && any.Triplets != null && any.Triplets.Length > 0 && EvaluateTriplets(prop, any.Triplets, false))
            return true;

        return false;

        // local helper
        static bool IsRuntimeOnly(object attr)
        {
#if UNITY_EDITOR
            if (attr is HideIfAnyAttribute a && a.EditState == MOSTEdit.RuntimeOnly && !EditorApplication.isPlaying)
                return true;
            if (attr is HideIfAllAttribute b && b.EditState == MOSTEdit.RuntimeOnly && !EditorApplication.isPlaying)
                return true;
#endif
            return false;
        }
    }

    // (field, value, hideWhen) * N
    static bool EvaluateTriplets(SerializedProperty ctx, object[] triplets, bool requireAll)
    {
        if (triplets == null || triplets.Length == 0) return false;
        if (triplets.Length % 3 != 0)
        {
            Debug.LogError("[HideIfAny/All] Expected triplets: (field, value, hideWhen)*N");
            return false;
        }

        bool anyHit = false;

        for (int i = 0; i < triplets.Length; i += 3)
        {
            string name = triplets[i] as string;
            object compare = triplets[i + 1];
            bool hideWhen = triplets[i + 2] is bool b && b;
            if (string.IsNullOrEmpty(name)) continue;

            var cond = Resolve(ctx, name);
            if (cond == null)
            {
                if (requireAll) return false; // ALL cannot be satisfied if a rule can't be evaluated
                continue;                     // ANY skips unknown rule
            }

            bool hit = (compare is bool)
                ? (ReadBool(cond) == hideWhen)
                : (((ReadInt(cond) == ToInt(compare)) == hideWhen));

            if (requireAll) { if (!hit) return false; }
            else { if (hit) { anyHit = true; break; } }
        }

        return requireAll ? true : anyHit;
    }

    // ---------- arrays/lists helpers ----------
    static bool IsArrayButNotString(SerializedProperty p) => p.isArray && p.propertyType != SerializedPropertyType.String;

    static bool AllArrayChildrenHidden(SerializedProperty arrayProp)
    {
        // Keep Unity's normal list UI. This only decides whether the whole array/list
        // should be skipped, and it ignores Unity's internal Array.size row.
        var copy = arrayProp.Copy();
        var end = copy.GetEndProperty();
        int directDepth = arrayProp.depth + 1;
        bool enterChildren = true;
        bool sawRealElement = false;

        while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
        {
            enterChildren = false;

            if (copy.depth != directDepth)
                continue;

            if (IsArraySizeProperty(copy))
                continue;

            sawRealElement = true;

            if (FieldOrDescendantVisible(copy))
                return false;
        }

        // Empty arrays/lists should stay visible so the user can edit the Size field.
        return sawRealElement;
    }

    static bool FieldOrDescendantVisible(SerializedProperty prop)
    {
        if (prop == null || IsArraySizeProperty(prop))
            return false;

        bool isArrayElementContainer = IsArrayElementProperty(prop) && prop.hasVisibleChildren;

        // A normal field with no HideIf is visible.
        // Array/list elements are just containers, so inspect their children instead.
        if (!isArrayElementContainer && !FieldHiddenByHideIfAA(prop))
            return true;

        // If the property itself is explicitly hidden, the whole subtree is hidden.
        if (FieldHiddenByHideIfAA(prop))
            return false;

        var copy = prop.Copy();
        var end = copy.GetEndProperty();
        bool enterChildren = true;

        while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
        {
            enterChildren = false;

            if (IsArraySizeProperty(copy))
                continue;

            if (FieldOrDescendantVisible(copy))
                return true;
        }

        return false;
    }

    static bool IsArraySizeProperty(SerializedProperty p) =>
        p != null && p.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal);

    static bool IsArrayElementProperty(SerializedProperty p) =>
        p != null && p.propertyPath.Contains(".Array.data[");

    // ---------- resolve & read ----------
    static SerializedProperty Resolve(SerializedProperty ctx, string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath)) return null;
        var so = ctx.serializedObject;

        var p = so.FindProperty(nameOrPath);
        if (p != null) return p;

        string basePath = GetBase(ctx.propertyPath);
        if (!string.IsNullOrEmpty(basePath))
        {
            p = so.FindProperty($"{basePath}.{nameOrPath}");
            if (p != null) return p;
        }

        string path = basePath;
        while (!string.IsNullOrEmpty(path))
        {
            int lastDot = path.LastIndexOf('.');
            if (lastDot <= 0) { path = string.Empty; break; }
            path = path[..lastDot];
            p = so.FindProperty($"{path}.{nameOrPath}");
            if (p != null) return p;
        }

        return so.FindProperty(nameOrPath);
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

    static FieldInfo GetFieldInfoFromProperty(SerializedProperty prop, out Type fieldType)
    {
        fieldType = null;

        if (prop == null || prop.serializedObject == null || prop.serializedObject.targetObject == null)
            return null;

        var so = prop.serializedObject;
        Type currentType = so.targetObject.GetType();
        FieldInfo fi = null;

        string[] parts = prop.propertyPath.Split('.');
        string runningPath = string.Empty;

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            runningPath = string.IsNullOrEmpty(runningPath)
                ? part
                : runningPath + "." + part;

            // Unity list/array path: field.Array.data[index]
            if (part == "Array")
                continue;

            if (part.StartsWith("data[", StringComparison.Ordinal))
            {
                currentType = GetElementType(currentType);
                if (currentType == null)
                    return null;

                // Critical for [SerializeReference] List<MOST_ActionCore>:
                // the declared element type is MOST_ActionCore, but the real fields
                // live on MOSTAction_HapticFeedback / MOSTAction_AudioSource / etc.
                var elementProp = so.FindProperty(runningPath);
                Type managedType = GetManagedReferenceRuntimeType(elementProp);
                if (managedType != null)
                    currentType = managedType;

                continue;
            }

            fi = FindFieldInHierarchy(currentType, part);
            if (fi == null)
                return null;

            currentType = fi.FieldType;

            // Also support a direct [SerializeReference] field, not only list elements.
            var currentProp = so.FindProperty(runningPath);
            Type directManagedType = GetManagedReferenceRuntimeType(currentProp);
            if (directManagedType != null)
                currentType = directManagedType;
        }

        fieldType = currentType;
        return fi;
    }

    static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
    {
        if (type == null || string.IsNullOrEmpty(fieldName))
            return null;

        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        for (Type t = type; t != null; t = t.BaseType)
        {
            var fi = t.GetField(fieldName, flags);
            if (fi != null)
                return fi;
        }

        return null;
    }

    static Type GetManagedReferenceRuntimeType(SerializedProperty prop)
    {
#if UNITY_2019_3_OR_NEWER
        if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference)
            return null;

        string fullTypeName = prop.managedReferenceFullTypename;
        if (string.IsNullOrEmpty(fullTypeName))
            return null;

        // Unity format is usually: "AssemblyName Full.Namespace.TypeName"
        int split = fullTypeName.IndexOf(' ');
        if (split > 0 && split < fullTypeName.Length - 1)
        {
            string assemblyName = fullTypeName.Substring(0, split);
            string typeName = fullTypeName.Substring(split + 1);

            var type = Type.GetType(typeName + ", " + assemblyName);
            if (type != null)
                return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null)
                    return type;
            }
        }
#endif
        return null;
    }

    static bool IsList(Type t) =>
        t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>));

    static Type GetElementType(Type t)
    {
        if (t.IsArray) return t.GetElementType();
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            return t.GetGenericArguments()[0];
        return t;
    }
}
#endif
