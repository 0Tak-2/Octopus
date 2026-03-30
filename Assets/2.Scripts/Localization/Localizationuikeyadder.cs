using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 기존 LocalizationTable에 UI 키를 추가하는 도구.
/// 메뉴: OCTO > Localization > Add UI Keys
/// 이미 있는 키는 건너뛰므로 중복 걱정 없음.
/// </summary>
public class LocalizationUIKeyAdder
{
    [MenuItem("OCTO/Localization/Add UI Keys")]
    public static void AddUIKeys()
    {
        // KO 테이블 찾기
        string[] koGuids = AssetDatabase.FindAssets("LocalizationTable_KO t:LocalizationTable");
        string[] enGuids = AssetDatabase.FindAssets("LocalizationTable_EN t:LocalizationTable");

        if (koGuids.Length == 0 || enGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("오류",
                "LocalizationTable_KO 또는 LocalizationTable_EN을 찾을 수 없습니다.\n" +
                "먼저 OCTO > Localization > Generate Tables를 실행하세요.", "확인");
            return;
        }

        var koTable = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
            AssetDatabase.GUIDToAssetPath(koGuids[0]));
        var enTable = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
            AssetDatabase.GUIDToAssetPath(enGuids[0]));

        int koAdded = AddKeysToTable(koTable, GetKoreanUIKeys());
        int enAdded = AddKeysToTable(enTable, GetEnglishUIKeys());

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("완료",
            $"KO 테이블: {koAdded}개 추가\nEN 테이블: {enAdded}개 추가\n\n(이미 있는 키는 건너뜀)",
            "확인");
    }

    private static int AddKeysToTable(LocalizationTable table, Dictionary<string, string> keys)
    {
        Undo.RecordObject(table, "Add UI Localization Keys");

        var existingKeys = new HashSet<string>(table.entries.Select(e => e.key));
        int added = 0;

        foreach (var kvp in keys)
        {
            if (existingKeys.Contains(kvp.Key))
                continue;

            table.entries.Add(new LocalizationEntry { key = kvp.Key, value = kvp.Value });
            added++;
        }

        EditorUtility.SetDirty(table);
        return added;
    }

    private static Dictionary<string, string> GetKoreanUIKeys()
    {
        return new Dictionary<string, string>
        {
            // UI 일반
            { "UI_SKILL", "스킬" },
            { "UI_PASSIVE", "패시브" },
            { "UI_COLOR_RED", "빨강" },
            { "UI_COLOR_BLUE", "파랑" },
            { "UI_COLOR_BLACK", "검정" },
            { "UI_NO_STATS", "스탯 없음" },
            { "UI_EQUIP_BONUS", "장비 보너스" },
            { "UI_REQUIRED_MATERIALS", "필요 재료" },
            { "UI_YOU_DIED", "사망" },
            { "UI_HINT_MOVE", "이동할 위치를 클릭 (우클릭/ESC 취소)" },
            { "UI_HINT_ATTACK", "적을 클릭하여 공격 (우클릭/ESC 취소)" },
            { "UI_CHARACTER_OCTOPUS", "문어" },
            { "UI_CHARACTER_OCTOPUS_DESC", "조류를 거슬러 헤엄치는 외로운 문어.\n\n바다의 기억을 모아, 앞으로 나아간다." },

            // 스탯
            { "STAT_ATK", "공격력" },
            { "STAT_DEF", "방어력" },
            { "STAT_HP", "최대HP" },
            { "STAT_EVA", "회피율" },
            { "STAT_CRIT", "치명타" },
            { "STAT_CRIT_DMG", "치명타 피해" },
            { "STAT_STUN_CHANCE", "스턴 확률" },

            // 상태이상
            { "STATUS_BLEED", "출혈" },
            { "STATUS_STUN", "스턴" },
            { "STATUS_IMPERFECT", "불완전" },
        };
    }

    private static Dictionary<string, string> GetEnglishUIKeys()
    {
        return new Dictionary<string, string>
        {
            // UI General
            { "UI_SKILL", "Skill" },
            { "UI_PASSIVE", "Passive" },
            { "UI_COLOR_RED", "Red" },
            { "UI_COLOR_BLUE", "Blue" },
            { "UI_COLOR_BLACK", "Black" },
            { "UI_NO_STATS", "No stats" },
            { "UI_EQUIP_BONUS", "Equipment bonus" },
            { "UI_REQUIRED_MATERIALS", "Required materials" },
            { "UI_YOU_DIED", "You Died" },
            { "UI_HINT_MOVE", "Click a tile to move (Right-click/ESC to cancel)" },
            { "UI_HINT_ATTACK", "Click an enemy to attack (Right-click/ESC to cancel)" },
            { "UI_CHARACTER_OCTOPUS", "Octopus" },
            { "UI_CHARACTER_OCTOPUS_DESC", "A lonely octopus swimming against the current.\n\nGathering the sea's memories, pressing forward." },

            // Stats
            { "STAT_ATK", "ATK" },
            { "STAT_DEF", "DEF" },
            { "STAT_HP", "Max HP" },
            { "STAT_EVA", "EVA" },
            { "STAT_CRIT", "CRIT" },
            { "STAT_CRIT_DMG", "CRIT DMG" },
            { "STAT_STUN_CHANCE", "Stun chance" },

            // Status Effects
            { "STATUS_BLEED", "Bleed" },
            { "STATUS_STUN", "Stun" },
            { "STATUS_IMPERFECT", "Imperfect" },
        };
    }
}