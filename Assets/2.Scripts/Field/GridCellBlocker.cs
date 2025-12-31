using UnityEngine;

public class GridCellBlocker : MonoBehaviour
{
    public GridBoard grid;

    [Header("Block Flags")]
    public bool blocksMovement = true;
    public bool blocksVision = true;

    [Header("Cell (optional)")]
    public bool useManualCell = false;
    public Vector2Int manualCell;

    private Vector2Int _cell;
    private bool _registered;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
    }

    private void Start()
    {
        if (grid == null) return;

        _cell = useManualCell ? manualCell : grid.WorldToCell(transform.position);
        grid.RegisterCellBlock(_cell, blocksMovement, blocksVision);
        _registered = true;
    }

    private void OnDestroy()
    {
        if (!_registered || grid == null) return;
        grid.UnregisterCellBlock(_cell, blocksMovement, blocksVision);
    }
}
