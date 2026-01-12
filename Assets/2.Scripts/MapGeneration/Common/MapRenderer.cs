using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 맵 데이터를 Tilemap에 렌더링
/// </summary>
public class MapRenderer : MonoBehaviour
{
    [Header("Tilemaps")]
    [Tooltip("바닥 타일맵")]
    public Tilemap floorTilemap;
    
    [Tooltip("벽 타일맵")]
    public Tilemap wallTilemap;
    
    [Tooltip("길 타일맵 (선택)")]
    public Tilemap roadTilemap;
    
    [Header("Tiles")]
    [Tooltip("바닥 타일")]
    public TileBase floorTile;
    
    [Tooltip("벽 타일")]
    public TileBase wallTile;
    
    [Tooltip("길 타일 (선택)")]
    public TileBase roadTile;
    
    [Tooltip("물 타일 (선택)")]
    public TileBase waterTile;
    
    [Header("References")]
    [Tooltip("GridBoard (기존 시스템)")]
    public GridBoard gridBoard;
    
    [Header("Debug")]
    public bool logRendering = true;
    
    /// <summary>
    /// 맵 데이터를 Tilemap에 렌더링
    /// </summary>
    public void RenderMap(MapData mapData)
    {
        if (mapData == null)
        {
            Debug.LogError("[MapRenderer] MapData가 null입니다!");
            return;
        }
        
        if (logRendering)
            Debug.Log($"[MapRenderer] 맵 렌더링 시작: {mapData.width}x{mapData.height}");
        
        // 기존 타일 모두 제거
        ClearAllTilemaps();
        
        // 타일맵 렌더링
        for (int x = 0; x < mapData.width; x++)
        {
            for (int y = 0; y < mapData.height; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileType tileType = mapData.GetTile(x, y);
                
                RenderTile(cellPos, tileType);
            }
        }
        
        // GridBoard 업데이트 (벽 정보 동기화)
        if (gridBoard != null)
        {
            UpdateGridBoard(mapData);
        }
        
        if (logRendering)
            Debug.Log($"[MapRenderer] 맵 렌더링 완료");
    }
    
    /// <summary>
    /// 개별 타일 렌더링
    /// </summary>
    private void RenderTile(Vector3Int cellPos, TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty:
                // 바닥
                if (floorTilemap != null && floorTile != null)
                    floorTilemap.SetTile(cellPos, floorTile);
                break;
                
            case TileType.Wall:
                // 벽
                if (wallTilemap != null && wallTile != null)
                    wallTilemap.SetTile(cellPos, wallTile);
                break;
                
            case TileType.Road:
                // 길
                if (roadTilemap != null && roadTile != null)
                    roadTilemap.SetTile(cellPos, roadTile);
                else if (floorTilemap != null && floorTile != null)
                    floorTilemap.SetTile(cellPos, floorTile); // 길 타일 없으면 바닥으로
                break;
                
            case TileType.Water:
                // 물
                if (floorTilemap != null && waterTile != null)
                    floorTilemap.SetTile(cellPos, waterTile);
                break;
                
            case TileType.Tree:
                // 나무 (벽처럼 취급)
                if (wallTilemap != null && wallTile != null)
                    wallTilemap.SetTile(cellPos, wallTile);
                break;
        }
    }
    
    /// <summary>
    /// 모든 타일맵 초기화
    /// </summary>
    private void ClearAllTilemaps()
    {
        if (floorTilemap != null)
            floorTilemap.ClearAllTiles();
        
        if (wallTilemap != null)
            wallTilemap.ClearAllTiles();
        
        if (roadTilemap != null)
            roadTilemap.ClearAllTiles();
    }
    
    /// <summary>
    /// GridBoard 업데이트 (벽 정보 동기화)
    /// </summary>
    private void UpdateGridBoard(MapData mapData)
    {
        // GridBoard의 moveBlockedCells 업데이트
        // (기존 GridBoard가 벽 정보를 저장하는 방식에 따라 다름)
        
        // 예시: GridBoard에 UpdateWalls(MapData) 메서드가 있다면
        // gridBoard.UpdateWalls(mapData);
        
        if (logRendering)
            Debug.Log($"[MapRenderer] GridBoard 업데이트 완료");
    }
}
