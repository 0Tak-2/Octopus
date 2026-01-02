using System.Collections.Generic;
using UnityEngine;

public class CombatTerrainGenerator : MonoBehaviour
{
    [Header("Wall clusters")]
    public int clusterCountMin = 3;
    public int clusterCountMax = 5;
    public int clusterSizeMin = 3;
    public int clusterSizeMax = 4;

    [Header("Other terrain counts")]
    public int seaweedCount = 3;
    public int hillCount = 2;
    public int currentCount = 1;

    [Header("Generation safety")]
    public int triesPerCluster = 100;
    public int maxFullReroll = 40;

    // 4연결 패턴만
    private static readonly Vector2Int[][] Patterns3 =
    {
        // 1자(3)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0) },
        // ㄴ(3)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1) },
    };

    private static readonly Vector2Int[][] Patterns4 =
    {
        // 1자(4)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0) },
        // ㅁ(2x2)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) },
        // ㄴ(4)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(2,1) },
        // ㄹ(지그재그)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(2,1) },
        // T(4)
        new []{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(1,1) },
    };

    public bool Generate(
        int width,
        int height,
        Vector2Int playerSpawn,
        Vector2Int enemySpawn,
        out CombatGridData grid)
    {
        for (int reroll = 0; reroll < maxFullReroll; reroll++)
        {
            grid = new CombatGridData(width, height);

            HashSet<Vector2Int> spawnReserved = new HashSet<Vector2Int> { playerSpawn, enemySpawn };

            // 벽 금지: 스폰 + 주변 8방 1칸
            HashSet<Vector2Int> wallForbidden = BuildWallForbidden(width, height, playerSpawn, enemySpawn);

            // 벽 타일의 클러스터 ID
            int[,] clusterId = new int[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    clusterId[x, y] = -1;

            int clusterCount = Random.Range(clusterCountMin, clusterCountMax + 1);

            bool attachedPairUsed = false; // 4방 접촉 “한 쌍” 허용
            int nextCluster = 0;

            bool ok = true;

            for (int c = 0; c < clusterCount; c++)
            {
                int size = Random.Range(clusterSizeMin, clusterSizeMax + 1);
                size = Mathf.Clamp(size, 3, 4);

                bool placed = false;

                for (int t = 0; t < triesPerCluster && !placed; t++)
                {
                    Vector2Int[] pattern = PickPattern(size);
                    pattern = TransformPattern(pattern);

                    GetBounds(pattern, out int minX, out int maxX, out int minY, out int maxY);

                    // 테두리 금지(1..width-2, 1..height-2 범위 안에 들어오게)
                    int axMin = 1 - minX;
                    int axMax = (width - 2) - maxX;
                    int ayMin = 1 - minY;
                    int ayMax = (height - 2) - maxY;

                    if (axMin > axMax || ayMin > ayMax) continue;

                    int anchorX = Random.Range(axMin, axMax + 1);
                    int anchorY = Random.Range(ayMin, ayMax + 1);

                    List<Vector2Int> cells = new List<Vector2Int>(pattern.Length);
                    for (int i = 0; i < pattern.Length; i++)
                        cells.Add(new Vector2Int(anchorX + pattern[i].x, anchorY + pattern[i].y));

                    // 스폰 침범 금지
                    bool overlapSpawn = false;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (spawnReserved.Contains(cells[i])) { overlapSpawn = true; break; }
                    }
                    if (overlapSpawn) continue;

                    // 스폰 주변 벽 금지 구역 침범 금지
                    bool inForbidden = false;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (wallForbidden.Contains(cells[i])) { inForbidden = true; break; }
                    }
                    if (inForbidden) continue;

                    // 배치 검증
                    HashSet<int> touch4 = new HashSet<int>();
                    bool invalid = false;

                    HashSet<Vector2Int> newSet = new HashSet<Vector2Int>(cells);

                    for (int i = 0; i < cells.Count && !invalid; i++)
                    {
                        Vector2Int p = cells[i];

                        if (!grid.InBounds(p.x, p.y)) { invalid = true; break; }
                        if (!grid.IsEmpty(p.x, p.y)) { invalid = true; break; }

                        // 주변 8칸에 “다른 클러스터” 벽이 있으면 원칙적으로 금지
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                int nx = p.x + dx;
                                int ny = p.y + dy;
                                if (!grid.InBounds(nx, ny)) continue;

                                if (!grid.IsWall(nx, ny)) continue;

                                // 같은 신규 클러스터 내부라면 무시
                                if (newSet.Contains(new Vector2Int(nx, ny)))
                                    continue;

                                bool diagonal = (dx != 0 && dy != 0);

                                if (diagonal)
                                {
                                    // ✅ 대각선 접촉은 무조건 금지
                                    invalid = true;
                                    break;
                                }
                                else
                                {
                                    // 4방 접촉은 “한 쌍”만 자연발생으로 허용 가능
                                    touch4.Add(clusterId[nx, ny]);
                                }
                            }
                    }

                    if (invalid) continue;

                    touch4.Remove(-1);

                    if (touch4.Count == 0)
                    {
                        Commit(grid, clusterId, cells, nextCluster);
                        nextCluster++;
                        placed = true;
                    }
                    else if (touch4.Count == 1)
                    {
                        if (attachedPairUsed) continue; // 이미 한 쌍 붙었으면 금지

                        // 이 경우가 “자연발생 붙기 1회”
                        Commit(grid, clusterId, cells, nextCluster);
                        attachedPairUsed = true;
                        nextCluster++;
                        placed = true;
                    }
                    else
                    {
                        // 두 클러스터 이상과 동시에 4방 접촉이면 금지
                        continue;
                    }
                }

                if (!placed)
                {
                    ok = false;
                    break;
                }
            }

            if (!ok) continue;

            // 스폰은 항상 Empty 유지(안전)
            grid.Set(playerSpawn.x, playerSpawn.y, CombatTileType.Empty);
            grid.Set(enemySpawn.x, enemySpawn.y, CombatTileType.Empty);

            // 해초/언덕/해류 배치(스폰 제외)
            if (!PlaceOtherTerrain(grid, spawnReserved))
                continue;

            return true;
        }

        grid = null;
        return false;
    }

    private HashSet<Vector2Int> BuildWallForbidden(int w, int h, Vector2Int p, Vector2Int e)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        AddRadius8_1(set, w, h, p);
        AddRadius8_1(set, w, h, e);
        return set;
    }

    private void AddRadius8_1(HashSet<Vector2Int> set, int w, int h, Vector2Int c)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = c.x + dx;
                int y = c.y + dy;
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                set.Add(new Vector2Int(x, y));
            }
    }

    private Vector2Int[] PickPattern(int size)
    {
        if (size <= 3) return Patterns3[Random.Range(0, Patterns3.Length)];
        return Patterns4[Random.Range(0, Patterns4.Length)];
    }

    private Vector2Int[] TransformPattern(Vector2Int[] src)
    {
        int rot = Random.Range(0, 4);
        bool flipX = Random.value < 0.5f;
        bool flipY = Random.value < 0.5f;

        Vector2Int[] outp = new Vector2Int[src.Length];

        for (int i = 0; i < src.Length; i++)
        {
            Vector2Int v = src[i];

            if (flipX) v.x = -v.x;
            if (flipY) v.y = -v.y;

            for (int r = 0; r < rot; r++)
                v = new Vector2Int(-v.y, v.x);

            outp[i] = v;
        }

        return outp;
    }

    private void GetBounds(Vector2Int[] pts, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = int.MaxValue; maxX = int.MinValue;
        minY = int.MaxValue; maxY = int.MinValue;

        for (int i = 0; i < pts.Length; i++)
        {
            minX = Mathf.Min(minX, pts[i].x);
            maxX = Mathf.Max(maxX, pts[i].x);
            minY = Mathf.Min(minY, pts[i].y);
            maxY = Mathf.Max(maxY, pts[i].y);
        }
    }

    private void Commit(CombatGridData grid, int[,] clusterId, List<Vector2Int> cells, int id)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int p = cells[i];
            grid.Set(p.x, p.y, CombatTileType.Wall);
            clusterId[p.x, p.y] = id;
        }
    }

    private bool PlaceOtherTerrain(CombatGridData grid, HashSet<Vector2Int> spawnReserved)
    {
        List<Vector2Int> empties = new List<Vector2Int>();

        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
            {
                if (!grid.IsEmpty(x, y)) continue;

                Vector2Int p = new Vector2Int(x, y);
                if (spawnReserved.Contains(p)) continue; // ✅ 스폰은 항상 Empty

                empties.Add(p);
            }

        int need = seaweedCount + hillCount + currentCount;
        if (empties.Count < need) return false;

        Shuffle(empties);
        int idx = 0;

        for (int i = 0; i < seaweedCount; i++, idx++)
            grid.Set(empties[idx].x, empties[idx].y, CombatTileType.Seaweed);

        for (int i = 0; i < hillCount; i++, idx++)
            grid.Set(empties[idx].x, empties[idx].y, CombatTileType.Hill);

        for (int i = 0; i < currentCount; i++, idx++)
            grid.Set(empties[idx].x, empties[idx].y, CombatTileType.Current);

        return true;
    }

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
