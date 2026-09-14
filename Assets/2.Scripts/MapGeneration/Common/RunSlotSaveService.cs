using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public enum RunSessionIntent
{
    None,
    NewCharacter,
    ContinueSlot,
}

/// <summary>
/// 런 세이브 슬롯(최대 10개) 파일 저장/로드 서비스.
/// 각 슬롯 = 유저가 이름을 지은 캐릭터 1명.
/// </summary>
public static class RunSlotSaveService
{
    public const int MaxSlots = 10;
    public const int MaxCharacterNameLength = 20;
    private const string ActiveSlotPrefKey = "RunSlot_Active";

    private static string _pendingLoadJson;
    private static string _pendingNewCharacterName;
    private static RunSessionIntent _sessionIntent = RunSessionIntent.None;

    [Serializable]
    private class RunSnapshot
    {
        public int slot;
        public string characterName;
        public long savedUnixTime;
        public int currentChapter;
        public int currentMapIndex;
        public List<string> clearedDungeons = new List<string>();
        public PlayerSaveData playerData = new PlayerSaveData();
        public List<InventoryItemSave> inventoryData = new List<InventoryItemSave>();
        public List<FieldMapEntry> fieldMapDataList = new List<FieldMapEntry>();
        public List<DungeonDataEntry> allDungeonData = new List<DungeonDataEntry>();
        public DungeonSaveData currentDungeonData;
        public RunStatistics runStats = new RunStatistics();
        // 챕터 진행도. WorldProgressManager 는 PlayerPrefs(슬롯 공용)에만 들고 있어서
        // 슬롯별로 남기지 않으면 이어하기 때 1챕터로 되돌아간다.
        public int currentFieldIndex;
    }

    private static string SlotsDir
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "RunSlots");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SlotPath(int slot) => Path.Combine(SlotsDir, $"slot_{slot}.json");

    public static int GetActiveSlot()
    {
        int slot = PlayerPrefs.GetInt(ActiveSlotPrefKey, 1);
        return Mathf.Clamp(slot, 1, MaxSlots);
    }

    public static void SetActiveSlot(int slot)
    {
        PlayerPrefs.SetInt(ActiveSlotPrefKey, Mathf.Clamp(slot, 1, MaxSlots));
        PlayerPrefs.Save();
    }

    public static bool HasAnySlot()
    {
        for (int i = 1; i <= MaxSlots; i++)
            if (File.Exists(SlotPath(i)))
                return true;
        return false;
    }

    public static bool HasEmptySlot()
    {
        for (int i = 1; i <= MaxSlots; i++)
            if (!File.Exists(SlotPath(i)))
                return true;
        return false;
    }

    public static int GetFirstEmptySlot()
    {
        for (int i = 1; i <= MaxSlots; i++)
            if (!File.Exists(SlotPath(i)))
                return i;
        return -1;
    }

    public static int GetLatestSlot()
    {
        int bestSlot = -1;
        long bestTime = long.MinValue;
        for (int i = 1; i <= MaxSlots; i++)
        {
            if (!TryReadSnapshot(i, out var snap))
                continue;
            if (snap.savedUnixTime > bestTime)
            {
                bestTime = snap.savedUnixTime;
                bestSlot = i;
            }
        }
        return bestSlot;
    }

    public static List<RunSlotSummary> GetAllSummaries()
    {
        var list = new List<RunSlotSummary>(MaxSlots);
        for (int i = 1; i <= MaxSlots; i++)
            list.Add(BuildSummary(i));
        return list;
    }

    public static RunSlotSummary GetSummary(int slot)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        return BuildSummary(slot);
    }

    private static RunSlotSummary BuildSummary(int slot)
    {
        var summary = new RunSlotSummary { slot = slot, isEmpty = !File.Exists(SlotPath(slot)) };
        if (summary.isEmpty || !TryReadSnapshot(slot, out var snap))
            return summary;

        summary.characterName = snap.characterName;
        summary.savedUnixTime = snap.savedUnixTime;
        summary.currentChapter = snap.currentChapter;
        summary.currentMapIndex = snap.currentMapIndex;
        return summary;
    }

    public static RunSessionIntent PeekSessionIntent() => _sessionIntent;

    public static RunSessionIntent ConsumeSessionIntent()
    {
        var intent = _sessionIntent;
        _sessionIntent = RunSessionIntent.None;
        return intent;
    }

    public static string GetPendingNewCharacterName() => _pendingNewCharacterName ?? "";

    public static void SaveActiveRun(GameManager gm)
    {
        if (gm == null) return;
        SaveToSlot(GetActiveSlot(), gm);
    }

    public static void SaveToSlot(int slot, GameManager gm)
    {
        if (gm == null) return;
        slot = Mathf.Clamp(slot, 1, MaxSlots);

        string characterName = ResolveCharacterName(slot, gm.runCharacterName);

        // 저장 직전에 경과 시간을 적립해야 이어하기에서 플레이 타임이 이어진다.
        gm.AccumulatePlayTime();

        var snap = new RunSnapshot
        {
            slot = slot,
            characterName = characterName,
            savedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            currentChapter = gm.currentChapter,
            currentMapIndex = gm.currentMapIndex,
            clearedDungeons = Clone(gm.clearedDungeons) ?? new List<string>(),
            playerData = Clone(gm.playerData) ?? new PlayerSaveData(),
            inventoryData = Clone(gm.inventoryData) ?? new List<InventoryItemSave>(),
            fieldMapDataList = Clone(gm.fieldMapDataList) ?? new List<FieldMapEntry>(),
            allDungeonData = Clone(gm.allDungeonData) ?? new List<DungeonDataEntry>(),
            currentDungeonData = Clone(gm.currentDungeonData),
            runStats = Clone(gm.runStats) ?? new RunStatistics(),
            currentFieldIndex = WorldProgressManager.Instance != null
                ? WorldProgressManager.Instance.CurrentFieldIndex
                : 0,
        };

        gm.runCharacterName = characterName;
        _pendingNewCharacterName = null;

        File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(snap));
        SetActiveSlot(slot);
    }

    /// <summary>새 캐릭터 생성 후 게임 씬 진입 준비.</summary>
    public static bool PrepareNewCharacter(int slot, string characterName)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        if (File.Exists(SlotPath(slot)))
            return false;

        string sanitized = SanitizeCharacterName(characterName);
        if (string.IsNullOrEmpty(sanitized))
            return false;

        SetActiveSlot(slot);
        _pendingLoadJson = null;
        _pendingNewCharacterName = sanitized;
        _sessionIntent = RunSessionIntent.NewCharacter;
        return true;
    }

    /// <summary>기존 캐릭터(슬롯) 이어하기 준비.</summary>
    public static bool PrepareContinueSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        if (!TryReadRawJson(slot, out _pendingLoadJson))
            return false;

        SetActiveSlot(slot);
        _pendingNewCharacterName = null;
        _sessionIntent = RunSessionIntent.ContinueSlot;
        return true;
    }

    public static bool PrepareContinueLatest() => PrepareContinueSlot(GetLatestSlot());

    public static void StartNewRunInSlot(int slot, bool deleteExisting)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        SetActiveSlot(slot);
        _pendingLoadJson = null;
        _sessionIntent = RunSessionIntent.NewCharacter;
        if (deleteExisting)
            DeleteSlot(slot);
    }

    public static void ApplySessionStart(GameManager gm)
    {
        if (gm == null) return;

        var intent = ConsumeSessionIntent();
        switch (intent)
        {
            case RunSessionIntent.ContinueSlot:
                gm.ResetGame();
                TryConsumePendingInto(gm);
                break;
            case RunSessionIntent.NewCharacter:
                gm.ResetGame();
                gm.runCharacterName = GetPendingNewCharacterName();
                _pendingNewCharacterName = null;
                SaveToSlot(GetActiveSlot(), gm);
                break;
        }
    }

    public static void TryConsumePendingInto(GameManager gm)
    {
        if (gm == null) return;
        if (string.IsNullOrEmpty(_pendingLoadJson)) return;

        var snap = JsonUtility.FromJson<RunSnapshot>(_pendingLoadJson);
        _pendingLoadJson = null;
        if (snap == null) return;

        gm.runCharacterName = string.IsNullOrWhiteSpace(snap.characterName)
            ? $"캐릭터 {snap.slot}"
            : snap.characterName;
        gm.currentChapter = snap.currentChapter;
        gm.currentMapIndex = snap.currentMapIndex;
        gm.clearedDungeons = Clone(snap.clearedDungeons) ?? new List<string>();
        gm.playerData = Clone(snap.playerData) ?? new PlayerSaveData();
        gm.inventoryData = Clone(snap.inventoryData) ?? new List<InventoryItemSave>();
        gm.fieldMapDataList = Clone(snap.fieldMapDataList) ?? new List<FieldMapEntry>();
        gm.allDungeonData = Clone(snap.allDungeonData) ?? new List<DungeonDataEntry>();
        gm.currentDungeonData = Clone(snap.currentDungeonData);
        gm.runStats = Clone(snap.runStats) ?? new RunStatistics();
        gm.ResetPlayTimeAnchor();

        // ResetGame() 이 WorldProgressManager 를 0으로 밀어놓은 뒤라 여기서 되돌린다.
        if (WorldProgressManager.Instance != null)
            WorldProgressManager.Instance.RestoreFieldIndex(snap.currentFieldIndex);
    }

    public static void DeleteActiveSlot() => DeleteSlot(GetActiveSlot());

    public static void DeleteSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        string p = SlotPath(slot);
        if (File.Exists(p))
            File.Delete(p);
    }

    public static string SanitizeCharacterName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        raw = raw.Trim().Replace("\n", "").Replace("\r", "");
        if (raw.Length > MaxCharacterNameLength)
            raw = raw.Substring(0, MaxCharacterNameLength);
        return raw;
    }

    private static string ResolveCharacterName(int slot, string gmName)
    {
        if (!string.IsNullOrWhiteSpace(gmName))
            return SanitizeCharacterName(gmName);

        if (!string.IsNullOrWhiteSpace(_pendingNewCharacterName))
            return _pendingNewCharacterName;

        if (TryReadSnapshot(slot, out var existing) && !string.IsNullOrWhiteSpace(existing.characterName))
            return existing.characterName;

        return $"캐릭터 {slot}";
    }

    private static bool TryReadSnapshot(int slot, out RunSnapshot snap)
    {
        snap = null;
        if (!TryReadRawJson(slot, out var json)) return false;
        snap = JsonUtility.FromJson<RunSnapshot>(json);
        return snap != null;
    }

    private static bool TryReadRawJson(int slot, out string json)
    {
        json = null;
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        string p = SlotPath(slot);
        if (!File.Exists(p)) return false;
        json = File.ReadAllText(p);
        return !string.IsNullOrEmpty(json);
    }

    private static T Clone<T>(T source)
    {
        if (source == null) return default;
        string json = JsonUtility.ToJson(source);
        if (string.IsNullOrEmpty(json)) return default;
        return JsonUtility.FromJson<T>(json);
    }
}
