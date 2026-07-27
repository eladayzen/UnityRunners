using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Solo.MOST_IN_ONE
{
    [CustomEditor(typeof(MOST_Action))]
    public class MOST_ActionEditor : Editor
    {
        SerializedProperty effectsProp;
        MOST_Action effectsTarget;
        List<bool> effectFoldouts = new();

        static readonly Dictionary<int, bool> _persistentFoldoutStates = new();

        const string CopyBufferHeader = "MOST_ACTION_COPY_v1";
        static Type _copiedActionType;
        static string _copiedActionJson;

        void OnEnable()
        {
            effectsTarget = target as MOST_Action;
            if (effectsTarget == null) return;

            effectsProp = serializedObject.FindProperty("actions");
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            RebuildFoldoutStates();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            RebuildFoldoutStates();
            Repaint();
        }

        void RebuildFoldoutStates()
        {
            var newFoldouts = new List<bool>();
            for (int i = 0; i < effectsTarget.Actions.Count; i++)
            {
                if (i < effectFoldouts.Count)
                    newFoldouts.Add(effectFoldouts[i]);
                else
                    newFoldouts.Add(GetPersistentFoldoutState(i));
            }
            effectFoldouts = newFoldouts;
        }

        bool GetPersistentFoldoutState(int index)
        {
            string key = $"{effectsTarget.GetInstanceID()}_{index}";
            if (!_persistentFoldoutStates.ContainsKey(key.GetHashCode()))
                _persistentFoldoutStates[key.GetHashCode()] = false;
            return _persistentFoldoutStates[key.GetHashCode()];
        }

        void SetPersistentFoldoutState(int index, bool state)
        {
            string key = $"{effectsTarget.GetInstanceID()}_{index}";
            _persistentFoldoutStates[key.GetHashCode()] = state;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (effectsTarget == null || effectsProp == null)
            {
                EditorGUILayout.HelpBox("Most_Action not available.", MessageType.Error);
                return;
            }

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterMainChildren = true;

            while (prop.NextVisible(enterMainChildren))
            {
                enterMainChildren = false;

                if (prop.name == "actions" || prop.name == "m_Script") continue;

                global::SmartInspector_HideIfAA_AndScript.DrawPropertyForSmartInspector(prop.Copy(), true);
            }
            EditorGUILayout.Space(5);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space(5);

            while (effectFoldouts.Count < effectsTarget.Actions.Count)
            {
                effectFoldouts.Add(GetPersistentFoldoutState(effectFoldouts.Count));
            }
            while (effectFoldouts.Count > effectsTarget.Actions.Count)
            {
                effectFoldouts.RemoveAt(effectFoldouts.Count - 1);
            }


            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                var element = effectsProp.GetArrayElementAtIndex(i);
                var effectObj = effectsTarget.Actions[i];
                if (effectObj == null) continue;

                Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                // Header row
                EditorGUI.DrawRect(headerRect, new Color(0.17f, 0.17f, 0.17f));

                // ▶/▼ (clickable for foldout)
                Rect dropdownRect = new(headerRect.x + 4, headerRect.y, 18, headerRect.height);
                if (GUI.Button(dropdownRect, effectFoldouts[i] ? "▼" : "▶", EditorStyles.label))
                {
                    effectFoldouts[i] = !effectFoldouts[i];
                    SetPersistentFoldoutState(i, effectFoldouts[i]);
                }

                // Enable toggle
                Rect toggleRect = new(dropdownRect.xMax + 4, headerRect.y, 18, headerRect.height);
                var enabledProp = element.FindPropertyRelative("Enabled");
                if (enabledProp != null && enabledProp.propertyType == SerializedPropertyType.Boolean)
                    enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
                else
                    effectObj.Enabled = EditorGUI.Toggle(toggleRect, effectObj.Enabled);

                // Name label (clickable for foldout)
                Rect labelRect = new(toggleRect.xMax + 4, headerRect.y, headerRect.width - 120, headerRect.height);
                if (GUI.Button(labelRect, effectObj.ActionName, EditorStyles.boldLabel))
                {
                    effectFoldouts[i] = !effectFoldouts[i];
                    SetPersistentFoldoutState(i, effectFoldouts[i]);
                }

                // Move ↑/↓
                float buttonWidth = 20;
                if (GUI.Button(new Rect(headerRect.xMax - (buttonWidth * 3), headerRect.y, buttonWidth, headerRect.height), "↑"))
                    MoveEffect(i, i - 1);
                if (GUI.Button(new Rect(headerRect.xMax - (buttonWidth * 2), headerRect.y, buttonWidth, headerRect.height), "↓"))
                    MoveEffect(i, i + 1);

                // ⋮ Action menu
                Rect menuRect = new(headerRect.xMax - buttonWidth, headerRect.y, buttonWidth, headerRect.height);
                GUIStyle dotStyle = new(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                if (GUI.Button(menuRect, "⋮", dotStyle))
                {
                    int actionIndex = i;
                    Type typeToEdit = effectObj.GetType();

                    GenericMenu menu = new();

                    menu.AddItem(new GUIContent("Copy"), false, () =>
                    {
                        if (actionIndex >= 0 && actionIndex < effectsTarget.Actions.Count)
                            CopyAction(effectsTarget.Actions[actionIndex]);
                    });

                    if (CanPasteCopiedActionInto(actionIndex))
                    {
                        menu.AddItem(new GUIContent("Paste"), false, () =>
                        {
                            PasteCopiedActionValuesAt(actionIndex);
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste"));
                    }

                    if (HasCopiedAction())
                    {
                        menu.AddItem(new GUIContent("Paste as New"), false, () =>
                        {
                            PasteCopiedActionAsNew();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste as New"));
                    }

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("Remove"), false, () =>
                    {
                        if (actionIndex >= 0 && actionIndex < effectsTarget.Actions.Count)
                        {
                            Undo.RecordObject(effectsTarget, "Remove Effect");
                            effectsTarget.RemoveEffectAt(actionIndex);
                            EditorUtility.SetDirty(effectsTarget);

                            if (actionIndex >= 0 && actionIndex < effectFoldouts.Count)
                                effectFoldouts.RemoveAt(actionIndex);

                            RebuildFoldoutStates();
                            SaveFoldoutStates();
                        }
                    });

                    menu.AddItem(new GUIContent("Edit Script"), false, () =>
                    {
                        OpenActionScript(typeToEdit);
                    });

                    menu.ShowAsContext();
                }

                if (effectFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    string typeName = effectObj.GetType().Name.Replace("MOSTAction_", string.Empty);
                    EditorGUILayout.HelpBox("Type: " + typeName, MessageType.None);

                    var iterator = element.Copy();
                    var endProperty = iterator.GetEndProperty();
                    bool enterChildren = true;

                    while (iterator.NextVisible(enterChildren))
                    {
                        if (SerializedProperty.EqualContents(iterator, endProperty))
                            break;

                        global::SmartInspector_HideIfAA_AndScript.DrawPropertyForSmartInspector(iterator.Copy(), true);

                        enterChildren = false;
                    }

                    if (Application.isPlaying)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(.32f, .32f, .32f));
                        EditorGUILayout.Space(2);

                        EditorGUILayout.HelpBox("Is Playing/Active is a debug for MOST_Action.IsPlaying\nLogically in case of instant or moment actions will always be equal false", MessageType.Info);
                        GUI.enabled = false;
                        EditorGUILayout.Toggle("Is Playing/Active", effectsTarget.Actions[i].IsPlaying);
                        GUI.enabled = true;
                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginHorizontal();
                        {
                            if (GUILayout.Button("Play", GUILayout.Height(20)))
                            {
                                effectsTarget.PlayAction(effectsTarget.Actions[i].ActionName);
                            }

                            if (GUILayout.Button("Stop", GUILayout.Height(20)))
                            {
                                effectsTarget.StopAction(effectsTarget.Actions[i].ActionName);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(5);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.black);
                EditorGUILayout.Space(2);
            }

            if (effectsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No Actions Added, Tap To Add.", MessageType.Warning);
            }
            // Add Effect Button
            if (GUILayout.Button("Add Effect", GUILayout.Height(20)))
            {
                GenericMenu menu = new();
                DrawType(menu, typeof(MOSTAction_HDRGlow), "HDR Glow");
                DrawType(menu, typeof(MOSTAction_GrayShift), "Gray Shift");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_CameraShake), "Camera Shake");
                DrawType(menu, typeof(MOSTAction_CameraFollow), "Camera Follow");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_AudioSource), "Audio Source");
                DrawType(menu, typeof(MOSTAction_HapticFeedback), "Haptic Feedback");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_Spawner), "Spawner");
                DrawType(menu, typeof(MOSTAction_Burst), "Burst");
                DrawType(menu, typeof(MOSTAction_Swap), "Object Swap");
                DrawType(menu, typeof(MOSTAction_RandomSelect), "Random Select");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_PositionAnimation), "Position Animation");
                DrawType(menu, typeof(MOSTAction_RotationAnimation), "Rotate Animation");
                DrawType(menu, typeof(MOSTAction_ScaleAnimation), "Scale Animation");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_Events), "Events");
                DrawType(menu, typeof(MOSTAction_Collision_Trigger), "Collision And Trigger");
                DrawType(menu, typeof(MOSTAction_Destroy), "Destroy");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_DisplayData), "Display Data");
                DrawType(menu, typeof(MOSTAction_UpdateData), "Update Data");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_SceneManage), "Scene Manage");
                menu.ShowAsContext();
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void CopyAction(MOST_ActionCore action)
        {
            if (action == null)
                return;

            _copiedActionType = action.GetType();
            _copiedActionJson = EditorJsonUtility.ToJson(action);

            EditorGUIUtility.systemCopyBuffer = string.Join(
                "\n",
                CopyBufferHeader,
                _copiedActionType.AssemblyQualifiedName,
                _copiedActionJson
            );
        }

        static bool HasCopiedAction()
        {
            return TryGetCopiedAction(out _, out _);
        }

        static bool TryGetCopiedAction(out Type actionType, out string actionJson)
        {
            actionType = _copiedActionType;
            actionJson = _copiedActionJson;

            if (actionType != null && !string.IsNullOrEmpty(actionJson))
                return true;

            string copyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(copyBuffer))
                return false;

            string[] parts = copyBuffer.Replace("\r\n", "\n").Split(new[] { '\n' }, 3);
            if (parts.Length != 3 || parts[0].Trim() != CopyBufferHeader)
                return false;

            actionType = Type.GetType(parts[1].Trim());
            actionJson = parts[2];

            if (actionType == null || string.IsNullOrEmpty(actionJson) || !typeof(MOST_ActionCore).IsAssignableFrom(actionType))
                return false;

            _copiedActionType = actionType;
            _copiedActionJson = actionJson;
            return true;
        }

        bool CanPasteCopiedActionInto(int actionIndex)
        {
            if (!TryGetCopiedAction(out Type copiedType, out _))
                return false;

            if (actionIndex < 0 || actionIndex >= effectsTarget.Actions.Count)
                return false;

            MOST_ActionCore targetAction = effectsTarget.Actions[actionIndex];
            return targetAction != null && targetAction.GetType() == copiedType;
        }

        void PasteCopiedActionValuesAt(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= effectsTarget.Actions.Count)
                return;

            if (!TryGetCopiedAction(out Type copiedType, out string copiedJson))
                return;

            MOST_ActionCore targetAction = effectsTarget.Actions[actionIndex];
            if (targetAction == null || targetAction.GetType() != copiedType)
            {
                Debug.LogWarning("Paste is only available when the copied action type matches the selected action type. Use Paste as New to create a separate copied action.");
                return;
            }

            try
            {
                Undo.RecordObject(effectsTarget, "Paste Effect Values");
                EditorJsonUtility.FromJsonOverwrite(copiedJson, targetAction);
                serializedObject.Update();
                EditorUtility.SetDirty(effectsTarget);
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not paste values for action type {copiedType?.FullName}: {exception.Message}");
            }
        }

        void PasteCopiedActionAsNew()
        {
            if (!TryGetCopiedAction(out Type copiedType, out string copiedJson))
                return;

            MOST_ActionCore pastedEffect;

            try
            {
                pastedEffect = (MOST_ActionCore)Activator.CreateInstance(copiedType);
                EditorJsonUtility.FromJsonOverwrite(copiedJson, pastedEffect);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not paste action of type {copiedType?.FullName}: {exception.Message}");
                return;
            }

            Undo.RecordObject(effectsTarget, "Paste Effect As New");
            effectsTarget.AddEffect(pastedEffect);
            EditorUtility.SetDirty(effectsTarget);

            effectFoldouts.Add(true);
            SaveFoldoutStates();
            serializedObject.Update();
            Repaint();
        }

        void SaveFoldoutStates()
        {
            for (int i = 0; i < effectFoldouts.Count; i++)
                SetPersistentFoldoutState(i, effectFoldouts[i]);
        }

        static void OpenActionScript(Type type)
        {
            if (type == null)
                return;

            MonoScript fallbackScript = null;
            int fallbackLine = 0;

            // First search by type name.
            if (TryOpenActionScript(type, AssetDatabase.FindAssets($"{type.Name} t:MonoScript"), ref fallbackScript, ref fallbackLine))
                return;

            // Fallback: search all scripts, useful if many action classes live in one file.
            if (TryOpenActionScript(type, AssetDatabase.FindAssets("t:MonoScript"), ref fallbackScript, ref fallbackLine))
                return;

            if (fallbackScript != null)
            {
                if (fallbackLine > 0)
                    AssetDatabase.OpenAsset(fallbackScript, fallbackLine);
                else
                    AssetDatabase.OpenAsset(fallbackScript);

                return;
            }

            Debug.LogWarning($"Could not find script asset for action type: {type.FullName}");
        }

        static bool TryOpenActionScript(Type type, string[] guids, ref MonoScript fallbackScript, ref int fallbackLine)
        {
            string typeName = type.Name;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null)
                    continue;

                if (script.GetClass() == type)
                {
                    OpenScriptAsset(script, path, typeName);
                    return true;
                }

                int line = FindTypeLine(path, typeName);
                bool fileNameMatches = string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(path),
                    typeName,
                    StringComparison.Ordinal
                );

                if (fallbackScript == null && (line > 0 || fileNameMatches))
                {
                    fallbackScript = script;
                    fallbackLine = line;
                }
            }

            return false;
        }

        static void OpenScriptAsset(MonoScript script, string assetPath, string typeName)
        {
            int line = FindTypeLine(assetPath, typeName);

            if (line > 0)
                AssetDatabase.OpenAsset(script, line);
            else
                AssetDatabase.OpenAsset(script);
        }

        static int FindTypeLine(string assetPath, string typeName)
        {
            try
            {
                string fullPath = System.IO.Path.GetFullPath(assetPath);
                string[] lines = System.IO.File.ReadAllLines(fullPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    if (line.Contains("class " + typeName) ||
                        line.Contains("struct " + typeName) ||
                        line.Contains("record " + typeName))
                    {
                        return i + 1;
                    }
                }
            }
            catch
            {
                // Ignore file read errors and fall back to opening the script asset.
            }

            return 0;
        }

        void MoveEffect(int fromIndex, int toIndex)
        {
            if (toIndex < 0 || toIndex >= effectsProp.arraySize) return;

            effectsProp.MoveArrayElement(fromIndex, toIndex);
            var tmp = effectFoldouts[fromIndex];
            effectFoldouts.RemoveAt(fromIndex);
            effectFoldouts.Insert(toIndex, tmp);

            SaveFoldoutStates();
        }

        void DrawType(GenericMenu menu, Type type, string name)
        {
            menu.AddItem(new GUIContent(name), false, () =>
            {
                var newEffect = (MOST_ActionCore)Activator.CreateInstance(type);
                Undo.RecordObject(effectsTarget, "Add Effect");
                effectsTarget.AddEffect(newEffect);
                EditorUtility.SetDirty(effectsTarget);
                effectFoldouts.Add(true);
                SaveFoldoutStates();
            });
        }
    }
}