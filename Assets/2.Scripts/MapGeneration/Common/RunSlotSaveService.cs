using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런 세이브 슬롯(최대 10개) 파일 저장/로드 서비스.
/// - 타이틀 "이어하기"는 최신 슬롯을 로드한다.
/// - DeathManager는 활성 슬롯 파일을 삭제한다.
/// </summary>
public static class RunSlotSaveService
{
    public const int MaxSlots = 10;
    private const string ActiveSlotPrefKey = "RunSlot_Active";

    private static string _pendingLoadJson;

    [Serializable]
    private class RunSnapshot
    {
        public int slot;
        public long savedUnixTime;
        public int currentChapter;
        public int currentMapIndex;
        public List<string> clearedDungeons = new List<string>();
        public PlayerSaveData playerData = new PlayerSaveData();
        public List<InventoryItemSave> inventoryData = new List<InventoryItemSave>();
        public List<FieldMapEntry> fieldMapDataList = new List<FieldMapEntry>();
        public List<DungeonDataEntry> allDungeonData = new List<DungeonDataEntry>();
        public DungeonSaveData currentDungeonData;
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

    public static void SaveActiveRun(GameManager gm)
    {
        if (gm == null) return;
        SaveToSlot(GetActiveSlot(), gm);
    }

    public static void SaveToSlot(int slot, GameManager gm)
    {
        if (gm == null) return;
        slot = Mathf.Clamp(slot, 1, MaxSlots);

        var snap = new RunSnapshot
        {
            slot = slot,
            savedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            currentChapter = gm.currentChapter,
            currentMapIndex = gm.currentMapIndex,
            clearedDungeons = Clone(gm.clearedDungeons) ?? new List<string>(),
            playerData = Clone(gm.playerData) ?? new PlayerSaveData(),
            inventoryData = Clone(gm.inventoryData) ?? new List<InventoryItemSave>(),
            fieldMapDataList = Clone(gm.fieldMapDataList) ?? new List<FieldMapEntry>(),
            allDungeonData = Clone(gm.allDungeonData) ?? new List<DungeonDataEntry>(),
            currentDungeonData = Clone(gm.currentDungeonData),
        };

        File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(snap));
        SetActiveSlot(slot);
    }

    public static void StartNewRunInSlot(int slot, bool deleteExisting)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        SetActiveSlot(slot);
        _pendingLoadJson = null;
        if (deleteExisting)
            DeleteSlot(slot);
    }

    public static bool PrepareContinueLatest()
    {
        int slot = GetLatestSlot();
        if (slot <= 0) return false;
        if (!TryReadRawJson(slot, out _pendingLoadJson)) return false;
        SetActiveSlot(slot);
        return true;
    }

    public static void TryConsumePendingInto(GameManager gm)
    {
        if (gm == null) return;
        if (string.IsNullOrEmpty(_pendingLoadJson)) return;
        var snap = JsonUtility.FromJson<RunSnapshot>(_pendingLoadJson);
        _pendingLoadJson = null;
        if (snap == null) return;

        gm.currentChapter = snap.currentChapter;
        gm.currentMapIndex = snap.currentMapIndex;
        gm.clearedDungeons = Clone(snap.clearedDungeons) ?? new List<string>();
        gm.playerData = Clone(snap.playerData) ?? new PlayerSaveData();
        gm.inventoryData = Clone(snap.inventoryData) ?? new List<InventoryItemSave>();
        gm.fieldMapDataList = Clone(snap.fieldMapDataList) ?? new List<FieldMapEntry>();
        gm.allDungeonData = Clone(snap.allDungeonData) ?? new List<DungeonDataEntry>();
        gm.currentDungeonData = Clone(snap.currentDungeonData);
    }

    public static void DeleteActiveSlot()
    {
        DeleteSlot(GetActiveSlot());
    }

    public static void DeleteSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        string p = SlotPath(slot);
        if (File.Exists(p))
            File.Delete(p);
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
