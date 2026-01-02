using System.Collections.Generic;
using UnityEngine;

public class CombatSimpleBlocker : MonoBehaviour, ICombatGridBlocker
{
    [System.Serializable]
    public struct Cell
    {
        public int x;
        public int y;
    }

    public List<Cell> blockedCells = new List<Cell>();

    private HashSet<long> _set;

    private void Awake()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        _set = new HashSet<long>();
        for (int i = 0; i < blockedCells.Count; i++)
        {
            long key = ((long)blockedCells[i].x << 32) ^ (uint)blockedCells[i].y;
            _set.Add(key);
        }
    }

    public bool IsBlocked(int x, int y)
    {
        if (_set == null) Rebuild();
        long key = ((long)x << 32) ^ (uint)y;
        return _set.Contains(key);
    }
}
