using UnityEngine;
using System;
using System.Collections.Generic;

public class EngraveManager : MonoBehaviour
{
    public static EngraveManager Instance { get; private set; }

    [SerializeField] private EngraveTreeData treeData;

    // nodeId -> 투자한 포인트 수
    private Dictionary<string, int> investedPoints = new Dictionary<string, int>();
    private int availableMetaPoints;

    private const string SAVE_KEY_INVESTED = "Engrave_InvestedPoints";
    private const string SAVE_KEY_META = "Engrave_MetaPoints";

    // === 이벤트 ===
    // UI 갱신용. 트리 변경 시 발행
    public event Action OnTreeChanged;
    // 메타 포인트 변동 시 발행
    public event Action<int> OnMetaPointsChanged;

    // === 프로퍼티 ===
    public EngraveTreeData TreeData => treeData;
    public int AvailableMetaPoints => availableMetaPoints;

    // 전체 투자한 포인트 총합
    public int TotalInvestedPoints
    {
        get
        {
            int total = 0;
            foreach (var kvp in investedPoints) total += kvp.Value;
            return total;
        }
    }

    // =========================================================
    // 라이프사이클
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // =========================================================
    // 조회
    // =========================================================

    /// <summary>특정 노드에 투자된 포인트</summary>
    public int GetInvestedPoints(string nodeId)
    {
        return investedPoints.TryGetValue(nodeId, out int pts) ? pts : 0;
    }

    /// <summary>특정 노드에 투자된 포인트 (EngraveNodeData 직접 전달)</summary>
    public int GetInvestedPoints(EngraveNodeData node)
    {
        return GetInvestedPoints(node.nodeId);
    }

    /// <summary>해당 카테고리에서 1포인트 이상 투자된 노드 수</summary>
    public int GetCategoryInvestedNodeCount(EngraveCategory category)
    {
        int count = 0;
        foreach (var node in treeData.GetNodesByCategory(category))
        {
            if (GetInvestedPoints(node.nodeId) > 0)
                count++;
        }
        return count;
    }

    /// <summary>해당 카테고리의 총 투자 포인트</summary>
    public int GetCategoryTotalPoints(EngraveCategory category)
    {
        int total = 0;
        foreach (var node in treeData.GetNodesByCategory(category))
        {
            total += GetInvestedPoints(node.nodeId);
        }
        return total;
    }

    /// <summary>노드가 만렙(maxPoints)까지 찍혔는지</summary>
    public bool IsNodeMaxed(EngraveNodeData node)
    {
        return GetInvestedPoints(node.nodeId) >= node.maxPoints;
    }

    /// <summary>노드가 1포인트라도 투자됐는지</summary>
    public bool IsNodeInvested(EngraveNodeData node)
    {
        return GetInvestedPoints(node.nodeId) > 0;
    }

    // =========================================================
    // 투자 가능 여부 판정
    // =========================================================

    /// <summary>노드에 포인트 투자 가능 여부</summary>
    public bool CanInvest(EngraveNodeData node)
    {
        // 메타 포인트 부족
        if (availableMetaPoints <= 0) return false;

        // 이미 만렙
        if (GetInvestedPoints(node.nodeId) >= node.maxPoints) return false;

        // 선행 노드 확인: 모든 선행 노드가 만렙이어야 함
        foreach (var prereq in node.prerequisites)
        {
            if (!IsNodeMaxed(prereq)) return false;
        }

        // 고급 노드: 해당 카테고리 기본 노드 5개 모두 1포인트 이상 투자 필요
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

    /// <summary>
    /// 노드의 잠금 해제 여부 (투자 가능 상태 or 이미 투자됨).
    /// 선행 노드 충족 + 고급 해금 조건 충족이면 true.
    /// CanInvest와 다른 점: 이미 만렙이거나 포인트 부족해도 true 반환 가능.
    /// UI에서 "잠김/해금" 시각 상태 판별용.
    /// </summary>
    public bool IsNodeUnlocked(EngraveNodeData node)
    {
        // 선행 노드 확인
        foreach (var prereq in node.prerequisites)
        {
            if (!IsNodeMaxed(prereq)) return false;
        }

        // 고급 노드 해금 조건
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

    // =========================================================
    // 투자 / 초기화
    // =========================================================

    /// <summary>노드에 1포인트 투자. 성공 시 true.</summary>
    public bool Invest(EngraveNodeData node)
    {
        if (!CanInvest(node)) return false;

        investedPoints[node.nodeId] = GetInvestedPoints(node.nodeId) + 1;
        availableMetaPoints--;

        Save();
        OnTreeChanged?.Invoke();
        OnMetaPointsChanged?.Invoke(availableMetaPoints);
        return true;
    }

    /// <summary>메타 포인트 추가 (사망 시 경험치 환산 후 호출)</summary>
    public void AddMetaPoints(int amount)
    {
        if (amount <= 0) return;
        availableMetaPoints += amount;
        Save();
        OnMetaPointsChanged?.Invoke(availableMetaPoints);
    }

    /// <summary>
    /// 각인 트리 전체 초기화. 투자한 포인트 전액 환불.
    /// 인게임 특정 아이템 사용 시 호출.
    /// </summary>
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

    // =========================================================
    // 저장 / 불러오기 (PlayerPrefs + JSON)
    // =========================================================

    private void Save()
    {
        // 투자 현황 저장
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

        // 메타 포인트 저장
        PlayerPrefs.SetInt(SAVE_KEY_META, availableMetaPoints);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        investedPoints.Clear();

        // 투자 현황 불러오기
        string json = PlayerPrefs.GetString(SAVE_KEY_INVESTED, "");
        if (!string.IsNullOrEmpty(json))
        {
            var saveData = JsonUtility.FromJson<EngraveSaveData>(json);
            if (saveData != null && saveData.entries != null)
            {
                foreach (var entry in saveData.entries)
                {
                    investedPoints[entry.nodeId] = entry.points;
                }
            }
        }

        // 메타 포인트 불러오기
        availableMetaPoints = PlayerPrefs.GetInt(SAVE_KEY_META, 0);
    }

    /// <summary>저장 데이터 완전 삭제 (디버그용)</summary>
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

// =========================================================
// 저장용 직렬화 클래스
// =========================================================

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