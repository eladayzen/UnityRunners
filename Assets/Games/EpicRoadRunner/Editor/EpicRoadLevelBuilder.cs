using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner.EditorTools
{
    // Builds every EpicRoad level from EpicRoadBuildSettings and wires the results
    // into UniversalGameManager.
    //
    // The MOST generator rolls each row independently, so it cannot express mix,
    // pacing or ordering. This runs it, then rewrites the result:
    //
    //   1. barrel cost ramps along the track      (cheap early, expensive late)
    //   2. charge gates moved behind a barrel     (barrel eats your fire first)
    //   3. enemy share topped up to target        (fights, not just pickups)
    //   4. no empty stretch longer than MaxGapRows
    //   5. enemies kept clear of barrels/gates    (time to react)
    //   6. a final enemy, then the finish line
    [CustomEditor(typeof(EpicRoadBuildSettings))]
    public class EpicRoadBuildSettingsEditor : Editor
    {
        bool _advanced;

        public override void OnInspectorGUI()
        {
            var settings = (EpicRoadBuildSettings)target;
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Edit a level, then press Build All Levels.\n" +
                "Everything else is under Advanced and rarely needs touching.",
                MessageType.None);
            EditorGUILayout.Space();

            var levels = serializedObject.FindProperty("Levels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var spec = levels.GetArrayElementAtIndex(i);
                var seconds = spec.FindPropertyRelative("TrackLength");
                var share = spec.FindPropertyRelative("EnemyShare");
                var gearUp = spec.FindPropertyRelative("GearUpShare");
                var hpEnd = spec.FindPropertyRelative("BarrelHpEnd");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Level " + (i + 1), EditorStyles.boldLabel);

                float secs = EditorGUILayout.Slider(
                    new GUIContent("Length (seconds)", "How long the run takes."),
                    seconds.floatValue / 2.5f, 20f, 120f);
                seconds.floatValue = secs * 2.5f;

                EditorGUILayout.Slider(share,
                    0f, 0.6f, new GUIContent("Fights", "Share of encounters that are enemies."));

                EditorGUILayout.Slider(gearUp,
                    0f, 0.5f, new GUIContent("Safe opening", "Share of the level with no enemies, so you can gear up."));

                EditorGUILayout.Slider(hpEnd,
                    5f, 60f, new GUIContent("Toughest barrel", "Bullets the LAST barrel needs. First barrel stays cheap."));

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Build All Levels", GUILayout.Height(34)))
            {
                serializedObject.ApplyModifiedProperties();
                EpicRoadLevelBuilder.BuildAll(settings);
                return;
            }

            EditorGUILayout.Space();
            _advanced = EditorGUILayout.Foldout(_advanced, "Advanced", true);
            if (_advanced) DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }

    public static class EpicRoadLevelBuilder
    {
        public static void BuildAll(EpicRoadBuildSettings settings)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("EpicRoad", "Exit Play mode before building levels.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var report = new System.Text.StringBuilder();
            var built = new List<GameObject>();

            ClearExistingLevelRoots();

            for (int i = 0; i < settings.Levels.Length; i++)
            {
                var spec = settings.Levels[i];
                if (spec == null || spec.Profile == null)
                {
                    Debug.LogError($"[EpicRoad] Level {i + 1} has no profile assigned.");
                    return;
                }

                var prefab = BuildOne(settings, spec, i + 1, report);
                if (prefab == null) return;
                built.Add(prefab);
            }

            WireIntoGameManager(built);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[EpicRoad] Build complete.\n" + report);
        }

        static void ClearExistingLevelRoots()
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (System.Text.RegularExpressions.Regex.IsMatch(go.name, @"^L\d+ #"))
                    Object.DestroyImmediate(go);
        }

        static GameObject BuildOne(EpicRoadBuildSettings settings, EpicRoadBuildSettings.LevelSpec spec,
                                   int levelNumber, System.Text.StringBuilder report)
        {
            string prefix = "L" + levelNumber + " #";
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { settings.OutputFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(path).StartsWith(prefix))
                    AssetDatabase.DeleteAsset(path);
            }

            ApplySpecToProfile(spec, settings.OutputFolder, "L" + levelNumber);

            GameObject best = null;
            string bestPath = null;
            int bestGifts = -1;

            for (int roll = 0; roll < settings.RollsPerLevel; roll++)
            {
                var before = new HashSet<GameObject>(EditorSceneManager.GetActiveScene().GetRootGameObjects());
                spec.Profile.GetType().GetMethod("GenerateLevel").Invoke(spec.Profile, null);

                GameObject rolled = null;
                foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(go)) { rolled = go; break; }
                if (rolled == null) break;

                string rolledPath = settings.OutputFolder + "/" + rolled.name + ".prefab";
                int gifts = rolled.GetComponentsInChildren<MOST_Gate>(true).Length;

                if (gifts > bestGifts)
                {
                    if (best != null) { Object.DestroyImmediate(best); AssetDatabase.DeleteAsset(bestPath); }
                    best = rolled; bestPath = rolledPath; bestGifts = gifts;
                }
                else { Object.DestroyImmediate(rolled); AssetDatabase.DeleteAsset(rolledPath); }

                if (bestGifts >= spec.MinGifts) break;
            }

            if (best == null)
            {
                Debug.LogError($"[EpicRoad] Level {levelNumber} produced no layout.");
                return null;
            }

            var stats = ApplyDesignRules(best, spec);
            PrefabUtility.ApplyPrefabInstance(best, InteractionMode.AutomatedAction);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
            Object.DestroyImmediate(best);

            report.AppendLine(
                $"L{levelNumber}: {stats.enemyProps} enemy encounters / {stats.totalEncounters} total " +
                $"({stats.enemySharePct}%), {stats.gifts} gifts, " +
                $"barrels {spec.BarrelHpStart:0}->{spec.BarrelHpEnd:0}, " +
                $"gaps filled {stats.gapsFilled}, swapped {stats.swapped}, surge {stats.surge}, " +
                $"{spec.TrackLength / 2.5f:0}s");

            return asset;
        }

        static void ApplySpecToProfile(EpicRoadBuildSettings.LevelSpec spec, string outputFolder, string baseName)
        {
            var so = new SerializedObject(spec.Profile);
            so.FindProperty("Step").vector3Value = new Vector3(0f, 0f, spec.RowSpacing);
            so.FindProperty("OffsetFromStart").vector3Value = new Vector3(0f, 0f, spec.TrackLength);
            so.FindProperty("OffsetFromStartZ").floatValue = spec.StartClearance;
            so.FindProperty("FolderPath").stringValue = outputFolder;
            so.FindProperty("BaseLevelName").stringValue = baseName;

            var crowd = LoadCrowdPrefab(spec.CrowdSize);
            var parts = so.FindProperty("Parts");
            for (int i = 0; i < parts.arraySize; i++)
            {
                var part = parts.GetArrayElementAtIndex(i);
                string name = part.FindPropertyRelative("Name").stringValue;

                if (name.StartsWith("Enemy Crowd") && crowd != null)
                    part.FindPropertyRelative("Prefab").objectReferenceValue = crowd;

                if (name.StartsWith("Gate Children"))
                {
                    var presets = part.FindPropertyRelative("GatePresets");
                    for (int g = 0; g < presets.arraySize; g++)
                    {
                        var range = presets.GetArrayElementAtIndex(g).FindPropertyRelative("GateRange");
                        range.FindPropertyRelative("min").floatValue = spec.GateMin;
                        range.FindPropertyRelative("max").floatValue = spec.GateMax;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spec.Profile);
        }

        static GameObject LoadCrowdPrefab(int size)
        {
            string path =
                size <= 6 ? "Assets/Games/EpicRoadRunner/Prefabs/Enemies Crowd Small.prefab" :
                size <= 12 ? "Assets/Games/EpicRoadRunner/Prefabs/Enemies Crowd Medium.prefab" :
                             "Assets/Most In One/Runner/Prefabs/Enemies/Enemies Crowd.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        struct BuildStats
        {
            public int enemyProps, totalEncounters, enemySharePct, gifts, gapsFilled, enemiesSpaced, swapped, surge;
        }

        static BuildStats ApplyDesignRules(GameObject root, EpicRoadBuildSettings.LevelSpec spec)
        {
            float step = spec.RowSpacing;
            float minClear = step * spec.EnemyClearRows;
            float maxGap = step * spec.MaxGapRows;
            var crowd = LoadCrowdPrefab(spec.CrowdSize);

            var props = new List<Transform>();
            Transform endline = null;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "Endline") { endline = child; continue; }
                if (child.name.Contains("Empty")) continue;
                props.Add(child);
            }

            // 1. Barrel cost ramps with distance.
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in props) { minZ = Mathf.Min(minZ, p.position.z); maxZ = Mathf.Max(maxZ, p.position.z); }
            float span = Mathf.Max(1f, maxZ - minZ);

            foreach (var p in props)
            {
                if (p.name.Contains("Gate Children")) continue;
                var gate = p.GetComponentInChildren<MOST_Gate>(true);
                if (gate == null) continue;
                float t = Mathf.Clamp01((p.position.z - minZ) / span);
                float hp = Mathf.Max(2f, Mathf.Round(Mathf.Lerp(spec.BarrelHpStart, spec.BarrelHpEnd, t) * Random.Range(0.85f, 1.15f)));
                gate.SetGateValue(MOST_Gate.GateOperator.Add, hp);
            }

            // 2. Charge gates hide behind a barrel, so the barrel absorbs your fire.
            var barrels = props.FindAll(p => p.name.Contains("Barrel"));
            foreach (var p in props)
            {
                if (!p.name.Contains("Gate Children") || barrels.Count == 0) continue;

                bool shielded = false;
                foreach (var b in barrels)
                {
                    float dz = p.position.z - b.position.z;
                    if (dz > 1f && dz < step * 2.2f && Mathf.Abs(b.position.x - p.position.x) < 1f) { shielded = true; break; }
                }
                if (shielded) continue;

                Transform nearest = null; float nearestD = float.MaxValue;
                foreach (var b in barrels)
                {
                    float d = Mathf.Abs(b.position.z - p.position.z);
                    if (d < nearestD) { nearestD = d; nearest = b; }
                }
                if (nearest != null)
                    p.position = new Vector3(nearest.position.x, p.position.y, nearest.position.z + step * 0.85f);
            }

            // Opening stretch reserved for gearing up - no enemies allowed before this.
            // Measured against the configured track length, NOT the rolled props' span:
            // the span at this point excludes the finale enemy and the finish line, so
            // using it produced a zone only a third of the intended size.
            float gearUpEnd = minZ + spec.TrackLength * spec.GearUpShare;

            // 3a. Explicit swaps: turn a share of gates and barrels into fights.
            var enemiesSoFar = props.FindAll(p => p.GetComponentInChildren<WalkEnemyManager>(true) != null);
            int swapped = 0;
            if (crowd != null)
            {
                swapped += SwapForEnemies(root, props, crowd, gearUpEnd,
                    p => p.name.Contains("Gate Children"), spec.GatesToEnemies);
                swapped += SwapForEnemies(root, props, crowd, gearUpEnd,
                    p => p.name.Contains("Barrel") && !p.name.Contains("Start"), spec.BarrelsToEnemies);
            }

            // 3. Top the enemy share up to target by converting surplus barrels.
            var enemies = props.FindAll(p => p.GetComponentInChildren<WalkEnemyManager>(true) != null);
            int wanted = Mathf.RoundToInt(props.Count * spec.EnemyShare);
            int gapsFilled = 0;

            if (crowd != null && enemies.Count < wanted)
            {
                var convertible = props.FindAll(p =>
                    p.name.Contains("Barrel") && !p.name.Contains("Start") &&
                    p.position.z > gearUpEnd &&
                    p.GetComponentInChildren<WalkEnemyManager>(true) == null);

                // Convert the ones furthest from other enemies, so the level does not
                // end up front- or back-loaded.
                convertible.Sort((a, b) => NearestEnemyDistance(b, enemies).CompareTo(NearestEnemyDistance(a, enemies)));

                int convert = Mathf.Min(wanted - enemies.Count, convertible.Count);
                for (int i = 0; i < convert; i++)
                {
                    var victim = convertible[i];
                    var spawned = (GameObject)PrefabUtility.InstantiatePrefab(crowd);
                    spawned.transform.SetParent(root.transform, true);
                    spawned.transform.position = victim.position;
                    spawned.name = "Obj_Enemy Converted";
                    props.Remove(victim);
                    barrels.Remove(victim);
                    Object.DestroyImmediate(victim.gameObject);
                    props.Add(spawned.transform);
                    enemies.Add(spawned.transform);
                }
            }

            // 4. No dead stretches: drop an enemy into any gap that is too long.
            props.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            if (crowd != null && props.Count > 0)
            {
                var inserts = new List<float>();
                for (int i = 0; i < props.Count - 1; i++)
                {
                    float gap = props[i + 1].position.z - props[i].position.z;
                    if (gap <= maxGap) continue;
                    int pieces = Mathf.FloorToInt(gap / maxGap);
                    for (int k = 1; k <= pieces; k++)
                    {
                        float z = props[i].position.z + gap * k / (pieces + 1f);
                        if (z <= gearUpEnd) continue;   // opening stays enemy-free
                        inserts.Add(z);
                    }
                }
                foreach (float z in inserts)
                {
                    var filler = (GameObject)PrefabUtility.InstantiatePrefab(crowd);
                    filler.transform.SetParent(root.transform, true);
                    filler.transform.position = new Vector3(0f, 0f, z);
                    filler.name = "Obj_Enemy GapFill";
                    props.Add(filler.transform);
                    enemies.Add(filler.transform);
                    gapsFilled++;
                }
            }

            // 5. Enemies need clear air around barrels/gates, without clumping together.
            var blockers = new List<float>();
            foreach (var p in props)
                if (p.GetComponentInChildren<MOST_Gate>(true) != null) blockers.Add(p.position.z);

            enemies.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            var placed = new List<float>();
            float enemySeparation = step * 0.8f;
            int nudged = 0;

            foreach (var enemy in enemies)
            {
                if (enemy.name.Contains("Surge")) continue;   // deliberately packed, leave it
                float baseZ = enemy.position.z, chosen = baseZ, bestScore = float.MinValue;
                for (float d = 0f; d <= 40f; d += 1f)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        float candidate = side == 0 ? baseZ + d : baseZ - d;

                        bool crowded = false;
                        foreach (float z in placed)
                            if (Mathf.Abs(z - candidate) < enemySeparation) { crowded = true; break; }
                        if (crowded) continue;

                        float nearest = float.MaxValue;
                        foreach (float z in blockers) nearest = Mathf.Min(nearest, Mathf.Abs(z - candidate));

                        // Clamp the reward at minClear so it never drifts further than needed,
                        // then prefer staying close to where it started.
                        float score = Mathf.Min(nearest, minClear) * 100f - Mathf.Abs(candidate - baseZ);
                        if (candidate <= gearUpEnd) score -= 100000f;   // keep the opening enemy-free
                        if (score > bestScore) { bestScore = score; chosen = candidate; }
                    }
                }
                if (Mathf.Abs(chosen - baseZ) > 0.01f) nudged++;
                enemy.position = new Vector3(enemy.position.x, enemy.position.y, chosen);
                placed.Add(chosen);
            }

            // 5b. Back-half surge: pack the tail of the level with hordes.
            int surgeAdded = 0;
            if (crowd != null && spec.SurgeStart > 0f)
            {
                float surgeFrom = minZ + spec.TrackLength * spec.SurgeStart;
                float surgeTo = float.MinValue;
                foreach (var p in props) surgeTo = Mathf.Max(surgeTo, p.position.z);
                float gapStep = Mathf.Max(1f, step * spec.SurgeSpacingRows);
                float[] lanes = { -6f, 0f, 6f };
                int lane = 0;
                for (float z = surgeFrom; z <= surgeTo; z += gapStep)
                {
                    var horde = (GameObject)PrefabUtility.InstantiatePrefab(crowd);
                    horde.transform.SetParent(root.transform, true);
                    horde.transform.position = new Vector3(lanes[lane % lanes.Length], 0f, z);
                    horde.name = "Obj_Enemy Surge";
                    props.Add(horde.transform);
                    enemies.Add(horde.transform);
                    lane++;
                    surgeAdded++;
                }
            }

            // 6. Always finish on a fight, then the finish line.
            float lastZ = float.MinValue;
            foreach (var p in props) lastZ = Mathf.Max(lastZ, p.position.z);

            if (crowd != null)
            {
                var finale = (GameObject)PrefabUtility.InstantiatePrefab(crowd);
                finale.transform.SetParent(root.transform, true);
                finale.transform.position = new Vector3(0f, 0f, lastZ + Mathf.Max(minClear, step));
                finale.name = "Obj_FinalEnemy";
                lastZ = finale.transform.position.z;
                enemies.Add(finale.transform);
            }

            if (endline != null)
                endline.position = new Vector3(endline.position.x, endline.position.y, lastZ + 8f);

            int total = props.Count + 1;
            return new BuildStats
            {
                enemyProps = enemies.Count,
                totalEncounters = total,
                enemySharePct = Mathf.RoundToInt(100f * enemies.Count / Mathf.Max(1, total)),
                gifts = root.GetComponentsInChildren<MOST_Gate>(true).Length,
                gapsFilled = gapsFilled,
                enemiesSpaced = nudged,
                swapped = swapped,
                surge = surgeAdded
            };
        }

        static int SwapForEnemies(GameObject root, List<Transform> props, GameObject crowd,
                                  float afterZ, System.Predicate<Transform> match, float share)
        {
            if (share <= 0f) return 0;
            var candidates = props.FindAll(p => p.position.z > afterZ && match(p) &&
                                                p.GetComponentInChildren<WalkEnemyManager>(true) == null);
            int count = Mathf.FloorToInt(candidates.Count * share);
            // Spread the swaps evenly instead of taking a run of neighbours.
            candidates.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            for (int i = 0; i < count; i++)
            {
                var victim = candidates[Mathf.RoundToInt((float)i * (candidates.Count - 1) / Mathf.Max(1, count - 1))];
                if (victim == null) continue;
                var spawned = (GameObject)PrefabUtility.InstantiatePrefab(crowd);
                spawned.transform.SetParent(root.transform, true);
                spawned.transform.position = victim.position;
                spawned.name = "Obj_Enemy Swapped";
                props.Remove(victim);
                props.Add(spawned.transform);
                Object.DestroyImmediate(victim.gameObject);
                candidates[Mathf.RoundToInt((float)i * (candidates.Count - 1) / Mathf.Max(1, count - 1))] = null;
            }
            return count;
        }

        static float NearestEnemyDistance(Transform candidate, List<Transform> enemies)
        {
            float nearest = float.MaxValue;
            foreach (var e in enemies) nearest = Mathf.Min(nearest, Mathf.Abs(e.position.z - candidate.position.z));
            return nearest == float.MaxValue ? 999f : nearest;
        }

        static void WireIntoGameManager(List<GameObject> levels)
        {
            var manager = Object.FindFirstObjectByType<UniversalGameManager>();
            if (manager == null) { Debug.LogError("[EpicRoad] No UniversalGameManager in the scene."); return; }

            var so = new SerializedObject(manager);
            var array = so.FindProperty("LevelsPrefs");
            array.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            var data = manager.DatabaseHolder != null
                ? manager.DatabaseHolder.Get<IntData>(manager.LevelDataName) : null;
            if (data != null) { data.Value = 1; manager.DatabaseHolder.SaveToJson(); }
        }
    }
}
