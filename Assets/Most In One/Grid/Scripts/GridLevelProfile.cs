using System.Linq;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField, CreateAssetMenu(menuName = "MOST/Grid Level Profile", fileName = "GridLevelProfile")]
    public class GridLevelProfile : ScriptableObject
    {
        public enum TargetPlane { XY, XZ }

        [BigHeader("Level Layout")]
        [ShapeGridOptions(typeof(GameObject),
            allowSceneObjects: false,
            maxCellSize: 84f,
            minCellSize: 16f,
            spacing: 4f,
            enableTriggeredState = true,
            rightClickMode = ShapeGridRightClickMode.ContextMenu,
            shiftRightClickAlwaysClears = true)]
        public ShapeGrid grid = new();

        public TargetPlane targetPlane = TargetPlane.XZ;

        [Tooltip("Step per cell. X step = CellWorldSize.x. Second step = CellWorldSize.y (Y for XY, Z for XZ).")]
        public float cellWorldSize = 1;

        [Tooltip("Center of the grid in local space.")]
        public Vector3 centerPoint = Vector3.zero;

        [BigHeader("Meta Data")]
        public bool AddGridLevelMetaData = true;
        [HideIfAny(nameof(AddGridLevelMetaData), true, false)]
        public float CustomValue;

        [BigHeader("Optional Visual Helpers")]
        [InnerHint("(Optional) Spawns on each EMPTY cell (purely visual).")]
        public GameObject emptyCellFillPrefab;

        [InnerHint("(Optional) Spawns at CenterPoint (purely visual).")]
        public GameObject centerOfGridPrefab;

        [HideIfAny(nameof(AddGridLevelMetaData), true, false)]
        public GameObject[] ToBeCountedPrefs;

        [BigHeader("Prefab Generation")]
        public string outputFolder = "Assets/Most In One/Grid";
        public string prefabNameOverride = "";
        public bool overwritePrefab = true;

        [SerializeField] GameObject generatedPrefabAsset;
        public GameObject GeneratedPrefabAsset => generatedPrefabAsset;

        [InspectorButton(nameof(GenerateLevelPrefab),
            Label = "Generate Level Prefab",
            Tooltip = "Builds prefab from grid + adds GridLevelData",
            Height = 28)]
        public InspectorButton generatePrefabButton;

        [InspectorButton(nameof(GenerateLevelInScene),
            Label = "Generate Level In Scene",
            Tooltip = "Spawns this level into the current scene (works in play mode too).",
            Height = 28)]
        public InspectorButton generateInSceneButton;

        public int Columns => Mathf.Max(1, grid != null ? grid.columns : 1);
        public int Rows => Mathf.Max(1, grid != null ? grid.rows : 1);

        Vector3 AxisX => Vector3.right * cellWorldSize;

        Vector3 Axis2 => targetPlane switch
        {
            TargetPlane.XY => Vector3.up * cellWorldSize,
            TargetPlane.XZ => Vector3.forward * cellWorldSize,
            _ => Vector3.up * cellWorldSize
        };

        public Vector3 GetPositionForCell(int gridX, int gridY)
        {
            // keep your cell order flip (simulates the 180° on X by index)
            int yFlipped = Rows - 1 - gridY;

            return centerPoint
                 + AxisX * (gridX - (Columns - 1) * 0.5f)
                 + Axis2 * (yFlipped - (Rows - 1) * 0.5f);
        }

        /// <summary>
        /// Generates this level into the currently open scene.
        /// Works in editor (edit/play) and in runtime builds.
        /// </summary>
        public GameObject GenerateLevelInScene()
        {
            return GenerateLevelInSceneInternal(parent: null, worldPosition: null);
        }

        /// <summary>
        /// Optional overload for runtime code: spawn root at a given world position.
        /// (Inspector button calls the parameterless version above.)
        /// </summary>
        public GameObject GenerateLevelInScene(Vector3 worldPosition, Transform parent = null)
        {
            return GenerateLevelInSceneInternal(parent, worldPosition);
        }

        GameObject GenerateLevelInSceneInternal(Transform parent, Vector3? worldPosition)
        {
            if (grid == null)
            {
                Debug.LogError($"[{name}] Grid is null.");
                return null;
            }

            string rootName = string.IsNullOrWhiteSpace(prefabNameOverride) ? name : prefabNameOverride;

            var root = new GameObject(rootName);
            if (parent != null)
                root.transform.SetParent(parent, worldPositionStays: true);

            if (worldPosition.HasValue)
                root.transform.position = worldPosition.Value;

#if UNITY_EDITOR
            // Make it undoable in edit mode
            if (!Application.isPlaying)
                UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Generate Grid Level In Scene");
#endif

            BuildLevelHierarchy(root, preservePrefabLinksInEditor: true);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Mark scene dirty + select root for convenience
                UnityEditor.Selection.activeGameObject = root;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                );
            }
#endif

            return root;
        }

        // Prefab generation (editor-only)
        public GameObject GenerateLevelPrefab()
        {
#if UNITY_EDITOR
            if (grid == null)
            {
                Debug.LogError($"[{name}] Grid is null.");
                return null;
            }

            string folder = NormalizeFolder(outputFolder);
            EnsureFolderExists(folder);

            string prefabName = string.IsNullOrWhiteSpace(prefabNameOverride) ? name : prefabNameOverride;
            string prefabPath = $"{folder}/{prefabName}.prefab";

            if (!overwritePrefab)
                prefabPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(prefabPath);

            var root = new GameObject(prefabName);

            BuildLevelHierarchy(root, preservePrefabLinksInEditor: true);

            var prefabAsset = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
            UnityEngine.Object.DestroyImmediate(root);

            if (!success || prefabAsset == null)
            {
                Debug.LogError($"[{name}] Failed to save prefab at: {prefabPath}");
                return null;
            }

            generatedPrefabAsset = prefabAsset;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            return prefabAsset;
#else
            Debug.LogError("GenerateLevelPrefab is editor-only.");
            return null;
#endif
        }

        byte[] BuildCellStatesArray(int cols, int rows)
        {
            // 0 Empty, 1 Occupied, 2 Blocked
            var data = new byte[cols * rows];

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int idx = y * cols + x;

                    var obj = grid.Get(x, y) as GameObject;
                    var st = grid.GetState(x, y);

                    if (obj != null)
                    {
                        data[idx] = 1; // Occupied
                    }
                    else
                    {
                        data[idx] = (st == ShapeGrid.CellState.Triggered) ? (byte)2 : (byte)0; // Blocked/Empty
                    }
                }

            return data;
        }

        void BuildLevelHierarchy(GameObject root, bool preservePrefabLinksInEditor)
        {
            var cellsRoot = new GameObject("Cells");
            cellsRoot.transform.SetParent(root.transform, false);

            GameObject emptyRoot = null;
            if (emptyCellFillPrefab != null)
            {
                emptyRoot = new GameObject("EmptyCells");
                emptyRoot.transform.SetParent(root.transform, false);
            }

            // (Optional) center marker
            if (centerOfGridPrefab != null)
            {
                var markersRoot = new GameObject("Markers");
                markersRoot.transform.SetParent(root.transform, false);

                var centerInst = InstantiatePrefabSmart(centerOfGridPrefab, preservePrefabLinksInEditor);
                centerInst.transform.SetParent(markersRoot.transform, false);
                centerInst.transform.localPosition = centerPoint;
                centerInst.name = $"{centerOfGridPrefab.name} (Center)";
            }

            int cols = Columns;
            int rows = Rows;

            int emptyCount = 0;
            int tbCount = 0;

            // NEW: Start/End are purely grid metadata now
            bool hasStart = grid != null && grid.HasStartCell;
            bool hasEnd = grid != null && grid.HasEndCell;

            Vector2Int startCell = hasStart ? grid.StartCell : new Vector2Int(-1, -1);
            Vector2Int endCell = hasEnd ? grid.EndCell : new Vector2Int(-1, -1);

            // Full data snapshot: spawned instance per cell index (null for Empty/Blocked)
            var cellObjects = new GameObject[cols * rows];

            // Spawn prefab cells + count empties + count ToBeCountedPrefs
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var state = grid.GetState(x, y);
                    if (state == ShapeGrid.CellState.Empty)
                        emptyCount++;

                    var prefabObj = grid.Get(x, y) as GameObject;
                    if (prefabObj == null)
                        continue;

                    if (ToBeCountedPrefs != null && ToBeCountedPrefs.Contains(prefabObj))
                        tbCount++;

                    var instance = InstantiatePrefabSmart(prefabObj, preservePrefabLinksInEditor);
                    instance.transform.SetParent(cellsRoot.transform, false);
                    instance.transform.localPosition = GetPositionForCell(x, y);
                    instance.name = $"{prefabObj.name} ({x},{y})";

                    int idx = y * cols + x;
                    if (idx >= 0 && idx < cellObjects.Length)
                        cellObjects[idx] = instance;
                }

            // Fill EMPTY cells visually (optional) — emptyCount stays the same
            if (emptyCellFillPrefab != null && emptyRoot != null)
            {
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        if (grid.GetState(x, y) != ShapeGrid.CellState.Empty)
                            continue;

                        var emptyInstance = InstantiatePrefabSmart(emptyCellFillPrefab, preservePrefabLinksInEditor);
                        emptyInstance.transform.SetParent(emptyRoot.transform, false);
                        emptyInstance.transform.localPosition = GetPositionForCell(x, y);
                        emptyInstance.name = $"{emptyCellFillPrefab.name} ({x},{y})";
                    }
            }

            // Resolve Start/End objects from cellObjects (may be null if those cells are empty)
            GameObject startObj = null;
            GameObject endObj = null;

            if (hasStart && startCell.x >= 0 && startCell.y >= 0 && startCell.x < cols && startCell.y < rows)
            {
                int sIdx = startCell.y * cols + startCell.x;
                if (sIdx >= 0 && sIdx < cellObjects.Length) startObj = cellObjects[sIdx];
            }

            if (hasEnd && endCell.x >= 0 && endCell.y >= 0 && endCell.x < cols && endCell.y < rows)
            {
                int eIdx = endCell.y * cols + endCell.x;
                if (eIdx >= 0 && eIdx < cellObjects.Length) endObj = cellObjects[eIdx];
            }

            if (AddGridLevelMetaData)
            {
                var meta = root.AddComponent<GridLevelData>();

                meta.CustomValue = CustomValue;

                meta.LevelGridByCells = new Vector2(cols, rows);
                meta.CenterPoint = centerPoint;
                meta.CellWorldSize = cellWorldSize;

                meta.Plane = (targetPlane == TargetPlane.XZ) ? GridLevelData.LevelPlane.XZ : GridLevelData.LevelPlane.XY;
                meta.FlipSecondAxisByIndex = true; // because GetPositionForCell flips by index

                meta.CellStates = BuildCellStatesArray(cols, rows);
                meta.CellObjects = cellObjects;

                meta.HasStartCell = hasStart;
                meta.StartCell = startCell;

                meta.HasEndCell = hasEnd;
                meta.EndCell = endCell;

                meta.StartCellObject = startObj;
                meta.EndCellObject = endObj;

                meta.NumberOfEmptyCells = emptyCount;
                meta.NumberCountedObjects = tbCount;
            }
        }

        static GameObject InstantiatePrefabSmart(GameObject prefab, bool preservePrefabLinksInEditor)
        {
            if (prefab == null) return null;

#if UNITY_EDITOR
            // In editor edit mode, this keeps prefab connection in the scene/prefab build
            if (preservePrefabLinksInEditor && !Application.isPlaying)
            {
                var obj = UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
                if (obj is GameObject go) return go;
            }
#endif
            // Runtime / play mode fallback
            return Object.Instantiate(prefab);
        }

#if UNITY_EDITOR
        static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return "Assets/GeneratedLevels";

            folder = folder.Replace("\\", "/").TrimEnd('/');

            if (!folder.StartsWith("Assets"))
                return "Assets/GeneratedLevels";

            return folder;
        }

        static void EnsureFolderExists(string folder)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
#endif
    }
}