using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [CreateAssetMenu(menuName = "MOST/Shape Library", fileName = "ShapeLibrary")]
    public class ShapeLibrarySO : ScriptableObject
    {
        [SerializeField] private List<BlockShape> shapes = new();
        public IReadOnlyList<BlockShape> Shapes => shapes;

        [SerializeField] private List<BlockSkin> skins = new();
        public IReadOnlyList<BlockSkin> Skins => skins;

        /// <summary>
        /// Weighted random pick using chance, then random rotation from allowedRotations (0 always included).
        /// Returns false if nothing is pickable (empty list or all chance <= 0).
        /// </summary>
        public bool TryPickRandom(out BlockShape shape, out int rotationDegrees)
        {
            shape = null;
            rotationDegrees = 0;

            if (shapes == null || shapes.Count == 0)
                return false;

            float total = 0f;
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                if (s == null) continue;
                if (s.chance <= 0f) continue;
                total += s.chance;
            }

            if (total <= 0f)
                return false;

            float roll = UnityEngine.Random.value * total;

            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                if (s == null) continue;
                if (s.chance <= 0f) continue;

                roll -= s.chance;
                if (roll <= 0f)
                {
                    shape = s;
                    rotationDegrees = s.PickRotationDegrees();
                    return true;
                }
            }

            shape = shapes[shapes.Count - 1];
            rotationDegrees = shape != null ? shape.PickRotationDegrees() : 0;
            return shape != null;
        }

        /// <summary>
        /// Optional: weighted random pick a skin.
        /// Returns false if list empty or all chance <= 0.
        /// </summary>
        public bool TryPickRandomSkin(out BlockSkin skin)
        {
            skin = null;

            if (skins == null || skins.Count == 0)
                return false;

            float total = 0f;
            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                if (s == null) continue;
                if (s.chance <= 0f) continue;
                total += s.chance;
            }

            if (total <= 0f)
                return false;

            float roll = UnityEngine.Random.value * total;

            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                if (s == null) continue;
                if (s.chance <= 0f) continue;

                roll -= s.chance;
                if (roll <= 0f)
                {
                    skin = s;
                    return true;
                }
            }

            skin = skins[skins.Count - 1];
            return skin != null;
        }

        public BlockShape FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || shapes == null) return null;

            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                if (s == null) continue;
                if (string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase))
                    return s;
            }

            return null;
        }
    }

    [Serializable]
    public class BlockSkin
    {
        public string id;

        [Tooltip("Prefab for a SINGLE unit block (SpriteRenderer etc). If null, BlockBlastGame will fallback to blockUnitPrefab.")]
        public GameObject unitPrefab;

        [Min(0f)]
        [Tooltip("Relative weight. 0 = never selected.")]
        public float chance = 1f;

        [Header("Clear Preview (Per Skin)")]
        public bool enableClearPreview = true;

        [Tooltip("Sliced sprite used to highlight rows/cols that will clear while dragging this skin.")]
        public Sprite clearPreviewSprite;

        [Tooltip("Tint includes alpha.")]
        public Color clearPreviewTint = new Color(1f, 0.95f, 0.2f, 0.55f);

        [Tooltip("Added to BOTH width and height of the sliced preview.")]
        public float clearPreviewExtraScale = 0.15f;

        [Tooltip("Spawned when a unit using this skin is cleared from the board.")]
        public GameObject onClearPrefab;
    }

    [Serializable]
    public class BlockShape
    {
        public string id;
        public ShapeGrid grid = new ShapeGrid();

        [Min(0f)]
        [Tooltip("Relative weight. 0 = never selected.")]
        public float chance = 1f;

        [Flags]
        public enum RotationOptions
        {
            None = 0,
            Use90 = 1 << 0,
            Use180 = 1 << 1,
            Use270 = 1 << 2
        }

        [Tooltip("0° is always allowed. Check additional rotations you want the spawner to use.")]
        public RotationOptions allowedRotations = RotationOptions.Use90 | RotationOptions.Use180 | RotationOptions.Use270;

        public int PickRotationDegrees()
        {
            int count = 1;
            if ((allowedRotations & RotationOptions.Use90) != 0) count++;
            if ((allowedRotations & RotationOptions.Use180) != 0) count++;
            if ((allowedRotations & RotationOptions.Use270) != 0) count++;

            int pick = UnityEngine.Random.Range(0, count);

            if (pick == 0) return 0;

            if ((allowedRotations & RotationOptions.Use90) != 0)
            {
                pick--;
                if (pick == 0) return 90;
            }

            if ((allowedRotations & RotationOptions.Use180) != 0)
            {
                pick--;
                if (pick == 0) return 180;
            }

            return ((allowedRotations & RotationOptions.Use270) != 0) ? 270 : 0;
        }

        public Vector2Int[] GetFilledOffsets(int rotationDegrees)
        {
            if (grid == null) return Array.Empty<Vector2Int>();

            var points = new List<Vector2Int>();

            int cols = Mathf.Max(1, grid.columns);
            int rows = Mathf.Max(1, grid.rows);

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var st = grid.GetState(x, y);
                    if (st == ShapeGrid.CellState.Empty) continue;
                    points.Add(new Vector2Int(x, y));
                }

            if (points.Count == 0)
                return Array.Empty<Vector2Int>();

            NormalizeToMin(points);

            for (int i = 0; i < points.Count; i++)
                points[i] = Rotate(points[i], rotationDegrees);

            NormalizeToMin(points);

            return points.ToArray();
        }

        static Vector2Int Rotate(Vector2Int p, int deg)
        {
            deg %= 360;
            if (deg < 0) deg += 360;

            return deg switch
            {
                0 => p,
                90 => new Vector2Int(p.y, -p.x),
                180 => new Vector2Int(-p.x, -p.y),
                270 => new Vector2Int(-p.y, p.x),
                _ => throw new ArgumentException("Rotation must be 0, 90, 180, or 270.", nameof(deg))
            };
        }

        static void NormalizeToMin(List<Vector2Int> pts)
        {
            int minX = pts[0].x;
            int minY = pts[0].y;

            for (int i = 1; i < pts.Count; i++)
            {
                minX = Mathf.Min(minX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y);
            }

            if (minX == 0 && minY == 0) return;

            for (int i = 0; i < pts.Count; i++)
                pts[i] = new Vector2Int(pts[i].x - minX, pts[i].y - minY);
        }
    }
}