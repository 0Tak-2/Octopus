using System.Collections.Generic;
using UnityEngine;

public class GridBoard : MonoBehaviour
{
    [Header("Grid")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    public Vector3 origin = Vector3.zero;

    [Header("Blocked Cells (Movement)")]
    public List<Vector2Int> blockedMoveCells = new List<Vector2Int>();

    [Header("Blocked Cells (Vision)")]
    public List<Vector2Int> blockedVisionCells = new List<Vector2Int>();

    private HashSet<Vector2Int> _blockedMove;
    private HashSet<Vector2Int> _blockedVision;

    private void Awake()
    {
        _blockedMove = new HashSet<Vector2Int>(blockedMoveCells);
        _blockedVision = new HashSet<Vector2Int>(blockedVisionCells);
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public bool IsMoveBlocked(Vector2Int cell)
    {
        return _blockedMove != null && _blockedMove.Contains(cell);
    }

    public bool IsVisionBlocked(Vector2Int cell)
    {
        return _blockedVision != null && _blockedVision.Contains(cell);
    }

    public void RegisterCellBlock(Vector2Int cell, bool blocksMove, bool blocksVision)
    {
        if (!InBounds(cell)) return;

        if (blocksMove) _blockedMove.Add(cell);
        if (blocksVision) _blockedVision.Add(cell);
    }

    public void UnregisterCellBlock(Vector2Int cell, bool blocksMove, bool blocksVision)
    {
        if (blocksMove) _blockedMove.Remove(cell);
        if (blocksVision) _blockedVision.Remove(cell);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return origin + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - origin;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.y / cellSize);
        return new Vector2Int(x, y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 c = CellToWorld(new Vector2Int(x, y));
                Gizmos.DrawWireCube(c, new Vector3(cellSize, cellSize, 0f));
            }
        }

        // 이동 막힘
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.55f);
        if (blockedMoveCells != null)
        {
            foreach (var b in blockedMoveCells)
            {
                Vector3 c = CellToWorld(b);
                Gizmos.DrawCube(c, new Vector3(cellSize, cellSize, 0f));
            }
        }

        // 시야 막힘 (파란색)
        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.55f);
        if (blockedVisionCells != null)
        {
            foreach (var b in blockedVisionCells)
            {
                Vector3 c = CellToWorld(b);
                Gizmos.DrawCube(c, new Vector3(cellSize * 0.85f, cellSize * 0.85f, 0f));
            }
        }
    }
#endif
}
