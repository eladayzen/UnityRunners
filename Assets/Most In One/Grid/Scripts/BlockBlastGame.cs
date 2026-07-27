using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class BlockBlastGame : MonoBehaviour
    {
        #region Nested Types

        public enum CellState : byte { Empty = 0, Occupied = 1, Blocked = 2 }

        [HelpBox("Triggered when a single move clears multiple lines at once.", HelpBoxKind.Info)]
        [Serializable]
        public class MultiLineStage
        {
            [Tooltip("Optional display id for this multi-line stage.")]
            public string id;

            [Min(2)]
            [Tooltip("Total number of lines cleared in one move needed to activate this stage.")]
            public int requiredLines = 2;

            [Min(1f)]
            [Tooltip("Multiplier applied to the base clear score before the combo-stage multiplier.")]
            public float scoreMultiplier = 1.2f;

            [Min(0)]
            [Tooltip("Flat bonus added after the multi-line multiplier is applied.")]
            public int flatBonus = 10;

            [Tooltip("Effect spawned once when this multi-line stage is triggered.")]
            public GameObject effectPrefab;
        }

        [Serializable]
        struct PlacementSnapshot
        {
            [Tooltip("How many cells were placed by the last committed piece.")]
            public int placedCells;

            [Tooltip("World-space center of the most recently placed piece.")]
            public Vector3 centerWorld;

            public bool HasPlacedCells => placedCells > 0;
        }

        [Serializable]
        public struct ClearResolution
        {
            [Tooltip("How many horizontal lines were cleared by the move.")]
            public int rowsCleared;

            [Tooltip("How many vertical lines were cleared by the move.")]
            public int colsCleared;

            [Tooltip("How many occupied cells were actually removed.")]
            public int clearedCells;

            [Tooltip("True when the board became completely empty after the clear.")]
            public bool perfectClear;

            [Tooltip("Average world-space center of all cleared cells.")]
            public Vector3 clearCenterWorld;

            [Tooltip("Whether clearCenterWorld contains a valid center for the last clear.")]
            public bool hasClearCenter;

            public int LineCount => rowsCleared + colsCleared;
            public bool AnyClear => clearedCells > 0;
            public bool IsMultiLine => LineCount >= 2;
            public bool IsCrossClear => rowsCleared > 0 && colsCleared > 0;
        }

        [HelpBox("Readonly runtime breakdown for the last scored move.", HelpBoxKind.Info)]
        [Serializable]
        public struct ScoreBreakdown
        {
            [Tooltip("How many cells were placed in the move.")]
            public int placedCells;

            [Tooltip("How many cells were cleared in the move.")]
            public int clearedCells;

            [Tooltip("How many rows were cleared in the move.")]
            public int rowsCleared;

            [Tooltip("How many columns were cleared in the move.")]
            public int colsCleared;

            [Tooltip("Total cleared lines in the move. Equals rows + columns.")]
            public int lines;

            [Tooltip("Combo streak after this move was resolved.")]
            public int streak;

            [Tooltip("Score awarded only for placing the piece.")]
            public int placementScore;

            [Tooltip("Raw clear score before multi-line and combo-stage adjustments.")]
            public int baseClearScore;

            [Tooltip("Final clear score after all multipliers and bonuses except perfect clear.")]
            public int finalClearScore;

            [Tooltip("Extra bonus awarded for fully emptying the board.")]
            public int perfectClearBonus;

            [Tooltip("Final total score granted for the move.")]
            public int totalScore;

            [Tooltip("Matched multi-line stage index. -1 means no multi-line stage was used.")]
            public int multiLineStageIndex;

            [Tooltip("Multiplier taken from the matched multi-line stage.")]
            public float multiLineStageMultiplier;

            [Tooltip("Flat bonus taken from the matched multi-line stage.")]
            public int multiLineFlatBonus;

            [Tooltip("Extra bonus granted when the move clears both row(s) and column(s).")]
            public int patternBonus;
        }

        struct Piece
        {
            public string shapeId;
            public int rotationDeg;
            public Vector2Int[] relOffsets;
            public Vector2Int size;
            public BlockSkin skin;

            public bool IsValid => relOffsets != null && relOffsets.Length > 0;
            public int Count => relOffsets?.Length ?? 0;
        }

        class GhostUnit
        {
            public GameObject go;
            public SpriteRenderer[] srs;
            public Color[] baseColors;
        }

        #endregion

        #region Inspector Fields + Public Properties

        [BigHeader("Core")]
        [Required("Assign a ShapeLibrarySO asset.")]
        [Tooltip("Library used to randomly pick block shapes and skins.")]
        public ShapeLibrarySO shapeLibrary;

        [ReadOnly]
        [Tooltip("Resolved level grid metadata used to build and interpret the board.")]
        public GridLevelData levelMeta;

        [BigHeader("Tray")]
        [Range(1, 6)]
        [Tooltip("How many piece slots exist in the tray.")]
        public int traySize = 3;

        [Required("Assign tray slot anchors.")]
        [Tooltip("Anchors used as visual holders for tray pieces. Size should match or exceed Tray Size.")]
        public Transform[] traySlotAnchors;

        [Min(0.1f)]
        [Tooltip("Visual scale applied to pieces while they are sitting in the tray.")]
        public float trayPieceScale = 0.9f;

        [Tooltip("Invoked whenever tray contents are rebuilt or changed.")]
        public UnityEvent TrayChanged;

        public string BroadcastPlacedBlocks;

        [BigHeader("Dragging")]
        [Range(0f, 1.5f)]
        [Tooltip("How high above the board plane the dragged piece is lifted, measured in board cells.")]
        public float dragLiftInCells = 0.35f;

        [Range(0.01f, 0.2f)]
        [Tooltip("SmoothDamp time used while following the pointer during drag.")]
        public float dragSmoothTime = 0.05f;

        [Min(0f)]
        [Tooltip("Maximum speed used by drag smoothing.")]
        public float dragMaxSpeed = 80f;

        [Range(0.05f, 0.25f)]
        [Tooltip("Duration used to snap a valid dropped piece onto its final board position.")]
        public float dropSnapTime = 0.10f;

        [Range(0.05f, 0.35f)]
        [Tooltip("Duration used to animate an invalid piece back to the tray.")]
        public float returnToTrayTime = 0.12f;

        [BigHeader("Sorting Orders")]
        [Tooltip("Sorting order forced on the actively dragged piece.")]
        public int dragSortingOrder = 2000;

        [Tooltip("Sorting order used for ghost-preview blocks.")]
        public int ghostSortingOrder = 1500;

        [Tooltip("Sorting order used for clear-preview overlays.")]
        public int cleanPreviewSortingOrder = 1800;

        [BigHeader("Ghost Preview")]
        [Tooltip("Enables a ghost copy of the dragged shape on the board.")]
        public bool enableGhostPreview = true;

        [ReadOnlyIf(nameof(enableGhostPreview), false)]
        [Range(0.05f, 1f)]
        [Tooltip("Opacity multiplier used by ghost-preview sprites.")]
        public float ghostAlpha = 0.35f;

        [ReadOnlyIf(nameof(enableGhostPreview), false)]
        [Tooltip("Color multiplier applied to the ghost when placement is invalid.")]
        public Color invalidGhostMultiplier = new Color(1f, 0.35f, 0.35f, 1f);

        [BigHeader("Rules")]
        [Tooltip("When enabled, blocked cells are ignored while checking whether a row or column is full.")]
        public bool ignoreBlockedForLineFill = true;

        [BigHeader("Scoring")]
        [HelpBox("Move score = placement + adjusted clear score + perfect clear bonus. Adjusted clear score uses multi-line stages and combo streak display.", HelpBoxKind.Info)]
        [Min(0)]
        [Tooltip("Score awarded per cell in the placed piece, even if no clear happens.")]
        public int scorePerPlacedCell = 1;

        [Min(0)]
        [Tooltip("Base score awarded per cleared occupied cell.")]
        public int scorePerClearedCell = 2;

        [Min(0)]
        [Tooltip("Base bonus awarded per cleared line before stage multipliers.")]
        public int scorePerLineBonus = 10;

        [Min(0)]
        [Tooltip("Extra bonus awarded when the move clears the board completely.")]
        public int perfectClearBonus = 50;

        [Min(0)]
        [Tooltip("Extra bonus awarded when a move clears at least one row and one column together.")]
        public int mixedRowColumnBonus = 20;

        [Tooltip("Invoked whenever the total score changes. Sends the new total score.")]
        public UnityEvent<int> ScoreChanged;

        [GUIColor("cyan"), Tooltip("Optional TMP text updated directly with the current score.")]
        public TMP_Text ScoreText;

        [Line]
        public bool IsProgress;
        [HideIfAny(nameof(IsProgress), true, false)]
        public bool UseMetaCustomValue;
        [HideIfAny(nameof(IsProgress), true, false), InnerHint("(Optional)"), Tooltip("in this behavior, a progress bar controller attached")]
        public GameObject ProgressBar;
        [HideIfAny(nameof(IsProgress), true, false)]
        public int ProgressBarZeroPoint;
        [HideIfAny(nameof(IsProgress), true, false)]
        public UnityEvent OnProgressComplete;

        [BigHeader("Bonus")]
        [HelpBox("Bonus popups are spawned in sequence: Combo, Multi-line, then Score.", HelpBoxKind.Info)]
        [Min(0f)]
        [Tooltip("Delay between bonus popup spawns. Spawn order: Combo, Multi-line, Score.")]
        public float BonusSpawnStackDelay = 0.18f;

        [Min(0f), Tooltip("Maximum random spawn radius for combo, multi-line, and score popups, measured in board cells. 0 means spawn exactly at the chosen bonus center.")]
        public float stageFxMaxRadiusInCells = 0.9f;

        [Tooltip("Optional world-space target used to pull bonus TMP popups away from screen edges. When assigned, popups spawn between the blasted-line center and this target.")]
        public Transform BonusSpawnTarget;

        [Range(0f, 1f)]
        [Tooltip("Blend between blasted-line center and Bonus Spawn Target. 0 = blasted lines, 0.5 = halfway, 1 = target.")]
        public float BonusSpawnTargetBlend = 0.65f;

        [Line]
        [GUIColor(1f, 0.96f, 0.82f, 1f, true)]
        [Tooltip("TMP text prefab spawned while combo streak is active. Text is updated automatically as Combo x{streak}.")]
        public TMP_Text ComboTextPrefab;

        [GUIColor(1f, 0.96f, 0.82f, 1f, true)]
        [Tooltip("Format used for combo text. {0} = current combo streak.")]
        public string ComboTextFormat = "Combo x{0}";

        [Tooltip("Invoked whenever the combo streak changes. Sends the new streak value.")]
        public UnityEvent<int> ComboChanged;

        [Line]
        [GUIColor(1f, 0.96f, 0.82f, 1f, true)]
        [Tooltip("TMP text prefab spawned to show the score gained by the latest move.")]
        public TMP_Text ScoreTextPrefab;

        [GUIColor(1f, 0.96f, 0.82f, 1f, true)]
        [Tooltip("Format used for score popup text. {0} = score gained by the move.")]
        public string ScoreTextFormat = "+{0}";

        [GUIColor(1f, 0.96f, 0.82f, 1f, true)]
        [Tooltip("When enabled, the score popup only spawns on moves that clear/blast at least one line. The score is still added normally.")]
        public bool SpawnScoreTextOnlyOnClear;

        [Line]
        [Min(1)]
        [Tooltip("How many piece placements without a blast are allowed before the combo streak breaks. 1 = break immediately on the first no-blast placement.")]
        public int PlacementsWithoutBlastToBreakStreak = 1;

        [GUIColor(0.90f, 1f, 0.90f, 1f, true)]
        [Tooltip("Stages used when a single move clears multiple lines at once.")]
        public List<MultiLineStage> multiLineStages = new List<MultiLineStage>
{
    new MultiLineStage { id = "Double", requiredLines = 2, scoreMultiplier = 1.2f, flatBonus = 10 },
    new MultiLineStage { id = "Triple", requiredLines = 3, scoreMultiplier = 1.4f, flatBonus = 25 },
    new MultiLineStage { id = "Quad+", requiredLines = 4, scoreMultiplier = 1.7f, flatBonus = 50 }
};

        [Tooltip("Invoked when a multi-line stage is triggered by the move. Sends the multi-line stage index.")]
        public UnityEvent<int> MultiLineStageTriggered;

        [BigHeader("Database")]
        [Tooltip("When enabled, gained score is also written to the MOST database.")]
        public bool UseDatabase;

        [ReadOnlyIf(nameof(UseDatabase), false)]
        [Tooltip("The optional ScriptableObject database to read/write.")]
        public MOST_Database DataHolder;

        [ReadOnlyIf(nameof(UseDatabase), false)]
        [Tooltip("The key (DataName) inside the database.")]
        public string DataName;

        [BigHeader("Runtime")]
        [SerializeField, ReadOnly]
        [Tooltip("Current accumulated score.")]
        int _score;

        [SerializeField, ReadOnly]
        [Tooltip("True once no valid moves remain.")]
        bool _isGameOver;

        [SerializeField, ReadOnly]
        [Tooltip("Current consecutive clear streak.")]
        int _comboStreak;

        [SerializeField, ReadOnly]
        [Tooltip("How many placements happened without a blast while combo streak was active.")]
        int _placementsWithoutBlast;

        [SerializeField, ReadOnly]
        [Tooltip("Detailed breakdown of the most recently scored move.")]
        ScoreBreakdown _lastScoreBreakdown;

        public int Score => _score;
        public bool IsGameOver => _isGameOver;
        public int ComboStreak => _comboStreak;
        public int PlacementsWithoutBlast => _placementsWithoutBlast;
        public ScoreBreakdown LastScoreBreakdown => _lastScoreBreakdown;

        [BigHeader("Events")]

        [Tooltip("Invoked after a move clears at least one line.")]
        public UnityEvent OnClear;

        [Tooltip("Invoked when the game ends because no valid moves remain.")]
        public UnityEvent GameOver;
        #endregion

        #region Runtime State

        Transform _levelRoot;
        Transform _cellsRoot;
        Transform _previewRoot;
        Transform _fxRoot;

        int _cols;
        int _rows;
        float _cellSize;
        Vector3 _centerLocal;
        bool _useXZ;
        bool _flipSecondAxisByIndex;

        CellState[] _states;
        GameObject[] _cellObjects;
        SpriteRenderer[] _cellSR;

        bool[] _cellPreviewOverridden;
        Sprite[] _cellOrigSprite;
        Color[] _cellOrigColor;

        bool[] _afterMask;
        bool[] _rowFull;
        bool[] _colFull;

        BlockSkin[] _cellSkin;
        BlockSkin _fallbackOnClearSkin;

        Piece[] _tray;
        GameObject[] _trayRoots;

        bool _dragging;
        int _dragSlot = -1;
        Piece _dragPiece;
        GameObject _dragRoot;

        Transform _origParent;
        Vector3 _origLocalPos;
        Quaternion _origLocalRot;
        Vector3 _origLocalScale;

        Plane _boardPlane;
        Plane _dragPlane;

        Vector3 _dragVelocity;
        Vector3 _dragGrabOffset;

        Coroutine _animRoutine;
        bool _animating;

        Vector2Int _anchor;
        bool _valid;

        SpriteRenderer[] _dragSrs;
        int[] _dragOrigOrders;

        GhostUnit[] _ghostUnits;
        GameObject _ghostPrefabRef;

        SpriteRenderer _rowPreview;
        SpriteRenderer _colPreview;

        Camera _inputCamera;
        Quaternion _previewLocalRotation;
        bool _initialized;

        TMP_Text _comboTextInstance;

        TMP_Text _scoreTextInstance;
        Coroutine _bonusSpawnRoutine;

        #endregion

        #region Unity Lifecycle

        void Start()
        {
            if (_inputCamera == null) _inputCamera = Camera.main;
            TryAutoInit();
        }

        void Update()
        {
            if (_isGameOver || _animating) return;

            HandleInput();

            if (_dragging)
                UpdateDragging();
        }

        void TryAutoInit()
        {
            if (_initialized) return;
            if (shapeLibrary == null) return;

            if (levelMeta == null)
                levelMeta = FindObjectOfType<GridLevelData>();

            if (levelMeta == null) return;

            InitializeFromMeta(levelMeta);
        }

        void InitializeFromMeta(GridLevelData meta)
        {
            levelMeta = meta;
            _levelRoot = meta.transform;

            _cols = Mathf.Max(1, Mathf.RoundToInt(meta.LevelGridByCells.x));
            _rows = Mathf.Max(1, Mathf.RoundToInt(meta.LevelGridByCells.y));

            _cellSize = Mathf.Approximately(meta.CellWorldSize, 0f) ? 1f : meta.CellWorldSize;
            _centerLocal = meta.CenterPoint;

            _useXZ = (meta.Plane == GridLevelData.LevelPlane.XZ);
            _flipSecondAxisByIndex = meta.FlipSecondAxisByIndex;

            _cellsRoot = _levelRoot.Find("Cells");
            if (_cellsRoot == null)
            {
                var go = new GameObject("Cells");
                go.transform.SetParent(_levelRoot, false);
                _cellsRoot = go.transform;
            }

            _previewRoot = _levelRoot.Find("ClearPreview");
            if (_previewRoot == null)
            {
                var go = new GameObject("ClearPreview");
                go.transform.SetParent(_levelRoot, false);
                _previewRoot = go.transform;
            }

            _fxRoot = _levelRoot.Find("FX");
            if (_fxRoot == null)
            {
                var fxGo = new GameObject("FX");
                fxGo.transform.SetParent(_levelRoot, false);
                _fxRoot = fxGo.transform;
            }

            _states = new CellState[_cols * _rows];
            _cellObjects = new GameObject[_cols * _rows];
            _cellSR = new SpriteRenderer[_cols * _rows];

            _cellPreviewOverridden = new bool[_cols * _rows];
            _cellOrigSprite = new Sprite[_cols * _rows];
            _cellOrigColor = new Color[_cols * _rows];

            _afterMask = new bool[_cols * _rows];
            _rowFull = new bool[_rows];
            _colFull = new bool[_cols];

            _cellSkin = new BlockSkin[_cols * _rows];
            _fallbackOnClearSkin = FindFallbackOnClearSkin();

            if (meta.CellStates != null && meta.CellStates.Length == _states.Length)
            {
                for (int i = 0; i < _states.Length; i++)
                    _states[i] = (CellState)meta.CellStates[i];
            }
            else
            {
                for (int i = 0; i < _states.Length; i++)
                    _states[i] = CellState.Empty;

                ScanOccupiedFromCellsRoot();
            }

            CaptureOccupiedObjects();
            AssignSkinsToExistingOccupied();

            _previewLocalRotation = _useXZ
                ? Quaternion.LookRotation(Vector3.up, Vector3.forward)
                : Quaternion.identity;

            EnsureClearPreviewObjects();
            HideClearPreview();

            BuildTray();

            _score = 0;
            _comboStreak = 0;
            _placementsWithoutBlast = 0;
            DestroyComboText();

            if (_bonusSpawnRoutine != null)
            {
                StopCoroutine(_bonusSpawnRoutine);
                _bonusSpawnRoutine = null;
            }

            _lastScoreBreakdown = default;
            _isGameOver = false;

            //ScoreChanged?.Invoke(_score);
            //ComboChanged?.Invoke(_comboStreak);

            if (ScoreText != null)
                ScoreText.text = _score.ToString();

            ClearGhost();
            RestoreAllPreviewedCells();

            _initialized = true;
        }

        #endregion

        #region Board Mapping

        void ScanOccupiedFromCellsRoot()
        {
            for (int i = 0; i < _cellsRoot.childCount; i++)
            {
                var t = _cellsRoot.GetChild(i);
                if (!TryWorldToCell(t.position, out var cell)) continue;
                _states[Index(cell.x, cell.y)] = CellState.Occupied;
            }
        }

        void CaptureOccupiedObjects()
        {
            for (int i = 0; i < _cellsRoot.childCount; i++)
            {
                var t = _cellsRoot.GetChild(i);
                if (!TryWorldToCell(t.position, out var cell)) continue;

                int idx = Index(cell.x, cell.y);
                if (_states[idx] != CellState.Occupied) continue;

                _cellObjects[idx] = t.gameObject;
                _cellSR[idx] = t.GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        bool InBounds(int x, int y) => x >= 0 && x < _cols && y >= 0 && y < _rows;
        int Index(int x, int y) => y * _cols + x;

        Vector3 AxisX => Vector3.right * _cellSize;
        Vector3 Axis2 => _useXZ ? (Vector3.forward * _cellSize) : (Vector3.up * _cellSize);

        Vector3 CellToLocal(int x, int y)
        {
            int yIndex = _flipSecondAxisByIndex ? (_rows - 1 - y) : y;

            return _centerLocal
                 + AxisX * (x - (_cols - 1) * 0.5f)
                 + Axis2 * (yIndex - (_rows - 1) * 0.5f);
        }

        Vector3 CellToWorld(int x, int y) => _levelRoot.TransformPoint(CellToLocal(x, y));

        bool TryWorldToCell(Vector3 worldPos, out Vector2Int cell)
        {
            Vector3 local = _levelRoot.InverseTransformPoint(worldPos);
            return TryLocalToCell(local, out cell);
        }

        bool TryLocalToCell(Vector3 localPos, out Vector2Int cell)
        {
            cell = default;

            Vector3 rel = localPos - _centerLocal;

            float cx = (_cols - 1) * 0.5f;
            float cy = (_rows - 1) * 0.5f;

            float fx = (rel.x / _cellSize) + cx;

            float f2 = _useXZ
                ? (rel.z / _cellSize) + cy
                : (rel.y / _cellSize) + cy;

            int x = Mathf.RoundToInt(fx);
            int yIndex = Mathf.RoundToInt(f2);
            int y = _flipSecondAxisByIndex ? (_rows - 1 - yIndex) : yIndex;

            cell = new Vector2Int(x, y);
            return InBounds(x, y);
        }

        Vector2Int WorldToCellNearest(Vector3 worldPos)
        {
            Vector3 local = _levelRoot.InverseTransformPoint(worldPos);
            Vector3 rel = local - _centerLocal;

            float cx = (_cols - 1) * 0.5f;
            float cy = (_rows - 1) * 0.5f;

            float fx = (rel.x / _cellSize) + cx;

            float f2 = _useXZ
                ? (rel.z / _cellSize) + cy
                : (rel.y / _cellSize) + cy;

            int x = Mathf.RoundToInt(fx);
            int yIndex = Mathf.RoundToInt(f2);
            int y = _flipSecondAxisByIndex ? (_rows - 1 - yIndex) : yIndex;

            return new Vector2Int(x, y);
        }

        #endregion

        #region Tray

        void BuildTray()
        {
            int n = Mathf.Max(1, traySize);

            int available = traySlotAnchors != null ? traySlotAnchors.Length : 0;
            if (available <= 0)
            {
                Debug.LogError("[BlockBlastGame] traySlotAnchors is empty. Assign anchors in inspector.");
                n = 0;
            }
            else if (available < n)
            {
                Debug.LogWarning($"[BlockBlastGame] traySlotAnchors has {available} anchors but traySize is {n}. Using {available}.");
                n = available;
            }

            _tray = new Piece[n];
            _trayRoots = new GameObject[n];

            for (int i = 0; i < n; i++)
                CreateOrReplaceTrayPiece(i);

            TrayChanged?.Invoke();
        }

        void CreateOrReplaceTrayPiece(int slot)
        {
            if (_trayRoots[slot] != null)
                Destroy(_trayRoots[slot]);

            _tray[slot] = CreateRandomPieceSafe();
            if (!_tray[slot].IsValid || _tray[slot].skin == null || _tray[slot].skin.unitPrefab == null)
            {
                _tray[slot] = default;
                _trayRoots[slot] = null;
                return;
            }

            Transform anchor = traySlotAnchors[slot];
            if (anchor == null)
            {
                Debug.LogWarning($"[BlockBlastGame] traySlotAnchors[{slot}] is null.");
                _tray[slot] = default;
                _trayRoots[slot] = null;
                return;
            }

            var root = new GameObject($"TrayPiece_{slot}");
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * trayPieceScale;

            var piece = _tray[slot];
            var unitPrefab = piece.skin.unitPrefab;

            for (int i = 0; i < piece.relOffsets.Length; i++)
            {
                var u = Instantiate(unitPrefab);
                u.transform.SetParent(root.transform, false);
                u.transform.localPosition = OffsetToLocal(piece.relOffsets[i]);
                DisableAllColliders(u);
            }

            Vector3 min = OffsetToLocal(piece.relOffsets[0]);
            Vector3 max = min;
            for (int i = 1; i < piece.relOffsets.Length; i++)
            {
                Vector3 p = OffsetToLocal(piece.relOffsets[i]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 center = (min + max) * 0.5f;
            root.transform.localPosition = -center * trayPieceScale;

            AddOrUpdatePieceCollider(root, piece);
            _trayRoots[slot] = root;
        }

        Piece CreateRandomPieceSafe(int maxAttempts = 25)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!shapeLibrary.TryPickRandom(out var shape, out int rot))
                    break;

                if (shape == null) continue;

                var offsets = shape.GetFilledOffsets(rot);
                if (offsets == null || offsets.Length == 0) continue;

                var size = ComputeSize(offsets);
                var pivot = new Vector2Int(
                    Mathf.RoundToInt((size.x - 1) * 0.5f),
                    Mathf.RoundToInt((size.y - 1) * 0.5f)
                );

                var rel = new Vector2Int[offsets.Length];
                for (int i = 0; i < offsets.Length; i++)
                    rel[i] = offsets[i] - pivot;

                if (!TryPickRandomSkinSafe(out BlockSkin skin))
                    continue;

                return new Piece
                {
                    shapeId = shape.id,
                    rotationDeg = rot,
                    relOffsets = rel,
                    size = size,
                    skin = skin
                };
            }

            return default;
        }

        bool TryPickRandomSkinSafe(out BlockSkin skin)
        {
            skin = null;

            if (shapeLibrary == null)
                return false;

            if (shapeLibrary.TryPickRandomSkin(out skin) && skin != null && skin.unitPrefab != null)
                return true;

            var skins = shapeLibrary.Skins;
            if (skins == null) return false;

            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                if (s != null && s.unitPrefab != null)
                {
                    skin = s;
                    return true;
                }
            }

            return false;
        }

        static Vector2Int ComputeSize(Vector2Int[] pts)
        {
            int maxX = pts[0].x, maxY = pts[0].y;
            for (int i = 1; i < pts.Length; i++)
            {
                if (pts[i].x > maxX) maxX = pts[i].x;
                if (pts[i].y > maxY) maxY = pts[i].y;
            }
            return new Vector2Int(maxX + 1, maxY + 1);
        }

        Vector3 OffsetToLocal(Vector2Int o)
        {
            int dy = _flipSecondAxisByIndex ? -o.y : o.y;

            return _useXZ
                ? new Vector3(o.x * _cellSize, 0f, dy * _cellSize)
                : new Vector3(o.x * _cellSize, dy * _cellSize, 0f);
        }

        void AddOrUpdatePieceCollider(GameObject root, Piece piece)
        {
            var bc = root.GetComponent<BoxCollider>();
            if (bc == null) bc = root.AddComponent<BoxCollider>();

            Vector3 min = OffsetToLocal(piece.relOffsets[0]);
            Vector3 max = min;

            for (int i = 1; i < piece.relOffsets.Length; i++)
            {
                Vector3 p = OffsetToLocal(piece.relOffsets[i]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 center = (min + max) * 0.5f;
            float thickness = _cellSize * 0.35f;

            if (_useXZ)
            {
                bc.center = new Vector3(center.x, 0f, center.z);
                bc.size = new Vector3((max.x - min.x) + _cellSize, thickness, (max.z - min.z) + _cellSize);
            }
            else
            {
                bc.center = new Vector3(center.x, center.y, 0f);
                bc.size = new Vector3((max.x - min.x) + _cellSize, (max.y - min.y) + _cellSize, thickness);
            }
        }

        #endregion

        #region Input + Dragging

        void HandleInput()
        {
            if (_inputCamera == null) return;

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) TryBeginDrag(t.position);
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) TryEndDrag();
                return;
            }

            if (Input.GetMouseButtonDown(0)) TryBeginDrag(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) TryEndDrag();
        }

        int RayPickTraySlot(Vector2 screenPos)
        {
            Ray ray = _inputCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 1000f)) return -1;

            Transform ht = hit.transform;
            if (ht == null) return -1;

            for (int i = 0; i < _trayRoots.Length; i++)
            {
                var root = _trayRoots[i];
                if (root == null) continue;
                if (ht == root.transform || ht.IsChildOf(root.transform)) return i;
            }

            return -1;
        }

        static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        bool TryGetPointerWorldOnPlane(Plane plane, out Vector3 worldPoint)
        {
            worldPoint = default;

            Vector2 screenPos = (Input.touchCount > 0)
                ? Input.GetTouch(0).position
                : (Vector2)Input.mousePosition;

            Ray ray = _inputCamera.ScreenPointToRay(screenPos);

            if (!plane.Raycast(ray, out float enter))
                return false;

            worldPoint = ray.GetPoint(enter);
            return true;
        }

        Vector2Int ComputeAnchorFromDraggedUnits(Piece piece, GameObject dragRoot)
        {
            int n = Mathf.Min(piece.relOffsets.Length, dragRoot.transform.childCount);
            if (n <= 0)
            {
                Vector3 p = _boardPlane.ClosestPointOnPlane(dragRoot.transform.position);
                return WorldToCellNearest(p);
            }

            int refIndex = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var o = piece.relOffsets[i];
                int d = Mathf.Abs(o.x) + Mathf.Abs(o.y);
                if (d < bestDist) { bestDist = d; refIndex = i; }
            }

            Transform unit = dragRoot.transform.GetChild(refIndex);
            Vector3 projected = _boardPlane.ClosestPointOnPlane(unit.position);
            Vector2Int unitCell = WorldToCellNearest(projected);

            return unitCell - piece.relOffsets[refIndex];
        }

        void CacheAndSetDragSorting(GameObject root)
        {
            _dragSrs = root.GetComponentsInChildren<SpriteRenderer>(true);
            _dragOrigOrders = new int[_dragSrs.Length];

            for (int i = 0; i < _dragSrs.Length; i++)
            {
                _dragOrigOrders[i] = _dragSrs[i].sortingOrder;
                _dragSrs[i].sortingOrder = dragSortingOrder;
            }
        }

        void RestoreDragSorting()
        {
            if (_dragSrs == null || _dragOrigOrders == null) return;

            int n = Mathf.Min(_dragSrs.Length, _dragOrigOrders.Length);
            for (int i = 0; i < n; i++)
                if (_dragSrs[i] != null)
                    _dragSrs[i].sortingOrder = _dragOrigOrders[i];

            _dragSrs = null;
            _dragOrigOrders = null;
        }

        void TryBeginDrag(Vector2 screenPos)
        {
            if (_dragging || _animating) return;

            int slot = RayPickTraySlot(screenPos);
            if (slot < 0) return;
            if (!_tray[slot].IsValid) return;
            if (_trayRoots[slot] == null) return;

            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
                _animating = false;
            }

            _dragging = true;
            _dragSlot = slot;
            _dragPiece = _tray[slot];
            _dragRoot = _trayRoots[slot];

            _origParent = _dragRoot.transform.parent;
            _origLocalPos = _dragRoot.transform.localPosition;
            _origLocalRot = _dragRoot.transform.localRotation;
            _origLocalScale = _dragRoot.transform.localScale;

            _dragRoot.transform.localScale = Vector3.one;
            _dragRoot.transform.SetParent(_levelRoot, true);

            Vector3 boardNormal = _levelRoot.TransformDirection(_useXZ ? Vector3.up : Vector3.forward);
            Vector3 boardCenterWorld = _levelRoot.TransformPoint(_centerLocal);

            _boardPlane = new Plane(boardNormal, boardCenterWorld);
            _dragPlane = new Plane(boardNormal, boardCenterWorld + boardNormal * (dragLiftInCells * _cellSize));

            if (TryGetPointerWorldOnPlane(_dragPlane, out var p0))
                _dragGrabOffset = _dragRoot.transform.position - p0;
            else
                _dragGrabOffset = Vector3.zero;

            _dragVelocity = Vector3.zero;

            CacheAndSetDragSorting(_dragRoot);

            if (enableGhostPreview)
                EnsureGhostUnits(_dragPiece);
        }

        void UpdateDragging()
        {
            if (_dragRoot == null) return;

            if (TryGetPointerWorldOnPlane(_dragPlane, out var dragPoint))
            {
                Vector3 target = dragPoint + _dragGrabOffset;

                _dragRoot.transform.position = Vector3.SmoothDamp(
                    _dragRoot.transform.position,
                    target,
                    ref _dragVelocity,
                    dragSmoothTime,
                    dragMaxSpeed
                );
            }

            _anchor = ComputeAnchorFromDraggedUnits(_dragPiece, _dragRoot);
            _valid = CanPlace(_dragPiece, _anchor);

            if (enableGhostPreview)
                UpdateGhostForPiece(_dragPiece, _anchor, _valid);

            UpdateClearPreviewAndCellSkinOverride(_dragPiece, _anchor, _valid);
        }

        void TryEndDrag()
        {
            if (!_dragging || _animating) return;
            if (_dragRoot == null) { ResetDrag(); return; }

            int slot = _dragSlot;
            var piece = _dragPiece;
            var anchor = _anchor;
            var root = _dragRoot;

            var origParent = _origParent;
            var origLocalPos = _origLocalPos;
            var origLocalRot = _origLocalRot;
            var origLocalScale = _origLocalScale;

            bool valid = _valid;

            _dragging = false;

            ClearGhost();
            if (!valid)
            {
                HideClearPreview();
                RestoreAllPreviewedCells();
            }

            ResetDrag();

            if (_animRoutine != null) StopCoroutine(_animRoutine);

            if (valid)
                _animRoutine = StartCoroutine(DropCommitRoutine(slot, piece, anchor, root));
            else
                _animRoutine = StartCoroutine(ReturnToTrayRoutine(root, origParent, origLocalPos, origLocalRot, origLocalScale));
        }

        void ResetDrag()
        {
            _dragSlot = -1;
            _dragPiece = default;
            _dragRoot = null;
            _valid = false;

            _dragVelocity = Vector3.zero;
            _dragGrabOffset = Vector3.zero;
        }

        bool CanPlace(Piece piece, Vector2Int anchor)
        {
            for (int i = 0; i < piece.relOffsets.Length; i++)
            {
                int x = anchor.x + piece.relOffsets[i].x;
                int y = anchor.y + piece.relOffsets[i].y;

                if (!InBounds(x, y)) return false;
                if (_states[Index(x, y)] != CellState.Empty) return false;
            }

            return true;
        }

        IEnumerator DropCommitRoutine(int slot, Piece piece, Vector2Int anchor, GameObject root)
        {
            _animating = true;

            Vector3 startPos = root.transform.position;
            Vector3 targetPos = CellToWorld(anchor.x, anchor.y);

            float t = 0f;
            float dur = Mathf.Max(0.001f, dropSnapTime);

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float e = EaseOutCubic(t);
                root.transform.position = Vector3.LerpUnclamped(startPos, targetPos, e);
                yield return null;
            }

            PlacementSnapshot placement = CommitDraggedPieceToBoard(piece, anchor, root);

            _trayRoots[slot] = null;
            _tray[slot] = default;

            ClearResolution clearResult = ResolveLinesAndBlast(piece.skin);
            ApplyMoveScore(placement, clearResult);

            if (clearResult.AnyClear)
                OnClear?.Invoke();

            HideClearPreview();
            RestoreAllPreviewedCells();

            if (AllTrayEmpty())
            {
                for (int i = 0; i < _tray.Length; i++)
                    CreateOrReplaceTrayPiece(i);

                TrayChanged?.Invoke();
            }

            if (!AnyMoveAvailable())
            {
                _isGameOver = true;
                GameOver?.Invoke();
            }

            _animating = false;
            _animRoutine = null;
        }

        IEnumerator ReturnToTrayRoutine(GameObject root, Transform origParent, Vector3 origLocalPos, Quaternion origLocalRot, Vector3 origLocalScale)
        {
            _animating = true;

            Vector3 startPos = root.transform.position;
            Vector3 targetPos = (origParent != null) ? origParent.TransformPoint(origLocalPos) : startPos;

            Quaternion startRot = root.transform.rotation;
            Quaternion targetRot = (origParent != null) ? origParent.rotation : startRot;

            Vector3 startScale = root.transform.localScale;
            Vector3 targetScale = origLocalScale;

            float t = 0f;
            float dur = Mathf.Max(0.001f, returnToTrayTime);

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float e = EaseOutCubic(t);

                root.transform.position = Vector3.LerpUnclamped(startPos, targetPos, e);
                root.transform.rotation = Quaternion.SlerpUnclamped(startRot, targetRot, e);
                root.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, e);

                yield return null;
            }

            root.transform.SetParent(origParent, false);
            root.transform.localPosition = origLocalPos;
            root.transform.localRotation = origLocalRot;
            root.transform.localScale = origLocalScale;

            RestoreDragSorting();

            _animating = false;
            _animRoutine = null;
        }

        #endregion

        #region Placement + Clearing + Scoring

        PlacementSnapshot CommitDraggedPieceToBoard(Piece piece, Vector2Int anchor, GameObject dragRoot)
        {
            RestoreDragSorting();

            PlacementSnapshot snapshot = default;

            int n = Mathf.Min(piece.relOffsets.Length, dragRoot.transform.childCount);
            if (n <= 0)
            {
                Destroy(dragRoot);
                return snapshot;
            }

            Vector3 placedCenterSum = Vector3.zero;

            var blocks = new Transform[n];
            for (int i = 0; i < n; i++)
                blocks[i] = dragRoot.transform.GetChild(i);

            for (int i = 0; i < n; i++)
            {
                int x = anchor.x + piece.relOffsets[i].x;
                int y = anchor.y + piece.relOffsets[i].y;
                int idx = Index(x, y);

                _states[idx] = CellState.Occupied;
                _cellSkin[idx] = piece.skin != null ? piece.skin : _fallbackOnClearSkin;

                Vector3 worldPos = CellToWorld(x, y);

                Transform b = blocks[i];
                b.SetParent(_cellsRoot, true);
                b.position = worldPos;

                DisableAllColliders(b.gameObject);

                _cellObjects[idx] = b.gameObject;
                _cellSR[idx] = b.GetComponentInChildren<SpriteRenderer>(true);

                if (!string.IsNullOrEmpty(BroadcastPlacedBlocks)) b.gameObject.BroadcastMessage(BroadcastPlacedBlocks, SendMessageOptions.DontRequireReceiver);

                placedCenterSum += worldPos;
            }

            snapshot.placedCells = n;
            snapshot.centerWorld = placedCenterSum / n;

            Destroy(dragRoot);
            return snapshot;
        }

        ClearResolution ResolveLinesAndBlast(BlockSkin clearingSkin = null)
        {
            var result = new ClearResolution();

            bool[] clear = new bool[_cols * _rows];
            int rowsCleared = 0;
            int colsCleared = 0;

            for (int y = 0; y < _rows; y++)
            {
                bool anyUsable = false;
                bool full = true;

                for (int x = 0; x < _cols; x++)
                {
                    var st = GetState(x, y);
                    if (st == CellState.Blocked && ignoreBlockedForLineFill) continue;

                    anyUsable = true;
                    if (st != CellState.Occupied) { full = false; break; }
                }

                if (anyUsable && full)
                {
                    rowsCleared++;

                    for (int x = 0; x < _cols; x++)
                        if (GetState(x, y) == CellState.Occupied)
                            clear[Index(x, y)] = true;
                }
            }

            for (int x = 0; x < _cols; x++)
            {
                bool anyUsable = false;
                bool full = true;

                for (int y = 0; y < _rows; y++)
                {
                    var st = GetState(x, y);
                    if (st == CellState.Blocked && ignoreBlockedForLineFill) continue;

                    anyUsable = true;
                    if (st != CellState.Occupied) { full = false; break; }
                }

                if (anyUsable && full)
                {
                    colsCleared++;

                    for (int y = 0; y < _rows; y++)
                        if (GetState(x, y) == CellState.Occupied)
                            clear[Index(x, y)] = true;
                }
            }

            int cleared = 0;
            Vector3 clearCenterSum = Vector3.zero;

            for (int i = 0; i < clear.Length; i++)
            {
                if (!clear[i]) continue;

                Vector3 pos;
                if (_cellObjects[i] != null)
                    pos = _cellObjects[i].transform.position;
                else
                {
                    int x = i % _cols;
                    int y = i / _cols;
                    pos = CellToWorld(x, y);
                }

                clearCenterSum += pos;

                SpawnOnClearFX(i, clearingSkin);

                if (_cellObjects[i] != null)
                    Destroy(_cellObjects[i]);

                _cellObjects[i] = null;
                _cellSR[i] = null;
                _cellSkin[i] = null;
                _states[i] = CellState.Empty;
                _cellPreviewOverridden[i] = false;

                cleared++;
            }

            result.rowsCleared = rowsCleared;
            result.colsCleared = colsCleared;
            result.clearedCells = cleared;
            result.hasClearCenter = cleared > 0;
            result.clearCenterWorld = cleared > 0
                ? clearCenterSum / cleared
                : _levelRoot.TransformPoint(_centerLocal);
            result.perfectClear = cleared > 0 && IsBoardEmpty();

            return result;
        }

        void ApplyMoveScore(PlacementSnapshot placement, ClearResolution clearResult)
        {
            _lastScoreBreakdown = BuildScoreBreakdown(placement, clearResult);

            int gainedScore = _lastScoreBreakdown.totalScore;
            AddScore(gainedScore);

            if (_bonusSpawnRoutine != null)
                StopCoroutine(_bonusSpawnRoutine);

            _bonusSpawnRoutine = StartCoroutine(BonusSpawnStackRoutine(placement, clearResult, gainedScore));

            ComboChanged?.Invoke(_comboStreak);
        }

        ScoreBreakdown BuildScoreBreakdown(PlacementSnapshot placement, ClearResolution clearResult)
        {
            var breakdown = new ScoreBreakdown
            {
                placedCells = placement.placedCells,
                clearedCells = clearResult.clearedCells,
                rowsCleared = clearResult.rowsCleared,
                colsCleared = clearResult.colsCleared,
                lines = clearResult.LineCount,
                placementScore = placement.placedCells * scorePerPlacedCell,

                multiLineStageIndex = -1,
                multiLineStageMultiplier = 1f,
                multiLineFlatBonus = 0,
                patternBonus = 0
            };

            if (clearResult.LineCount > 0)
            {
                _comboStreak++;
                _placementsWithoutBlast = 0;
            }
            else if (_comboStreak > 0)
            {
                _placementsWithoutBlast++;

                int breakLimit = Mathf.Max(1, PlacementsWithoutBlastToBreakStreak);
                if (_placementsWithoutBlast >= breakLimit)
                {
                    _comboStreak = 0;
                    _placementsWithoutBlast = 0;
                }
            }
            else
            {
                _placementsWithoutBlast = 0;
            }

            breakdown.streak = _comboStreak;

            if (clearResult.LineCount > 0)
            {
                breakdown.baseClearScore =
                    (clearResult.clearedCells * scorePerClearedCell) +
                    (clearResult.LineCount * scorePerLineBonus);

                breakdown.multiLineStageIndex = GetMultiLineStageIndex(clearResult.LineCount);
                breakdown.multiLineStageMultiplier = GetMultiLineStageMultiplier(breakdown.multiLineStageIndex);
                breakdown.multiLineFlatBonus = GetMultiLineStageFlatBonus(breakdown.multiLineStageIndex);
                breakdown.patternBonus = clearResult.IsCrossClear ? mixedRowColumnBonus : 0;

                breakdown.finalClearScore =
                    Mathf.RoundToInt(breakdown.baseClearScore * breakdown.multiLineStageMultiplier) +
                    breakdown.multiLineFlatBonus +
                    breakdown.patternBonus;
            }

            breakdown.perfectClearBonus = clearResult.perfectClear ? perfectClearBonus : 0;
            breakdown.totalScore =
                breakdown.placementScore +
                breakdown.finalClearScore +
                breakdown.perfectClearBonus;

            return breakdown;
        }
        void AddScore(int amount)
        {
            if (amount <= 0) return;

            _score += amount;
            ScoreChanged?.Invoke(_score);

            if (ScoreText != null)
                ScoreText.text = _score.ToString();

            if (UseDatabase && DataHolder != null && !string.IsNullOrWhiteSpace(DataName))
            {
                var d = DataHolder.Get<IntData>(DataName);
                d?.Add(amount);
            }

            if (IsProgress)
            {
                if (UseMetaCustomValue)
                {
                    if (ProgressBar) ProgressBar.transform.localPosition = Mathf.Min(ProgressBarZeroPoint * (_score / levelMeta.CustomValue) - ProgressBarZeroPoint, 0) * Vector3.right;
                }
            }
        }

        int GetMultiLineStageIndex(int lines)
        {
            if (lines < 2 || multiLineStages == null || multiLineStages.Count == 0)
                return -1;

            int bestIndex = -1;
            int bestRequired = int.MinValue;

            for (int i = 0; i < multiLineStages.Count; i++)
            {
                var stage = multiLineStages[i];
                if (stage == null) continue;

                int req = Mathf.Max(2, stage.requiredLines);
                if (lines >= req && req > bestRequired)
                {
                    bestRequired = req;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        float GetMultiLineStageMultiplier(int stageIndex)
        {
            if (stageIndex < 0 || multiLineStages == null || stageIndex >= multiLineStages.Count)
                return 1f;

            var stage = multiLineStages[stageIndex];
            return stage != null ? Mathf.Max(1f, stage.scoreMultiplier) : 1f;
        }

        int GetMultiLineStageFlatBonus(int stageIndex)
        {
            if (stageIndex < 0 || multiLineStages == null || stageIndex >= multiLineStages.Count)
                return 0;

            var stage = multiLineStages[stageIndex];
            return stage != null ? Mathf.Max(0, stage.flatBonus) : 0;
        }

        CellState GetState(int x, int y)
        {
            if (!InBounds(x, y)) return CellState.Blocked;
            return _states[Index(x, y)];
        }

        bool IsBoardEmpty()
        {
            for (int i = 0; i < _states.Length; i++)
                if (_states[i] == CellState.Occupied)
                    return false;

            return true;
        }

        bool AllTrayEmpty()
        {
            for (int i = 0; i < _tray.Length; i++)
                if (_tray[i].IsValid) return false;
            return true;
        }

        bool AnyMoveAvailable()
        {
            for (int i = 0; i < _tray.Length; i++)
            {
                if (!_tray[i].IsValid) continue;
                if (PieceFitsAnywhere(_tray[i])) return true;
            }

            return false;
        }

        bool PieceFitsAnywhere(Piece piece)
        {
            for (int y = 0; y < _rows; y++)
                for (int x = 0; x < _cols; x++)
                    if (CanPlace(piece, new Vector2Int(x, y)))
                        return true;

            return false;
        }

        #endregion

        #region Stage FX
        void UpdateComboText(PlacementSnapshot placement, ClearResolution clearResult)
        {
            bool blastedThisMove = clearResult.AnyClear && clearResult.LineCount > 0;

            if (_comboStreak < 2)
            {
                DestroyComboText();
                return;
            }

            if (ComboTextPrefab == null)
                return;

            if (!blastedThisMove)
            {
                if (_comboTextInstance != null)
                    SetComboTextValue();

                return;
            }

            Vector3 fallback = clearResult.hasClearCenter
                ? clearResult.clearCenterWorld
                : _levelRoot.TransformPoint(_centerLocal);

            Vector3 pos = GetRandomStageFxPosition(placement, clearResult, fallback);

            if (_comboTextInstance == null)
                _comboTextInstance = Instantiate(ComboTextPrefab, pos, _levelRoot.rotation, _fxRoot);
            else
                _comboTextInstance.transform.SetPositionAndRotation(pos, _levelRoot.rotation);

            SetComboTextValue();
        }
        IEnumerator BonusSpawnStackRoutine(PlacementSnapshot placement, ClearResolution clearResult, int gainedScore)
        {
            float delay = Mathf.Max(0f, BonusSpawnStackDelay);

            // 1) Combo first
            bool shouldSpawnCombo =
                clearResult.AnyClear &&
                clearResult.LineCount > 0 &&
                _comboStreak >= 2 &&
                ComboTextPrefab != null;

            if (shouldSpawnCombo)
            {
                UpdateComboText(placement, clearResult);

                if (delay > 0f)
                    yield return new WaitForSeconds(delay);
            }
            else
            {
                // No-blast placements may keep the streak alive, but should not spawn/reposition combo.
                if (_comboStreak < 2)
                    DestroyComboText();
                else if (_comboTextInstance != null)
                    SetComboTextValue();
            }

            // 2) Multi-line second
            bool shouldSpawnMultiLine =
                clearResult.IsMultiLine &&
                _lastScoreBreakdown.multiLineStageIndex >= 0;

            if (shouldSpawnMultiLine)
            {
                TrySpawnMultiLineFX(placement, clearResult, _lastScoreBreakdown.multiLineStageIndex);

                if (delay > 0f)
                    yield return new WaitForSeconds(delay);
            }

            // 3) Score last
            bool shouldSpawnScore =
                gainedScore > 0 &&
                ScoreTextPrefab != null &&
                (!SpawnScoreTextOnlyOnClear || clearResult.AnyClear);

            if (shouldSpawnScore)
            {
                UpdateScoreText(placement, clearResult, gainedScore);
            }
            else
            {
                DestroyScoreText();
            }

            _bonusSpawnRoutine = null;
        }

        void UpdateScoreText(PlacementSnapshot placement, ClearResolution clearResult, int gainedScore)
        {
            if (gainedScore <= 0)
            {
                DestroyScoreText();
                return;
            }

            if (ScoreTextPrefab == null)
                return;

            Vector3 fallback = clearResult.hasClearCenter
                ? clearResult.clearCenterWorld
                : _levelRoot.TransformPoint(_centerLocal);

            Vector3 pos = GetRandomStageFxPosition(placement, clearResult, fallback);

            if (_scoreTextInstance == null)
                _scoreTextInstance = Instantiate(ScoreTextPrefab, pos, _levelRoot.rotation, _fxRoot);
            else
                _scoreTextInstance.transform.SetPositionAndRotation(pos, _levelRoot.rotation);

            SetScoreTextValue(gainedScore);
        }

        void SetScoreTextValue(int gainedScore)
        {
            if (_scoreTextInstance == null)
                return;

            string format = string.IsNullOrWhiteSpace(ScoreTextFormat)
                ? "+{0}"
                : ScoreTextFormat;

            _scoreTextInstance.text = string.Format(format, gainedScore);
        }

        void DestroyScoreText()
        {
            if (_scoreTextInstance == null)
                return;

            Destroy(_scoreTextInstance.gameObject);
            _scoreTextInstance = null;
        }

        void SetComboTextValue()
        {
            if (_comboTextInstance == null)
                return;

            string format = string.IsNullOrWhiteSpace(ComboTextFormat)
                ? "Combo x{0}"
                : ComboTextFormat;

            _comboTextInstance.text = string.Format(format, _comboStreak);
        }

        void DestroyComboText()
        {
            if (_comboTextInstance == null)
                return;

            Destroy(_comboTextInstance.gameObject);
            _comboTextInstance = null;
        }

        void TrySpawnMultiLineFX(PlacementSnapshot placement, ClearResolution clearResult, int stageIndex)
        {
            if (stageIndex < 0 || multiLineStages == null || stageIndex >= multiLineStages.Count)
                return;

            var stage = multiLineStages[stageIndex];
            if (stage == null)
                return;

            MultiLineStageTriggered?.Invoke(stageIndex);

            if (stage.effectPrefab == null)
                return;

            Vector3 fallback = clearResult.hasClearCenter
                ? clearResult.clearCenterWorld
                : _levelRoot.TransformPoint(_centerLocal);

            Vector3 pos = GetRandomStageFxPosition(placement, clearResult, fallback);
            Instantiate(stage.effectPrefab, pos, _levelRoot.rotation, _fxRoot);
        }

        Vector3 GetRandomStageFxPosition(PlacementSnapshot placement, ClearResolution clearResult, Vector3 fallbackCenterWorld)
        {
            Vector3 originalCenterWorld = placement.HasPlacedCells
                ? placement.centerWorld
                : fallbackCenterWorld;

            Vector3 centerWorld = originalCenterWorld;

            if (BonusSpawnTarget != null)
            {
                Vector3 blastCenterWorld = clearResult.hasClearCenter
                    ? clearResult.clearCenterWorld
                    : originalCenterWorld;

                centerWorld = Vector3.Lerp(
                    blastCenterWorld,
                    BonusSpawnTarget.position,
                    Mathf.Clamp01(BonusSpawnTargetBlend)
                );
            }

            float radiusWorld = Mathf.Max(0f, stageFxMaxRadiusInCells) * _cellSize;
            if (radiusWorld <= 0f || _levelRoot == null)
                return centerWorld;

            Vector2 rnd = UnityEngine.Random.insideUnitCircle * radiusWorld;

            Vector3 axisXWorld = _levelRoot.TransformDirection(Vector3.right);
            Vector3 axis2World = _levelRoot.TransformDirection(_useXZ ? Vector3.forward : Vector3.up);

            return centerWorld + axisXWorld * rnd.x + axis2World * rnd.y;
        }

        #endregion

        #region Ghost Preview

        void EnsureGhostUnits(Piece piece)
        {
            if (piece.skin == null || piece.skin.unitPrefab == null)
            {
                ClearGhost();
                return;
            }

            var unitPrefab = piece.skin.unitPrefab;

            if (_ghostUnits != null && _ghostUnits.Length == piece.Count && _ghostPrefabRef == unitPrefab)
                return;

            ClearGhost();

            _ghostPrefabRef = unitPrefab;
            _ghostUnits = new GhostUnit[piece.Count];

            for (int i = 0; i < piece.Count; i++)
            {
                var g = Instantiate(unitPrefab);
                g.name = $"Ghost_{i}";
                g.transform.SetParent(_levelRoot, true);

                DisableAllColliders(g);
                SetSortingOrderRecursive(g.transform, ghostSortingOrder);

                var srs = g.GetComponentsInChildren<SpriteRenderer>(true);
                var baseCols = new Color[srs.Length];
                for (int j = 0; j < srs.Length; j++)
                    baseCols[j] = srs[j].color;

                g.SetActive(false);

                _ghostUnits[i] = new GhostUnit
                {
                    go = g,
                    srs = srs,
                    baseColors = baseCols
                };
            }
        }

        void UpdateGhostForPiece(Piece piece, Vector2Int anchor, bool valid)
        {
            if (_ghostUnits == null) return;

            for (int i = 0; i < _ghostUnits.Length; i++)
            {
                var gu = _ghostUnits[i];
                if (gu == null || gu.go == null) continue;

                int x = anchor.x + piece.relOffsets[i].x;
                int y = anchor.y + piece.relOffsets[i].y;

                bool inside = InBounds(x, y);
                gu.go.SetActive(inside);
                if (!inside) continue;

                gu.go.transform.position = CellToWorld(x, y);
                SetSortingOrderRecursive(gu.go.transform, ghostSortingOrder);

                Color mul = valid ? Color.white : invalidGhostMultiplier;

                for (int j = 0; j < gu.srs.Length; j++)
                {
                    var sr = gu.srs[j];
                    if (sr == null) continue;

                    Color b = gu.baseColors[j];
                    Color c = new Color(b.r * mul.r, b.g * mul.g, b.b * mul.b, b.a);
                    c.a = b.a * ghostAlpha;
                    sr.color = c;
                }
            }
        }

        void ClearGhost()
        {
            if (_ghostUnits != null)
            {
                for (int i = 0; i < _ghostUnits.Length; i++)
                    if (_ghostUnits[i] != null && _ghostUnits[i].go != null)
                        Destroy(_ghostUnits[i].go);
            }

            _ghostUnits = null;
            _ghostPrefabRef = null;
        }

        #endregion

        #region Skin Guessing + Clear FX

        BlockSkin FindFallbackOnClearSkin()
        {
            if (shapeLibrary == null || shapeLibrary.Skins == null)
                return null;

            foreach (var s in shapeLibrary.Skins)
                if (s != null && s.onClearPrefab != null)
                    return s;

            return null;
        }

        void AssignSkinsToExistingOccupied()
        {
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i] != CellState.Occupied)
                    continue;

                if (_cellSkin[i] != null)
                    continue;

                var go = _cellObjects[i];
                if (go == null)
                {
                    _cellSkin[i] = _fallbackOnClearSkin;
                    continue;
                }

                if (TryGuessSkinFromUnitInstance(go, out var skin))
                    _cellSkin[i] = skin;
                else
                    _cellSkin[i] = _fallbackOnClearSkin;
            }
        }

        bool TryGuessSkinFromUnitInstance(GameObject unit, out BlockSkin skin)
        {
            skin = null;

            if (shapeLibrary == null || shapeLibrary.Skins == null || shapeLibrary.Skins.Count == 0)
                return false;

            var sr = unit.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null)
                return false;

            Sprite sprite = sr.sprite;
            Color col = sr.color;

            BlockSkin spriteMatch = null;

            foreach (var s in shapeLibrary.Skins)
            {
                if (s == null || s.unitPrefab == null)
                    continue;

                var sr2 = s.unitPrefab.GetComponentInChildren<SpriteRenderer>(true);
                if (sr2 == null)
                    continue;

                if (sr2.sprite != sprite)
                    continue;

                if (ApproximatelyColor(sr2.color, col, 0.03f))
                {
                    skin = s;
                    return true;
                }

                if (spriteMatch == null)
                    spriteMatch = s;
            }

            if (spriteMatch != null)
            {
                skin = spriteMatch;
                return true;
            }

            return false;
        }

        static bool ApproximatelyColor(Color a, Color b, float eps)
        {
            return Mathf.Abs(a.r - b.r) <= eps &&
                   Mathf.Abs(a.g - b.g) <= eps &&
                   Mathf.Abs(a.b - b.b) <= eps &&
                   Mathf.Abs(a.a - b.a) <= eps;
        }

        void SpawnOnClearFX(int idx, BlockSkin overrideSkin = null)
        {
            GameObject fxPrefab = null;

            if (overrideSkin != null && overrideSkin.onClearPrefab != null)
                fxPrefab = overrideSkin.onClearPrefab;

            if (fxPrefab == null)
            {
                var skin = _cellSkin != null ? _cellSkin[idx] : null;
                if (skin != null)
                    fxPrefab = skin.onClearPrefab;

                if (fxPrefab == null && _fallbackOnClearSkin != null)
                    fxPrefab = _fallbackOnClearSkin.onClearPrefab;
            }

            if (fxPrefab == null)
                return;

            Vector3 pos;
            if (_cellObjects[idx] != null)
                pos = _cellObjects[idx].transform.position;
            else
            {
                int x = idx % _cols;
                int y = idx / _cols;
                pos = CellToWorld(x, y);
            }

            Instantiate(fxPrefab, pos, _levelRoot.rotation, _fxRoot);
        }

        #endregion

        #region Clear Preview

        void EnsureClearPreviewObjects()
        {
            if (_rowPreview == null) _rowPreview = CreatePreviewSprite("RowClearPreview");
            if (_colPreview == null) _colPreview = CreatePreviewSprite("ColClearPreview");
        }

        SpriteRenderer CreatePreviewSprite(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_previewRoot, false);
            go.transform.localRotation = _previewLocalRotation;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = cleanPreviewSortingOrder;

            go.SetActive(false);
            return sr;
        }

        void HideClearPreview()
        {
            if (_rowPreview != null) _rowPreview.gameObject.SetActive(false);
            if (_colPreview != null) _colPreview.gameObject.SetActive(false);
        }

        bool TryGetDraggedVisualFromSkin(Piece piece, out Sprite sprite, out Color color)
        {
            if (piece.skin != null && piece.skin.unitPrefab != null)
            {
                var sr = piece.skin.unitPrefab.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null)
                {
                    sprite = sr.sprite;
                    color = sr.color;
                    return true;
                }
            }

            if (_dragRoot != null)
            {
                var sr = _dragRoot.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null)
                {
                    sprite = sr.sprite;
                    color = sr.color;
                    return true;
                }
            }

            sprite = null;
            color = default;
            return false;
        }

        void UpdateClearPreviewAndCellSkinOverride(Piece piece, Vector2Int anchor, bool validPlacement)
        {
            if (piece.skin == null || !piece.skin.enableClearPreview || piece.skin.clearPreviewSprite == null)
            {
                HideClearPreview();
                RestoreAllPreviewedCells();
                return;
            }

            if (!validPlacement)
            {
                HideClearPreview();
                RestoreAllPreviewedCells();
                return;
            }

            Array.Clear(_afterMask, 0, _afterMask.Length);
            for (int i = 0; i < _states.Length; i++)
                _afterMask[i] = (_states[i] == CellState.Occupied);

            for (int i = 0; i < piece.relOffsets.Length; i++)
            {
                int x = anchor.x + piece.relOffsets[i].x;
                int y = anchor.y + piece.relOffsets[i].y;
                if (!InBounds(x, y)) continue;
                _afterMask[Index(x, y)] = true;
            }

            for (int y = 0; y < _rows; y++)
            {
                bool anyUsable = false;
                bool full = true;

                for (int x = 0; x < _cols; x++)
                {
                    int idx = Index(x, y);
                    var st = _states[idx];
                    if (st == CellState.Blocked && ignoreBlockedForLineFill) continue;

                    anyUsable = true;
                    if (!_afterMask[idx]) { full = false; break; }
                }

                _rowFull[y] = anyUsable && full;
            }

            for (int x = 0; x < _cols; x++)
            {
                bool anyUsable = false;
                bool full = true;

                for (int y = 0; y < _rows; y++)
                {
                    int idx = Index(x, y);
                    var st = _states[idx];
                    if (st == CellState.Blocked && ignoreBlockedForLineFill) continue;

                    anyUsable = true;
                    if (!_afterMask[idx]) { full = false; break; }
                }

                _colFull[x] = anyUsable && full;
            }

            int minRow = int.MaxValue, maxRow = int.MinValue, rowCount = 0;
            for (int y = 0; y < _rows; y++)
                if (_rowFull[y]) { rowCount++; minRow = Mathf.Min(minRow, y); maxRow = Mathf.Max(maxRow, y); }

            int minCol = int.MaxValue, maxCol = int.MinValue, colCount = 0;
            for (int x = 0; x < _cols; x++)
                if (_colFull[x]) { colCount++; minCol = Mathf.Min(minCol, x); maxCol = Mathf.Max(maxCol, x); }

            if (rowCount == 0 && colCount == 0)
            {
                HideClearPreview();
                RestoreAllPreviewedCells();
                return;
            }

            EnsureClearPreviewObjects();

            float ex = Mathf.Max(0f, piece.skin.clearPreviewExtraScale);

            if (rowCount > 0)
            {
                float w = (_cols * _cellSize) + ex;
                float h = ((maxRow - minRow + 1) * _cellSize) + ex;

                Vector3 a = CellToLocal(0, minRow);
                Vector3 b = CellToLocal(_cols - 1, minRow);
                Vector3 c = CellToLocal(0, maxRow);
                Vector3 d = CellToLocal(_cols - 1, maxRow);
                Vector3 center = (a + b + c + d) * 0.25f;

                _rowPreview.sprite = piece.skin.clearPreviewSprite;
                _rowPreview.color = piece.skin.clearPreviewTint;
                _rowPreview.sortingOrder = cleanPreviewSortingOrder;
                _rowPreview.size = new Vector2(w, h);
                _rowPreview.transform.localPosition = center;
                _rowPreview.gameObject.SetActive(true);
            }
            else _rowPreview.gameObject.SetActive(false);

            if (colCount > 0)
            {
                float w = ((maxCol - minCol + 1) * _cellSize) + ex;
                float h = (_rows * _cellSize) + ex;

                Vector3 a = CellToLocal(minCol, 0);
                Vector3 b = CellToLocal(minCol, _rows - 1);
                Vector3 c = CellToLocal(maxCol, 0);
                Vector3 d = CellToLocal(maxCol, _rows - 1);
                Vector3 center = (a + b + c + d) * 0.25f;

                _colPreview.sprite = piece.skin.clearPreviewSprite;
                _colPreview.color = piece.skin.clearPreviewTint;
                _colPreview.sortingOrder = cleanPreviewSortingOrder;
                _colPreview.size = new Vector2(w, h);
                _colPreview.transform.localPosition = center;
                _colPreview.gameObject.SetActive(true);
            }
            else _colPreview.gameObject.SetActive(false);

            if (!TryGetDraggedVisualFromSkin(piece, out var previewSprite, out var previewColor))
                return;

            for (int idx = 0; idx < _states.Length; idx++)
            {
                int x = idx % _cols;
                int y = idx / _cols;

                bool shouldPreview =
                    (_cellObjects[idx] != null) &&
                    (_states[idx] == CellState.Occupied) &&
                    (_rowFull[y] || _colFull[x]);

                if (!shouldPreview)
                {
                    if (_cellPreviewOverridden[idx])
                        RestoreCellPreview(idx);
                    continue;
                }

                var sr = _cellSR[idx];
                if (sr == null && _cellObjects[idx] != null)
                {
                    sr = _cellObjects[idx].GetComponentInChildren<SpriteRenderer>(true);
                    _cellSR[idx] = sr;
                }

                if (sr == null) continue;

                if (!_cellPreviewOverridden[idx])
                {
                    _cellOrigSprite[idx] = sr.sprite;
                    _cellOrigColor[idx] = sr.color;
                    _cellPreviewOverridden[idx] = true;
                }

                if (previewSprite != null)
                    sr.sprite = previewSprite;

                sr.color = previewColor;
            }
        }

        void RestoreCellPreview(int idx)
        {
            _cellPreviewOverridden[idx] = false;

            var sr = _cellSR[idx];
            if (sr == null) return;

            sr.sprite = _cellOrigSprite[idx];
            sr.color = _cellOrigColor[idx];
        }

        void RestoreAllPreviewedCells()
        {
            if (_cellPreviewOverridden == null) return;

            for (int i = 0; i < _cellPreviewOverridden.Length; i++)
                if (_cellPreviewOverridden[i])
                    RestoreCellPreview(i);
        }

        #endregion

        #region Helpers

        void SetSortingOrderRecursive(Transform root, int order)
        {
            var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
                srs[i].sortingOrder = order;
        }

        void DisableAllColliders(GameObject go)
        {
            var cols3 = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols3.Length; i++) cols3[i].enabled = false;

            var cols2 = go.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols2.Length; i++) cols2[i].enabled = false;
        }

        #endregion
    }
}