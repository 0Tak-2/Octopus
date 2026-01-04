using System.Collections.Generic;
using UnityEngine;

public class CombatSpawnPlanner : MonoBehaviour
{
    public void PickEdgeSpawns(int width, int height, out Vector2Int player, out Vector2Int enemy)
    {
        List<Vector2Int> edges = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            edges.Add(new Vector2Int(x, 0));
            edges.Add(new Vector2Int(x, height - 1));
        }

        for (int y = 1; y < height - 1; y++)
        {
            edges.Add(new Vector2Int(0, y));
            edges.Add(new Vector2Int(width - 1, y));
        }

        float bestScore = float.NegativeInfinity;
        Vector2Int bestP = edges[0];
        Vector2Int bestE = edges[edges.Count - 1];

        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = 0; j < edges.Count; j++)
            {
                if (i == j) continue;

                Vector2Int p = edges[i];
                Vector2Int e = edges[j];

                int dist = Chebyshev(p, e); // 멀수록 좋음
                float sym = SymmetryScore(width, height, p, e); // 대칭 가까울수록 좋음
                float score = dist * 10f + sym;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestP = p;
                    bestE = e;
                }
            }
        }

        player = bestP;
        enemy = bestE;
    }

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy);
    }

    private float SymmetryScore(int w, int h, Vector2Int p, Vector2Int e)
    {
        // 180도 중심대칭 선호
        int sx = (w - 1) - p.x;
        int sy = (h - 1) - p.y;
        float dCenterSym = Mathf.Abs(e.x - sx) + Mathf.Abs(e.y - sy);

        // 가로형/세로형에 따른 축 대칭 선호
        float dAxis = 0f;
        if (w > h)
        {
            int ax = (w - 1) - p.x;
            dAxis = Mathf.Abs(e.x - ax) + Mathf.Abs(e.y - p.y);
        }
        else
        {
            int ay = (h - 1) - p.y;
            dAxis = Mathf.Abs(e.x - p.x) + Mathf.Abs(e.y - ay);
        }

        return -(dCenterSym * 2f + dAxis);
    }
}
