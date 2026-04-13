using UnityEngine;
using UnityEngine.Tilemaps;

public class MapRenderer : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public Tilemap roadTilemap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase roadTile;
    public TileBase waterTile;

    [Header("References")]
    public GridBoard gridBoard;

    [Header("Debug")]
    public bool logRendering = true;

    public void RenderMap(MapData mapData)
    {
        if (mapData == null)
        {
            Debug.LogError("[MapRenderer] MapData is null");
            return;
        }

        if (logRendering)
            Debug.Log("[MapRenderer] Rendering map: " + mapData.width + "x" + mapData.height);

        ClearAllTilemaps();

        for (int x = 0; x < mapData.width; x++)
        {
            for (int y = 0; y < mapData.height; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileType tileType = mapData.GetTile(x, y);
                RenderTile(cellPos, tileType);
            }
        }

        if (gridBoard != null)
        {
            UpdateGridBoard(mapData);
        }

        if (logRendering)
            Debug.Log("[MapRenderer] Map rendering complete");
    }

    private void RenderTile(Vector3Int cellPos, TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty:
                if (floorTilemap != null && floorTile != null)
                    floorTilemap.SetTile(cellPos, floorTile);
                break;

            case TileType.Wall:
                // Floor under walls so rounded corners / transparency show dirt, not void.
                if (floorTilemap != null && floorTile != null)
                    floorTilemap.SetTile(cellPos, floorTile);
                if (wallTilemap != null && wallTile != null)
                    wallTilemap.SetTile(cellPos, wallTile);
                break;

            case TileType.Road:
                if (roadTilemap != null && roadTile != null)
                    roadTilemap.SetTile(cellPos, roadTile);
                else if (floorTilemap != null && floorTile != null)
                    floorTilemap.SetTile(cellPos, floorTile);
                break;

            case TileType.Water:
                if (waterTile != null && floorTilemap != null)
                    floorTilemap.SetTile(cellPos, waterTile);
                else if (floorTile != null && floorTilemap != null)
                    floorTilemap.SetTile(cellPos, floorTile);
                break;

            case TileType.Tree:
                if (floorTilemap != null && floorTile != null)
                    floorTilemap.SetTile(cellPos, floorTile);
                if (wallTilemap != null && wallTile != null)
                    wallTilemap.SetTile(cellPos, wallTile);
                break;
        }
    }

    private void ClearAllTilemaps()
    {
        if (floorTilemap != null)
            floorTilemap.ClearAllTiles();

        if (wallTilemap != null)
            wallTilemap.ClearAllTiles();

        if (roadTilemap != null)
            roadTilemap.ClearAllTiles();
    }

    private void UpdateGridBoard(MapData mapData)
    {
        Debug.Log($"[MapRenderer] gridBoard={gridBoard?.name} id={gridBoard?.GetInstanceID()}");
        if (gridBoard == null)
        {
            if (logRendering)
                Debug.LogWarning("[MapRenderer] GridBoard is null");
            return;
        }

        gridBoard.width = mapData.width;
        gridBoard.height = mapData.height;

        gridBoard.blockedMoveCells.Clear();
        gridBoard.blockedVisionCells.Clear();

        int wallCount = 0;
        for (int x = 0; x < mapData.width; x++)
        {
            for (int y = 0; y < mapData.height; y++)
            {
                TileType tileType = mapData.GetTile(x, y);

                if (tileType == TileType.Wall || tileType == TileType.Tree)
                {
                    Vector2Int cellPos = new Vector2Int(x, y);
                    gridBoard.blockedMoveCells.Add(cellPos);
                    gridBoard.blockedVisionCells.Add(cellPos);
                    wallCount++;
                }
            }
        }

        gridBoard.RefreshBlockedCells();

        if (logRendering)
        {
            Debug.Log("[MapRenderer] GridBoard updated: " + wallCount + " walls registered");
            Debug.Log("[MapRenderer] blockedMoveCells.Count = " + gridBoard.blockedMoveCells.Count);
        }
    }
}
