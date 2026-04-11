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
    private Grid _unityGrid;

    private void Awake()
    {
        _blockedMove = new HashSet<Vector2Int>(blockedMoveCells);
        _blockedVision = new HashSet<Vector2Int>(blockedVisionCells);
        _unityGrid = GetComponent<Grid>();
        
        Debug.Log("[GridBoard] Awake - _blockedMove.Count = " + _blockedMove.Count);
        Debug.Log("[GridBoard] Unity Grid: " + (_unityGrid != null ? "Found" : "NULL"));
    }

    public void RefreshBlockedCells()
    {
        Debug.Log("[GridBoard] RefreshBlockedCells START - blockedMoveCells.Count = " + blockedMoveCells.Count);
        
        if (_blockedMove == null)
            _blockedMove = new HashSet<Vector2Int>();
        else
            _blockedMove.Clear();
        
        if (_blockedVision == null)
            _blockedVision = new HashSet<Vector2Int>();
        else
            _blockedVision.Clear();
        
        foreach (var cell in blockedMoveCells)
            _blockedMove.Add(cell);
        
        foreach (var cell in blockedVisionCells)
            _blockedVision.Add(cell);
        
        Debug.Log("[GridBoard] RefreshBlockedCells END - _blockedMove.Count = " + _blockedMove.Count);
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
        if (_unityGrid != null)
        {
            // CellToWorld는 셀 앵커(모서리) 기준. WorldToCell과 쌍을 맞추려면 셀 중심을 써야 한다.
            return _unityGrid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }
        return origin + new Vector3((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize, 0f);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        if (_unityGrid != null)
        {
            Vector3Int cell3 = _unityGrid.WorldToCell(worldPos);
            return new Vector2Int(cell3.x, cell3.y);
        }
        
        Vector3 local = worldPos - origin;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.y / cellSize);
        return new Vector2Int(x, y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_blockedMove == null || _blockedMove.Count == 0)
            return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);

        foreach (var cell in _blockedMove)
        {
            if (!InBounds(cell)) continue;

            Vector3 worldPos = CellToWorld(cell);
            
            float size = cellSize;
            if (_unityGrid != null)
                size = _unityGrid.cellSize.x;
            
            Gizmos.DrawCube(worldPos, new Vector3(size, size, 0.1f));
        }
    }
#endif
}