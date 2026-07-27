#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Solo.MOST_IN_ONE
{
    [CustomEditor(typeof(MOST_Database))]
    public class MOST_DatabaseEditor : Editor
    {
        SerializedProperty _entriesProp;
        readonly List<bool> _foldouts = new();
        static readonly Dictionary<int, bool> _persist = new();

        void OnEnable()
        {
            _entriesProp = serializedObject.FindProperty("entries");
            Undo.undoRedoPerformed += OnUndoRedo;
            RebuildFoldouts();
        }
        void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
        void OnUndoRedo() { RebuildFoldouts(); Repaint(); }

        void RebuildFoldouts()
        {
            var next = new List<bool>(_foldouts.Count);
            int count = _entriesProp != null ? _entriesProp.arraySize : 0;
            for (int i = 0; i < count; i++)
                next.Add(i < _foldouts.Count ? _foldouts[i] : GetPersist(i));
            _foldouts.Clear();
            _foldouts.AddRange(next);
        }
        bool GetPersist(int idx) { int key = MakeKey(idx); if (!_persist.ContainsKey(key)) _persist[key] = false; return _persist[key]; }
        void SetPersist(int idx, bool state) => _persist[MakeKey(idx)] = state;
        int MakeKey(int idx) => (target.GetInstanceID() * 397) ^ idx;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var it = serializedObject.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.name == "m_Script" || it.name == "entries") continue;
                    EditorGUILayout.PropertyField(it, true);
                }
            }

            EditorGUILayout.Space(6);
            DrawSeparator(0.25f);
            EditorGUILayout.Space(6);

            while (_foldouts.Count < _entriesProp.arraySize) _foldouts.Add(GetPersist(_foldouts.Count));
            while (_foldouts.Count > _entriesProp.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                var element = _entriesProp.GetArrayElementAtIndex(i);

                // Header
                Rect header = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.DrawRect(header, new Color(0.17f, 0.17f, 0.17f));

                // ▶/▼
                Rect arrow = new(header.x + 4, header.y, 18, header.height);
                if (GUI.Button(arrow, _foldouts[i] ? "▼" : "▶", EditorStyles.label))
                {
                    _foldouts[i] = !_foldouts[i];
                    SetPersist(i, _foldouts[i]);
                }

                // Label
                string typeName = GetManagedTypeName(element);
                var nameProp = element.FindPropertyRelative("DataName");
                string label = !string.IsNullOrWhiteSpace(nameProp?.stringValue)
                    ? nameProp.stringValue
                    : ObjectNames.NicifyVariableName(typeName);

                Rect labelRect = new(arrow.xMax + 6, header.y, header.width - 140, header.height);
                if (GUI.Button(labelRect, label, EditorStyles.boldLabel))
                {
                    _foldouts[i] = !_foldouts[i];
                    SetPersist(i, _foldouts[i]);
                }

                // Move buttons
                const float bw = 20f;
                if (GUI.Button(new Rect(header.xMax - (bw * 3), header.y, bw, header.height), "↑")) MoveItem(i, i - 1);
                if (GUI.Button(new Rect(header.xMax - (bw * 2), header.y, bw, header.height), "↓")) MoveItem(i, i + 1);

                // ⋮ Remove
                Rect menuRect = new(header.xMax - bw, header.y, bw, header.height);
                if (GUI.Button(menuRect, "⋮", EditorStyles.miniButton))
                {
                    int idx = i;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Remove"), false, () =>
                    {
                        _entriesProp.DeleteArrayElementAtIndex(idx);
                        serializedObject.ApplyModifiedProperties();
                        RebuildFoldouts();
                        EditorUtility.SetDirty(target);
                        ((MOST_Database)target).SaveSchema();
                    });
                    menu.ShowAsContext();
                }

                if (_foldouts[i])
                {
                    EditorGUILayout.Space(2);
                    DrawSeparator(0.40f);
                    EditorGUILayout.Space(4);

                    EditorGUILayout.HelpBox("Type: " + typeName, MessageType.None);
                    EditorGUI.indentLevel++;

                    if (nameProp != null) EditorGUILayout.PropertyField(nameProp);

                    var idProp = element.FindPropertyRelative("id");
                    using (new EditorGUI.DisabledScope(true)) { if (idProp != null) EditorGUILayout.PropertyField(idProp); }

                    //var inspectProp = element.FindPropertyRelative("AllowInspectorEdit");
                    //if (inspectProp != null) EditorGUILayout.PropertyField(inspectProp);

                    if (typeName != nameof(BoolData))
                    {
                        EditorGUILayout.Space(2);
                        DrawSeparator(0.25f);
                        EditorGUILayout.Space(2);
                    }

                    if (typeName == nameof(IntData))
                        DrawIntCustom(element);
                    else if (typeName == nameof(FloatData))
                        DrawFloatCustom(element);
                    else
                        DrawRemaining(element); // BoolData / StringData fallback

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2);
                    DrawSeparator(0.15f);
                    EditorGUILayout.Space(2);
                }
            }

            if (_entriesProp.arraySize == 0)
                EditorGUILayout.HelpBox("No Data. Click “Add Data”.", MessageType.Warning);

            if (GUILayout.Button("Add Data", GUILayout.Height(22)))
                ShowAddEntryMenu();

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Top section is read-only in Play Mode. Entries remain editable.", MessageType.None);
            }
        }

        void MoveItem(int from, int to)
        {
            if (to < 0 || to >= _entriesProp.arraySize) return;

            Undo.RecordObject(target, "Reorder Entry");
            _entriesProp.MoveArrayElement(from, to);
            serializedObject.ApplyModifiedProperties();

            var f = _foldouts[from];
            _foldouts.RemoveAt(from);
            _foldouts.Insert(to, f);
            SetPersist(from, _foldouts[from]);
            SetPersist(to, _foldouts[to]);

            EditorUtility.SetDirty(target);
        }

        void ShowAddEntryMenu()
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<MData>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name);

            bool any = false;
            foreach (var t in types)
            {
                any = true;
                var nice = ObjectNames.NicifyVariableName(t.Name);
                menu.AddItem(new GUIContent(nice), false, () => AddEntryOfType(t));
            }

            if (!any) menu.AddDisabledItem(new GUIContent("No derived MData types found"));
            menu.ShowAsContext();
        }

        void AddEntryOfType(Type type)
        {
            var db = (MOST_Database)target;
            Undo.RecordObject(db, "Add Entry");

            var instance = (MData)Activator.CreateInstance(type);
            instance.DataName = MakeUniqueName(ObjectNames.NicifyVariableName(type.Name), db);
            instance.id = NextFreeId(db);

            serializedObject.Update();
            _entriesProp.arraySize++;
            var elem = _entriesProp.GetArrayElementAtIndex(_entriesProp.arraySize - 1);
            elem.managedReferenceValue = instance; // This is a SerializeReference
            serializedObject.ApplyModifiedProperties();

            _foldouts.Add(true);
            SetPersist(_foldouts.Count - 1, true);

            EditorUtility.SetDirty(db);
            ((MOST_Database)target).SaveSchema();
        }

        static void DrawSeparator(float gray = 0.25f)
        {
            var c = new Color(gray, gray, gray);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), c);
        }

        void DrawRemaining(SerializedProperty root) =>
            DrawRemaining(root, new HashSet<string> { "DataName", "id", "AllowInspectorEdit" });

        void DrawRemaining(SerializedProperty root, HashSet<string> skip)
        {
            var it = root.Copy();
            var end = it.GetEndProperty();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(it, end)) break;
                if (skip != null && skip.Contains(it.name)) { enterChildren = false; continue; }
                EditorGUILayout.PropertyField(it, true);
                enterChildren = false;
            }
        }

        static Rect RowRect() =>
            EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

        static (Rect left, Rect right) Split2(Rect r, float gap = 8f)
        {
            float w = (r.width - gap) * 0.5f;
            return (new Rect(r.x, r.y, w, r.height),
                    new Rect(r.x + w + gap, r.y, w, r.height));
        }

        static (Rect toggle, Rect field) SplitToggleAndField(Rect r, float toggleWidth = 30f, float gap = 6f)
        {
            var t = new Rect(r.x, r.y, toggleWidth, r.height);
            var f = new Rect(t.xMax + gap, r.y, r.width - toggleWidth - gap, r.height);
            return (t, f);
        }

        static void LabeledInt(Rect cell, string label, SerializedProperty sp)
        {
            const float labelW = 64f;
            var lr = new Rect(cell.x, cell.y, labelW, cell.height);
            var fr = new Rect(lr.xMax + 4f, cell.y, cell.width - labelW - 4f, cell.height);
            EditorGUI.LabelField(lr, label);
            sp.intValue = EditorGUI.IntField(fr, GUIContent.none, sp.intValue);
        }
        static void LabeledFloat(Rect cell, string label, SerializedProperty sp)
        {
            const float labelW = 64f;
            var lr = new Rect(cell.x, cell.y, labelW, cell.height);
            var fr = new Rect(lr.xMax + 4f, cell.y, cell.width - labelW - 4f, cell.height);
            EditorGUI.LabelField(lr, label);
            sp.floatValue = EditorGUI.FloatField(fr, GUIContent.none, sp.floatValue);
        }

        void DrawIntCustom(SerializedProperty element)
        {
            var db = (MOST_Database)target;
            var valueProp = element.FindPropertyRelative("value");
            var prev = GUI.color;
            GUI.color = Color.cyan;

            EditorGUI.BeginChangeCheck();
            int newV = EditorGUILayout.IntField(new GUIContent("Value"), valueProp.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(db, "Edit Value");

                valueProp.intValue = newV;                 // persistent (serialized)
                serializedObject.ApplyModifiedProperties(); // commit to object

                if (element.managedReferenceValue is IntData data)
                    data.Value = newV;                     // triggers MarkValuesDirtyDelayed()

                EditorUtility.SetDirty(db);
                if (!Application.isPlaying)
                {
                    db.Save();
                    AssetDatabase.SaveAssets();
                }
            }
            GUI.color = prev;

            var pInitial = element.FindPropertyRelative("InitialValue");
            var pHighest = element.FindPropertyRelative("HighestValue");
            var pEnableMin = element.FindPropertyRelative("EnableMin");
            var pMinValue = element.FindPropertyRelative("MinValue");
            var pEnableMax = element.FindPropertyRelative("EnableMax");
            var pMaxValue = element.FindPropertyRelative("MaxValue");

            // Initial | Highest
            {
                var (L, R) = Split2(RowRect());
                LabeledInt(L, "Initial", pInitial);
                using (new EditorGUI.DisabledScope(true))
                    LabeledInt(R, "Highest", pHighest);
            }

            // EnableMin | Min
            {
                var (T, F) = SplitToggleAndField(RowRect());
                pEnableMin.boolValue = EditorGUI.Toggle(T, pEnableMin.boolValue);
                using (new EditorGUI.DisabledScope(!pEnableMin.boolValue))
                    LabeledInt(F, "Min", pMinValue);
            }

            // EnableMax | Max
            {
                var (T, F) = SplitToggleAndField(RowRect());
                pEnableMax.boolValue = EditorGUI.Toggle(T, pEnableMax.boolValue);
                using (new EditorGUI.DisabledScope(!pEnableMax.boolValue))
                    LabeledInt(F, "Max", pMaxValue);
            }

            DrawRemaining(element, new HashSet<string>{
                "DataName","id","Value", "value","InitialValue","HighestValue",
                "EnableMin","MinValue","EnableMax","MaxValue"
            });
        }

        void DrawFloatCustom(SerializedProperty element)
        {
            var db = (MOST_Database)target;
            var valueProp = element.FindPropertyRelative("value");
            var prev = GUI.color;
            GUI.color = Color.cyan;

            EditorGUI.BeginChangeCheck();
            float newV = EditorGUILayout.FloatField(new GUIContent("Value"), valueProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(db, "Edit Value");

                valueProp.floatValue = newV;                 // persistent (serialized)
                serializedObject.ApplyModifiedProperties(); // commit to object

                if (element.managedReferenceValue is FloatData data)
                    data.Value = newV;                     // triggers MarkValuesDirtyDelayed()

                EditorUtility.SetDirty(db);
                if (!Application.isPlaying)
                {
                    db.Save();
                    AssetDatabase.SaveAssets();
                }
            }
            GUI.color = prev;

            var pInitial = element.FindPropertyRelative("InitialValue");
            var pHighest = element.FindPropertyRelative("HighestValue");
            var pEnableMin = element.FindPropertyRelative("EnableMin");
            var pMinValue = element.FindPropertyRelative("MinValue");
            var pEnableMax = element.FindPropertyRelative("EnableMax");
            var pMaxValue = element.FindPropertyRelative("MaxValue");

            // Initial | Highest
            {
                var (L, R) = Split2(RowRect());
                LabeledFloat(L, "Initial", pInitial);
                using (new EditorGUI.DisabledScope(true))
                    LabeledFloat(R, "Highest", pHighest);
            }

            // EnableMin | Min
            {
                var (T, F) = SplitToggleAndField(RowRect());
                pEnableMin.boolValue = EditorGUI.Toggle(T, pEnableMin.boolValue);
                using (new EditorGUI.DisabledScope(!pEnableMin.boolValue))
                    LabeledFloat(F, "Min", pMinValue);
            }

            // EnableMax | Max
            {
                var (T, F) = SplitToggleAndField(RowRect());
                pEnableMax.boolValue = EditorGUI.Toggle(T, pEnableMax.boolValue);
                using (new EditorGUI.DisabledScope(!pEnableMax.boolValue))
                    LabeledFloat(F, "Max", pMaxValue);
            }

            DrawRemaining(element, new HashSet<string>{
                "DataName","id","value", "Value","InitialValue","HighestValue",
                "EnableMin","MinValue","EnableMax","MaxValue"
            });
        }

        static int NextFreeId(MOST_Database db)
        {
            var used = new HashSet<int>(db.Entries.Where(e => e != null).Select(e => e.id));
            int id = 0; while (used.Contains(id)) id++;
            return id;
        }

        string MakeUniqueName(string baseName, MOST_Database db)
        {
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "Value";
            var existing = new HashSet<string>(db.Entries.Select(e => e?.DataName ?? ""));
            string candidate = baseName; int i = 1;
            while (existing.Contains(candidate)) candidate = $"{baseName} {i++}";
            return candidate;
        }

        static string GetManagedTypeName(SerializedProperty element)
        {
            var full = element.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return "<Unknown>";
            int space = full.LastIndexOf(' ');
            var type = space >= 0 ? full.Substring(space + 1) : full;
            int dot = type.LastIndexOf('.');
            int plus = type.LastIndexOf('+');
            int cut = Math.Max(dot, plus);
            return cut >= 0 ? type.Substring(cut + 1) : type;
        }
    }
}
#endif
