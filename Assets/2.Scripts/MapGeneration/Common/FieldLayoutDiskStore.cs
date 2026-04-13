using System;
using UnityEngine;

/// <summary>
/// 필드별 맵 시드 + 출구/입구 통로 셀을 PlayerPrefs에 저장해
/// 게임을 껐다 켜도 같은 맵·같은 통로로 왕복할 수 있게 합니다.
/// </summary>
public static class FieldLayoutDiskStore
{
    private const string Prefix = "FieldLayout_v1_";

    [Serializable]
    private class Payload
    {
        public string mapKeyEcho;
        public int mapSeed;
        public int exitX, exitY, entX, entY;
        public bool hasPortals;
    }

    private static string Key(string mapKey) => Prefix + (mapKey ?? "").GetHashCode().ToString("X8");

    public static bool TryLoad(string mapKey, out int mapSeed, out Vector2Int exit, out Vector2Int entrance, out bool hasPortals)
    {
        mapSeed = 0;
        exit = entrance = Vector2Int.zero;
        hasPortals = false;

        string raw = PlayerPrefs.GetString(Key(mapKey), "");
        if (string.IsNullOrEmpty(raw)) return false;

        try
        {
            var p = JsonUtility.FromJson<Payload>(raw);
            if (p == null || p.mapKeyEcho != mapKey) return false;
            mapSeed = p.mapSeed;
            hasPortals = p.hasPortals;
            if (hasPortals)
            {
                exit = new Vector2Int(p.exitX, p.exitY);
                entrance = new Vector2Int(p.entX, p.entY);
            }
            return mapSeed != 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(string mapKey, int mapSeed, Vector2Int exit, Vector2Int entrance, bool hasPortals)
    {
        var p = new Payload
        {
            mapKeyEcho = mapKey,
            mapSeed = mapSeed,
            exitX = exit.x,
            exitY = exit.y,
            entX = entrance.x,
            entY = entrance.y,
            hasPortals = hasPortals
        };
        PlayerPrefs.SetString(Key(mapKey), JsonUtility.ToJson(p));
        PlayerPrefs.Save();
    }

    public static void ClearKey(string mapKey)
    {
        PlayerPrefs.DeleteKey(Key(mapKey));
    }

    /// <summary>새 게임 등 — 저장된 필드 레이아웃 전부 제거은 호출부에서 키 나열 어려우면 스킵 가능</summary>
    public static void ClearAllKnownPrefixes()
    {
        // PlayerPrefs에는 prefix 일괄 삭제 API 없음 — Reset 시 GameManager에서 필드 키별 ClearKey 호출 권장
    }
}
