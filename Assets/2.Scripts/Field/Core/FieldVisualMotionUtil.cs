using UnityEngine;

/// <summary>
/// 필드 오브젝트의 시각 연출용 트랜스폼 선택 유틸.
/// 루트 트랜스폼은 논리 좌표(격자) 이동용으로 유지하고,
/// 피격/대시 같은 연출은 가급적 하위 비주얼에 적용한다.
/// </summary>
public static class FieldVisualMotionUtil
{
    public static Transform ResolveVisualRoot(Transform root)
    {
        if (root == null) return null;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        Transform best = null;
        int bestDepth = int.MaxValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.transform == null) continue;

            Transform t = sr.transform;
            if (t == root) continue;

            int depth = GetDepthFromAncestor(t, root);
            if (depth < 0) continue;

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = t;
            }
        }

        return best != null ? best : root;
    }

    private static int GetDepthFromAncestor(Transform child, Transform ancestor)
    {
        int depth = 0;
        Transform cur = child;
        while (cur != null)
        {
            if (cur == ancestor)
                return depth;
            depth++;
            cur = cur.parent;
        }
        return -1;
    }
}
