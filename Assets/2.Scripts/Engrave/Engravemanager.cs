using UnityEngine;
using System;
using System.Collections.Generic;

public class EngraveManager : MonoBehaviour
{
    public static EngraveManager Instance { get; private set; }

    [SerializeField] private EngraveTreeData treeData;

    // nodeId(�Ǵ� GetStableNodeId) -> ������ ����Ʈ ��
    private Dictionary<string, int> investedPoints = new Dictionary<string, int>();
    private int availableMetaPoints;

    private const string SAVE_KEY_INVESTED = "Engrave_InvestedPoints";
    private const string SAVE_KEY_META = "Engrave_MetaPoints";

    public event Action OnTreeChanged;
    public event Action<int> OnMetaPointsChanged;

    public EngraveTreeData TreeData => treeData;
    public int AvailableMetaPoints => availableMetaPoints;

    public int TotalInvestedPoints
    {
        get
        {
            int total = 0;
            foreach (var kvp in investedPoints) total += kvp.Value;
            return total;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public int GetInvestedPoints(string nodeKey)
    {
        return investedPoints.TryGetValue(nodeKey, out int pts) ? pts : 0;
    }

    public int GetInvestedPoints(EngraveNodeData node)
    {
        return node == null ? 0 : GetInvestedPoints(node.GetStableNodeId());
    }

    public int GetCategoryInvestedNodeCount(EngraveCategory category)
    {
        int count = 0;
        foreach (var node in treeData.GetNodesByCategory(category))
        {
            if (GetInvestedPoints(node) > 0)
                count++;
        }
        return count;
    }

    public int GetCategoryTotalPoints(EngraveCategory category)
    {
        int total = 0;
        foreach (var node in treeData.GetNodesByCategory(category))
        {
            total += GetInvestedPoints(node);
        }
        return total;
    }

    public bool IsNodeMaxed(EngraveNodeData node)
    {
        return GetInvestedPoints(node) >= node.maxPoints;
    }

    public bool IsNodeInvested(EngraveNodeData node)
    {
        return GetInvestedPoints(node) > 0;
    }

    public bool CanInvest(EngraveNodeData node)
    {
        if (availableMetaPoints <= 0) return false;

        if (GetInvestedPoints(node) >= node.maxPoints) return false;

        foreach (var prereq in node.prerequisites)
        {
            if (!IsNodeMaxed(prereq)) return false;
        }

        if (node.tier == EngraveNodeTier.Advanced)
        {
            var basicNodes = treeData.GetBasicNodes(node.category);
            foreach (var basic in basicNodes)
            {
                if (!IsNodeInvested(basic)) return false;
            }
        }

        return true;
    }

    public bool IsNodeUnlocked(EngraveNodeData node)
    {
        foreach (var prereq in node.prerequisites)
        {
            if (!IsNodeMaxed(prereq)) return false;
        }

        if (node.tier == EngraveNodeTier.Advanced)
        {
            var basicNodes = treeData.GetBasicNodes(node.category);
            foreach (var basic in basicNodes)
            {
                if (!IsNodeInvested(basic)) return false;
            }
        }

        return true;
    }

    public bool Invest(EngraveNodeData node)
    {
        if (!CanInvest(node)) return false;

        string key = node.GetStableNodeId();
        investedPoints[key] = GetInvestedPoints(key) + 1;
        availableMetaPoints--;

        Save();
        OnTreeChanged?.Invoke();
        OnMetaPointsChanged?.Invoke(availableMetaPoints);
        return true;
    }

    public void AddMetaPoints(int amount)
    {
        if (amount <= 0) return;
        availableMetaPoints += amount;
        Save();
        OnMetaPointsChanged?.Invoke(availableMetaPoints);
    }

    public void ResetTree()
    {
        int refunded = 0;
        foreach (var kvp in investedPoints)
            refunded += kvp.Value;

        investedPoints.Clear();
        availableMetaPoints += refunded;

        Save();
        OnTreeChanged?.Invoke();
        OnMetaPointsChanged?.Invoke(availableMetaPoints);
    }

    private void Save()
    {
        var saveData = new EngraveSaveData();
        foreach (var kvp in investedPoints)
        {
            saveData.entries.Add(new EngraveSaveEntry
            {
                nodeId = kvp.Key,
                points = kvp.Value
            });
        }
        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SAVE_KEY_INVESTED, json);

        PlayerPrefs.SetInt(SAVE_KEY_META, availableMetaPoints);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        investedPoints.Clear();

        string json = PlayerPrefs.GetString(SAVE_KEY_INVESTED, "");
        if (!string.IsNullOrEmpty(json))
        {
            var saveData = JsonUtility.FromJson<EngraveSaveData>(json);
            if (saveData != null && saveData.entries != null)
            {
                foreach (var entry in saveData.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.nodeId))
                        continue;
                    investedPoints[entry.nodeId] = entry.points;
                }
            }
        }

        availableMetaPoints = PlayerPrefs.GetInt(SAVE_KEY_META, 0);

        MigrateLegacyEmptyNodeIdSave();
    }

    /// <summary>������: ��� ��尡 �� nodeId�� ���� Ű�� ���� ����� ���� �� ��Ÿ ����Ʈ ȯ��.</summary>
    private void MigrateLegacyEmptyNodeIdSave()
    {
        string legacyJson = PlayerPrefs.GetString(SAVE_KEY_INVESTED, "");
        if (string.IsNullOrEmpty(legacyJson))
            return;

        var saveData = JsonUtility.FromJson<EngraveSaveData>(legacyJson);
        if (saveData?.entries == null)
            return;

        int pooled = 0;
        var rebuilt = new List<EngraveSaveEntry>();
        foreach (var entry in saveData.entries)
        {
            if (entry == null) continue;
            if (string.IsNullOrEmpty(entry.nodeId))
                pooled += entry.points;
            else
                rebuilt.Add(entry);
        }

        if (pooled <= 0)
            return;

        saveData.entries = rebuilt;
        availableMetaPoints += pooled;
        string newJson = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SAVE_KEY_INVESTED, newJson);
        PlayerPrefs.SetInt(SAVE_KEY_META, availableMetaPoints);
        PlayerPrefs.Save();

        investedPoints.Clear();
        foreach (var entry in rebuilt)
            investedPoints[entry.nodeId] = entry.points;

        Debug.LogWarning("[EngraveManager] Legacy empty nodeId entries removed; " + pooled + " pts refunded to meta.");
    }

    public void DeleteAllSaveData()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY_INVESTED);
        PlayerPrefs.DeleteKey(SAVE_KEY_META);
        PlayerPrefs.Save();
        investedPoints.Clear();
        availableMetaPoints = 0;
        OnTreeChanged?.Invoke();
        OnMetaPointsChanged?.Invoke(availableMetaPoints);
    }
}

[Serializable]
public class EngraveSaveData
{
    public List<EngraveSaveEntry> entries = new List<EngraveSaveEntry>();
}

[Serializable]
public class EngraveSaveEntry
{
    public string nodeId;
    public int points;
}
