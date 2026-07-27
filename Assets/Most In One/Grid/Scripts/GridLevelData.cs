using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    public class GridLevelData : MonoBehaviour
    {
        public enum LevelPlane : byte { XY = 0, XZ = 1 }

        public float CustomValue;
        [Header("Layout")]
        [Tooltip("(Columns, Rows)")]
        public Vector2 LevelGridByCells;

        public Vector3 CenterPoint;
        public float CellWorldSize;

        [Header("Runtime Grid Snapshot")]
        public LevelPlane Plane = LevelPlane.XZ;

        // Matches GridLevelProfile.GetPositionForCell logic (yFlipped = Rows-1-y)
        public bool FlipSecondAxisByIndex = true;

        // 0 = Empty, 1 = Occupied, 2 = Blocked
        [Tooltip("0=Empty, 1=Occupied, 2=Blocked. Length must be Columns*Rows.")]
        public byte[] CellStates;

        [Tooltip("Per-cell spawned instance references (index = y*Columns + x). Null for Empty/Blocked.")]
        public GameObject[] CellObjects;

        [Header("Special Cells")]
        public bool HasStartCell;
        public Vector2Int StartCell;

        public bool HasEndCell;
        public Vector2Int EndCell;

        // Optional convenience references (may be null if Start/End cell has no prefab instance)
        public GameObject StartCellObject;
        public GameObject EndCellObject;

        [Header("Counts")]
        public int NumberOfEmptyCells;
        public int NumberCountedObjects;

        // Convenience helpers
        public int Columns => Mathf.Max(1, Mathf.RoundToInt(LevelGridByCells.x));
        public int Rows => Mathf.Max(1, Mathf.RoundToInt(LevelGridByCells.y));

        public bool InBounds(int x, int y) => x >= 0 && x < Columns && y >= 0 && y < Rows;
        public int Index(int x, int y) => y * Columns + x;

        public byte GetCellState(int x, int y)
        {
            if (!InBounds(x, y) || CellStates == null) return 0;
            int i = Index(x, y);
            return (i >= 0 && i < CellStates.Length) ? CellStates[i] : (byte)0;
        }

        public GameObject GetCellObject(int x, int y)
        {
            if (!InBounds(x, y) || CellObjects == null) return null;
            int i = Index(x, y);
            return (i >= 0 && i < CellObjects.Length) ? CellObjects[i] : null;
        }
    }
}