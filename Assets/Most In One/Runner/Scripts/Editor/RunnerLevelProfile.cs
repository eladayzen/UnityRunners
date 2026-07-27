#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField, CreateAssetMenu(menuName = "MOST/Runner Level Profile", fileName = "RunnerLevelProfile")]
    public class RunnerLevelGenerator : ScriptableObject
    {
        [HelpBox("Empty properties will be ignored (optional).", HelpBoxKind.Info)]
        [BigHeader("Layout")]
        [Tooltip("World position where the generated level root, road segments, objects, and endline are based from.")]
        public Vector3 StartWorldPosition;
        [Tooltip("Road segment prefab that will be repeated along the level path.")]
        public GameObject RoadPrefab;
        [Min(1), Tooltip("Number of road prefab instances to place in sequence.")] public int RepeatTimes = 10;
        [Tooltip("World-space offset added between each repeated road segment.")]
        public Vector3 OffsetBetweenParts = new(0, 0, 10);

        [BigHeader("Objects")]
        [HelpBox("Controls the playable object spawn area: where spawning starts and stops, row spacing, lane width, and vertical stepping.", HelpBoxKind.Info)]
        [Tooltip("Z distance from StartWorldPosition where object spawning begins.")]
        public float OffsetFromStartZ = 5f;
        [Tooltip("Z distance kept clear before the endline so objects do not spawn too close to the finish.")]
        public float OffsetBeforeEndlineZ = 5f;
        [Tooltip("Spacing used between spawn rows. Z controls forward spacing, and Y can add vertical rows when greater than zero.")]
        public Vector3 Step = new(0, 0, 5);
        [Tooltip("Total usable road width used to calculate lane positions for one, two, or three separated objects.")]
        public float RoadWidth = 9f;

        [Serializable]
        public class Part
        {
            [Tooltip("Display name used when naming spawned instances in the generated level hierarchy.")]
            public string Name = "Part";
            [GUIColor("cyan"), Tooltip("Prefab to instantiate when this part is selected for a spawn line.")] public GameObject Prefab;
            [Range(1, 3), Tooltip("Number of lane positions this part group occupies on the same spawn line: 1 center, 2 side lanes, or 3 full lanes.")] public int Separation = 1;
            [Tooltip("Extra delta added to global Step Z when this part is selected (can be negative)")]
            public float ExtraStepZ = 0f;

            [Tooltip("Additional local offset applied to the spawned instance after its lane and row position are calculated.")]
            public Vector3 SpawnOffset;
            [Min(0), Tooltip("Ignore this part for the first N spawn lines (rows along Z). Example: 2 => ignored for the first and second lines.")]
            public int IgnoreFirstLines = 0;

            [GUIColor("Red"), Min(0f), Tooltip("Weighted chance for this part to be selected. Higher values make it appear more often; zero disables it.")] public float Chance = 1f;
            [Line, Tooltip("When enabled, the spawned prefab is treated as a MOST_Gate and its GateText/OutputText can be generated automatically.")] public bool AsAGate;

            [Serializable]
            public class GatePreset
            {
                [Tooltip("Sign to prepend (e.g., x or +)")]
                public string Sign = "x";

                [Tooltip("Inclusive min and max of the stepped range")]
                public MOSTRange GateRange;

                [Tooltip("Step size (> 0) for the stepped grid")]
                public float Step = 1f;

                [Tooltip("Exclude zero from the candidate values")]
                public bool NoZero = false;
            }

            [Tooltip("Available random gate text presets used when this part spawns as a gate.")]
            public List<GatePreset> GatePresets = new();

            [HideIfAny(nameof(AsAGate), true, false), Tooltip("If enabled, show Output properties and write MOST_Gate.OutputText")]
            public bool UseGateOutputText;

            [HideIfAny(nameof(AsAGate), true, false, nameof(UseGateOutputText), true, false), Tooltip("Sign to prepend for OutputText (e.g., x or +)")]
            public string OutputSign = "x";
            [HideIfAny(nameof(AsAGate), true, false, nameof(UseGateOutputText), true, false), Tooltip("Inclusive min for Output e.g. 10 -> x10")]
            public MOSTRange OutputRange;
            [HideIfAny(nameof(AsAGate), true, false, nameof(UseGateOutputText), true, false), Tooltip("Step size for Output value (> 0)")]
            public float OutputStep = 1f;
            [HideIfAny(nameof(AsAGate), true, false, nameof(UseGateOutputText), true, false), Tooltip("Exclude zero from Output candidates")]
            public bool OutputNoZero = false;
        }
        [HelpBox("Configure the prefabs that can spawn on the road. Chance controls weighting, Separation controls lane usage, and gate settings can auto-fill MOST_Gate text.", HelpBoxKind.Info)]
        [Tooltip("List of object and gate prefabs that can be randomly spawned along the generated level.")]
        public List<Part> Parts = new();

        [BigHeader("Endgame")]
        [Tooltip("Prefab placed at the end of the generated track.")]
        public GameObject EndlinePrefab;
        [Tooltip("Offset from StartWorldPosition used to place the endline prefab.")]
        public Vector3 OffsetFromStart = new(0, 0, 120);
        [Tooltip("Enable bonus repeat generation after the endline.")]
        public bool HaveBonus;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [BigHeader("Bonus Repeat")]
        [Tooltip("Prefab repeated for each bonus step after the endline.")]
        public GameObject RepeatedPrefab;

        [ReadOnlyIf(nameof(HaveBonus), false), Min(1), Tooltip("Number of bonus repeated prefab instances to create.")] public int Amount = 5;
        [ReadOnlyIf(nameof(HaveBonus), false)]
        [Tooltip("Offset added after each bonus repeated prefab to position the next bonus step.")]
        public Vector3 OffsetPerStep = new(0, 0, 5);
        [ReadOnlyIf(nameof(HaveBonus), false)]
        [Tooltip("Offset from the endline position used as the starting point for bonus steps.")]
        public Vector3 OffsetFromEndpoint;

        [ReadOnlyIf(nameof(HaveBonus), false), Line]
        [Tooltip("When enabled, bonus repeated prefabs are treated as gates and receive GateText and OutputText values.")]
        public bool IsGate;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [GUIColor("green"), HideIfAny(nameof(IsGate), true, false), Tooltip("Sign to prepend for GateText (e.g., x or +)")]
        public string GateSign = "x";

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, false), Tooltip("GateText starts at this value")]
        public float GateStartValue = 1f;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, false), Tooltip("GateText increment per spawn")]
        public float GateJumpPerStep = 0.2f;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [GUIColor("green"), HideIfAny(nameof(IsGate), true, false), Tooltip("Sign to prepend for OutputText (e.g., x or +)")]
        public string OutputSign = "x";

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, false), Tooltip("OutputText starts at this value")]
        public float OutputStartValue = 1f;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, false), Tooltip("OutputText increment per spawn")]
        public float OutputJumpPerStep = 0.2f;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, true), Tooltip("Starting value for text (e.g., x1.0)")]
        public float TextStartValue = 1f;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, true), Tooltip("Increment per spawn (e.g., 0.2 -> x1.0, x1.2, ...)")]
        public float TextJumpPerStep = 0.2f;

        [Line]
        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, true)]
        [Tooltip("Child transform paths containing TMP_Text components to update for each non-gate bonus step.")]
        public string[] TextNameInStepChilds;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, true)]
        [Tooltip("Child transform path containing the Renderer whose material should cycle through MaterialsLerp.")]
        public string TargetColorMaterialLerpName;

        [ReadOnlyIf(nameof(HaveBonus), false)]
        [HideIfAny(nameof(IsGate), true, true)]
        [Tooltip("Materials assigned in order to the target renderer across generated non-gate bonus steps.")]
        public List<Material> MaterialsLerp;

        [BigHeader("Output")]
        [HelpBox("Controls how the generated level is output: save as a numbered prefab in a project folder or create a scene-only preview.", HelpBoxKind.Info)]
        [Tooltip("When enabled, the generated level root is saved as a prefab asset and connected in the scene.")]
        public bool SaveAsPrefab = true;
        [Tooltip("Unity project folder path where generated level prefabs are saved. Must be inside Assets.")]
        public string FolderPath = "Assets/Levels";
        [Tooltip("Base name used for generated level roots and prefab assets. Auto-numbering is added when saving.")]
        public string BaseLevelName = "Level";

        // Inspector button – uses the externally defined InspectorButton attribute
        [InspectorButton(nameof(GenerateLevel), Label = "Generate Level", Tooltip = "Creates the level in the current scene", Height = 32)]
        [Tooltip("Inspector button field used to run GenerateLevel from the custom inspector drawer.")]
        public InspectorButton generateButton;

        [ContextMenu("Generate Level (Context)")]
        public void GenerateLevel()
        {
            if (!CanGenerate())
            {
                EditorUtility.DisplayDialog("Generator", "Cannot generate: check output folder if saving as prefab.", "OK");
                return;
            }

            string rootName = SaveAsPrefab ? ComputeNextAutoName(FolderPath, BaseLevelName) : $"{BaseLevelName}_Preview";
            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Level Root");

            Vector3 start = StartWorldPosition;
            if (RoadPrefab != null)
            {
                for (int i = 0; i < RepeatTimes; i++)
                {
                    var pos = start + (OffsetBetweenParts * i);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(RoadPrefab);
                    Undo.RegisterCreatedObjectUndo(inst, "Instantiate Road Segment");
                    inst.transform.SetParent(root.transform, true);
                    inst.transform.position = pos;
                    inst.name = $"Road_{i + 1:000}";
                }
            }

            GameObject endlineInstance = null;
            if (EndlinePrefab != null)
            {
                var endPos = start + OffsetFromStart;
                endlineInstance = (GameObject)PrefabUtility.InstantiatePrefab(EndlinePrefab);
                Undo.RegisterCreatedObjectUndo(endlineInstance, "Instantiate Endline");
                endlineInstance.transform.SetParent(root.transform, true);
                endlineInstance.transform.position = endPos;
                endlineInstance.name = "Endline";
            }

            if (HaveBonus && RepeatedPrefab != null)
                GenerateBonus(root.transform, start, endlineInstance);

            GenerateLevelObjects(root.transform, start, endlineInstance);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (SaveAsPrefab)
            {
                EnsureFolder(FolderPath);
                var prefabPath = GetUniquePrefabPath(FolderPath, BaseLevelName);
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
                if (saved != null)
                {
                    EditorGUIUtility.PingObject(saved);
                    Debug.Log($"Saved level prefab at: {prefabPath}");
                }
                else
                    EditorUtility.DisplayDialog("Prefab Save Failed", "Could not save the generated level as a prefab.", "OK");
            }
        }

        private bool CanGenerate()
        {
            if (SaveAsPrefab && string.IsNullOrEmpty(FolderPath)) return false;
            return true;
        }

        private void GenerateBonus(Transform root, Vector3 start, GameObject endlineInstance)
        {
            if (RepeatedPrefab == null) return;
            var bonusRoot = new GameObject("Bonus");
            Undo.RegisterCreatedObjectUndo(bonusRoot, "Create Bonus Root");
            bonusRoot.transform.SetParent(root, true);

            Vector3 fallbackVec =
                (OffsetFromStart.sqrMagnitude > 1e-6f)
                    ? OffsetFromStart
                    : (OffsetBetweenParts * Mathf.Max(1, RepeatTimes));

            Vector3 endpoint = (endlineInstance != null)
                ? endlineInstance.transform.position
                : start + fallbackVec;

            Vector3 basePos = endpoint + OffsetFromEndpoint;
            Vector3 tmpPoint = basePos;

            float legacyVal = TextStartValue;
            float gateVal = GateStartValue;
            float outVal = OutputStartValue;

            for (int count = 0; count < Mathf.Max(1, Amount); count++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(RepeatedPrefab);
                if (inst == null) { tmpPoint += OffsetPerStep; continue; }

                Undo.RegisterCreatedObjectUndo(inst, "Instantiate Bonus Step");
                inst.transform.SetParent(bonusRoot.transform, true);
                inst.transform.position = tmpPoint;
                inst.name = $"BonusStep_{count + 1:000}";

                if (IsGate)
                {
                    foreach (var gate in FindMostGates(inst.transform))
                    {
                        var so = new SerializedObject(gate);
                        var pG = so.FindProperty("GateText");
                        var pOut = so.FindProperty("OutputText");

                        string gTxt = ComposeSigned(GateSign, gateVal);
                        string oTxt = ComposeSigned(OutputSign, outVal);

                        if (pG != null) pG.stringValue = gTxt;
                        if (pOut != null) pOut.stringValue = oTxt;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    gateVal += GateJumpPerStep;
                    outVal += OutputJumpPerStep;
                }
                else
                {
                    foreach (var path in TextNameInStepChilds)
                    {
                        if (string.IsNullOrEmpty(path)) continue;
                        var t = inst.transform.Find(path);
                        if (t)
                        {
                            var tmp = t.GetComponent<TMP_Text>();
                            if (tmp) tmp.text = "x" + legacyVal.ToString("0.0");
                        }
                    }

                    legacyVal += TextJumpPerStep;
                    if (!string.IsNullOrEmpty(TargetColorMaterialLerpName) && MaterialsLerp != null && MaterialsLerp.Count > 0)
                    {
                        var rT = inst.transform.Find(TargetColorMaterialLerpName);
                        if (rT)
                        {
                            var rend = rT.GetComponent<Renderer>();
                            if (rend)
                            {
                                var mat = MaterialsLerp[count % MaterialsLerp.Count];
                                if (mat) rend.material = mat;
                            }
                        }
                    }
                }
                tmpPoint += OffsetPerStep;
            }
        }

        private static IEnumerable<Component> FindMostGates(Transform root)
        {
            var all = root.GetComponentsInChildren<Component>(includeInactive: true);
            foreach (var c in all)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t != null && t.Name == "MOST_Gate") yield return c;
            }
        }

        private void GenerateLevelObjects(Transform root, Vector3 start, GameObject endlineInstance)
        {
            if (Parts == null || Parts.Count == 0) return;

            float startZ = start.z + OffsetFromStartZ;

            float fallbackLenZ =
                (Mathf.Abs(OffsetFromStart.z) > 1e-6f)
                    ? OffsetFromStart.z
                    : (OffsetBetweenParts.z * Mathf.Max(1, RepeatTimes));

            float trackEndZ = (endlineInstance != null)
                ? endlineInstance.transform.position.z
                : (start.z + fallbackLenZ);

            float endZ = trackEndZ - OffsetBeforeEndlineZ;

            if (endZ <= startZ)
                endZ = startZ + Mathf.Max(0.01f, Step.z);

            var validParts = Parts.Where(p => p != null && p.Prefab != null && p.Chance > 0f).ToList();
            if (validParts.Count == 0) return;

            List<float> GetLaneXs(int separation)
            {
                float w = RoadWidth;
                return Mathf.Clamp(separation, 1, 3) switch
                {
                    1 => new List<float> { 0f },
                    2 => new List<float> { -w * 0.25f, +w * 0.25f },
                    _ => new List<float> { -w * 0.25f, 0f, +w * 0.25f },
                };
            }

            float anchorZ = startZ;
            int lineIndex = 0;
            while (true)
            {
                var activeParts = validParts.Where(p => lineIndex >= Mathf.Max(0, p.IgnoreFirstLines)).ToList();

                if (activeParts.Count == 0)
                {
                    float stepSkip = Mathf.Max(0.01f, Step.z);
                    float midZSkip = anchorZ + stepSkip * 0.5f;
                    if (midZSkip > endZ + 1e-4f) break;

                    anchorZ += stepSkip;
                    lineIndex++;
                    continue;
                }

                var g1 = activeParts.Where(p => p.Separation == 1).ToList();
                var g2 = activeParts.Where(p => p.Separation == 2).ToList();
                var g3 = activeParts.Where(p => p.Separation == 3).ToList();

                float s1 = g1.Sum(p => p.Chance);
                float s2 = g2.Sum(p => p.Chance);
                float s3 = g3.Sum(p => p.Chance);
                float totalSep = (g1.Count > 0 ? s1 : 0f) + (g2.Count > 0 ? s2 : 0f) + (g3.Count > 0 ? s3 : 0f);
                if (totalSep <= 0f) break;

                float rr = UnityEngine.Random.Range(0f, totalSep), accSep = 0f;
                int sep;
                if (g1.Count > 0 && (accSep += s1) >= rr) sep = 1;
                else if (g2.Count > 0 && (accSep += s2) >= rr) sep = 2;
                else sep = 3;

                var group = sep == 1 ? g1 : sep == 2 ? g2 : g3;
                if (group.Count == 0) break;

                var selected = new List<Part>(sep);
                Shuffle(selected);
                var pool = new List<Part>(group);
                for (int need = sep; need > 0; need--)
                {
                    if (pool.Count == 0) pool = new List<Part>(group);
                    float sum = pool.Sum(p => p.Chance);
                    Part pick;
                    if (sum <= 0f)
                    {
                        pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                    }
                    else
                    {
                        float r = UnityEngine.Random.Range(0f, sum), acc = 0f;
                        pick = pool[0];
                        for (int i = 0; i < pool.Count; i++)
                        {
                            acc += pool[i].Chance;
                            if (r <= acc) { pick = pool[i]; break; }
                        }
                    }
                    selected.Add(pick);
                    pool.Remove(pick);
                }

                float avgExtra = selected.Average(p => p.ExtraStepZ);
                float localStepZ = Mathf.Max(0.01f, Step.z + avgExtra);
                float midZ = anchorZ + localStepZ * 0.5f;
                if (midZ > endZ + 1e-4f) break;

                var lanes = GetLaneXs(sep);
                float yStart = start.y;
                float yEnd = start.y + (Mathf.Abs(Step.y) < 1e-5f ? 0f : Step.y);
                for (float y = yStart; y <= yEnd + 1e-6f; y += Mathf.Max(0.0001f, Step.y == 0 ? float.MaxValue : Step.y))
                {
                    for (int li = 0; li < lanes.Count; li++)
                    {
                        var part = selected[li % selected.Count];
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(part.Prefab);
                        if (inst == null) continue;

                        Undo.RegisterCreatedObjectUndo(inst, "Instantiate Level Object");
                        inst.transform.SetParent(root, true);
                        inst.transform.position = new Vector3(start.x + lanes[li], y, midZ) + part.SpawnOffset;
                        inst.name = $"Obj_{(string.IsNullOrEmpty(part.Name) ? "Part" : part.Name)}_{midZ:0.#}_{lanes[li]:0.#}";

                        if (part.AsAGate)
                        {
                            if (part.GatePresets == null || part.GatePresets.Count == 0)
                                part.GatePresets = new List<Part.GatePreset> { new Part.GatePreset() };

                            foreach (var gate in FindMostGates(inst.transform))
                            {
                                var pr = part.GatePresets[UnityEngine.Random.Range(0, part.GatePresets.Count)];
                                string gateSign = string.IsNullOrEmpty(pr.Sign) ? "x" : pr.Sign;

                                var gateVals = BuildStepped(pr.GateRange.Min, pr.GateRange.Max, Mathf.Max(0.0001f, pr.Step), pr.NoZero);
                                float gv = gateVals[UnityEngine.Random.Range(0, gateVals.Count)];
                                string gText = ComposeSigned(gateSign, gv);

                                string oText = null;
                                if (part.UseGateOutputText)
                                {
                                    string outSign = string.IsNullOrEmpty(part.OutputSign) ? "x" : part.OutputSign;
                                    var outVals = BuildStepped(part.OutputRange.Min, part.OutputRange.Max,
                                                               Mathf.Max(0.0001f, part.OutputStep), part.OutputNoZero);
                                    float ov = (outVals != null && outVals.Count > 0)
                                                ? outVals[UnityEngine.Random.Range(0, outVals.Count)]
                                                : gv;
                                    oText = ComposeSigned(outSign, ov);
                                }

                                var so = new SerializedObject(gate);
                                var pG = so.FindProperty("GateText");
                                var pO = so.FindProperty("OutputText");
                                if (pG != null) pG.stringValue = gText;
                                if (part.UseGateOutputText && pO != null) pO.stringValue = oText;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }

                    }

                    if (Mathf.Abs(Step.y) < 1e-5f) break;
                }
                anchorZ += localStepZ;
                lineIndex++;
            }
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static string FormatNum(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v))
                ? Mathf.RoundToInt(v).ToString()
                : v.ToString("0.0");
        }

        private static string ComposeSigned(string sign, float v)
        {
            if (string.IsNullOrEmpty(sign)) sign = "x";
            var num = FormatNum(v);
            if (sign == "+" && v < 0f) return num;
            return sign + num;
        }

        private List<float> BuildStepped(float minV, float maxV, float step, bool noZero)
        {
            List<float> list = new();
            float lo = Mathf.Min(minV, maxV);
            float hi = Mathf.Max(minV, maxV);
            step = Mathf.Max(0.0001f, step);

            float k = Mathf.Ceil(lo / step);
            for (; ; k += 1f)
            {
                float v = k * step;
                if (v > hi + 1e-6f) break;
                if (noZero && Mathf.Abs(v) <= 1e-6f) continue;
                v = Mathf.Abs(v) < 1e-6f ? 0f : v;
                list.Add(v);
            }

            if (list.Count == 0)
            {
                if (!(noZero && Mathf.Abs(lo) <= 1e-6f)) list.Add(lo);
                if (!(noZero && Mathf.Abs(hi) <= 1e-6f) && Mathf.Abs(hi - lo) > 1e-6f) list.Add(hi);
                if (list.Count == 0) list.Add(lo != 0f ? lo : (hi != 0f ? hi : step));
            }
            return list;
        }

        private static string ToUnityRelativePath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (absolute.StartsWith(dataPath))
            {
                var rel = "Assets" + absolute[dataPath.Length..];
                return rel.TrimEnd('/');
            }
            EditorUtility.DisplayDialog("Invalid Folder", "Please choose a folder inside your project's Assets/ directory.", "OK");
            return "Assets";
        }

        private static void EnsureFolder(string unityFolderPath)
        {
            if (AssetDatabase.IsValidFolder(unityFolderPath)) return;

            var parts = unityFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0 || parts[0] != "Assets")
            {
                throw new Exception("Folder path must start with 'Assets'.");
            }

            string acc = "Assets";
            for (int i = 1; i < parts.Count; i++)
            {
                string next = acc + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(acc, parts[i]);
                }
                acc = next;
            }
        }

        private static string ComputeNextAutoName(string folderPath, string baseName)
        {
            int n = GetNextNumber(folderPath, baseName);
            return $"{baseName} #{n}";
        }

        private static string GetUniquePrefabPath(string folderPath, string baseName)
        {
            int n = GetNextNumber(folderPath, baseName);
            string fileName = $"{baseName} #{n}.prefab";
            return Path.Combine(folderPath, fileName).Replace('\\', '/');
        }

        private static int GetNextNumber(string folderPath, string baseName)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(baseName)) return 1;
            if (!AssetDatabase.IsValidFolder(folderPath)) return 1;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var regex = new Regex($@"^{Regex.Escape(baseName)} #(?<num>\d+)$");
            int max = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                var m = regex.Match(name);
                if (m.Success && int.TryParse(m.Groups["num"].Value, out int val))
                {
                    if (val > max) max = val;
                }
            }
            return Mathf.Max(1, max + 1);
        }
    }
}
#endif