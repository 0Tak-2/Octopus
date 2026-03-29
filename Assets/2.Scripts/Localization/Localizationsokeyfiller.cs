using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 모든 ScriptableObject에 nameKey/descKey를 자동 입력하는 도구.
/// 메뉴: OCTO > Localization > Auto-Fill SO Keys
/// 이미 값이 있는 필드는 건너뜁니다.
/// </summary>
public class LocalizationSOKeyFiller
{
    [MenuItem("OCTO/Localization/Auto-Fill SO Keys")]
    public static void AutoFillAllKeys()
    {
        int totalFilled = 0;

        totalFilled += FillItemDataKeys();
        totalFilled += FillEquipmentKeys();
        totalFilled += FillColorModuleKeys();
        totalFilled += FillRelicKeys();

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("완료",
            $"총 {totalFilled}개 필드에 키를 입력했습니다.\n(이미 값이 있는 필드는 건너뜀)",
            "확인");
    }

    // ========================================================
    // ItemData
    // ========================================================
    private static int FillItemDataKeys()
    {
        var map = new Dictionary<string, (string nameKey, string descKey)>
        {
            // ═══ Consumables ═══
            { "Data_InkCoagulant",       ("ITEM_NAME_INK_COAGULANT",       "ITEM_DESC_INK_COAGULANT") },
            { "Data_ParalysisOintment",  ("ITEM_NAME_PARALYSIS_OINTMENT",  "ITEM_DESC_PARALYSIS_OINTMENT") },
            { "Data_SpongeBandage",      ("ITEM_NAME_SPONGE_BANDAGE",      "ITEM_DESC_SPONGE_BANDAGE") },

            // ═══ Food ═══
            { "Data_GrilledCrustacean",  ("ITEM_NAME_GRILLED_CRUSTACEAN",  "ITEM_DESC_GRILLED_CRUSTACEAN") },
            { "Data_GrilledFish",        ("ITEM_NAME_GRILLED_FISH",        "ITEM_DESC_GRILLED_FISH") },
            { "Data_GrilledSquid",       ("ITEM_NAME_GRILLED_SQUID",       "ITEM_DESC_GRILLED_SQUID") },
            { "Data_JellyfishJelly",     ("ITEM_NAME_JELLYFISH_JELLY",     "ITEM_DESC_JELLYFISH_JELLY") },
            { "Data_RawClamMeat",        ("ITEM_NAME_RAW_CLAM",            "ITEM_DESC_RAW_CLAM") },
            { "Data_RawCrustaceanMeat",  ("ITEM_NAME_RAW_CRUSTACEAN",      "ITEM_DESC_RAW_CRUSTACEAN") },
            { "Data_RawFishMeat",        ("ITEM_NAME_RAW_FISH",            "ITEM_DESC_RAW_FISH") },
            { "Data_RawJellyfishJelly",  ("ITEM_NAME_RAW_JELLYFISH",       "ITEM_DESC_RAW_JELLYFISH") },
            { "Data_RawSquidMeat",       ("ITEM_NAME_RAW_SQUID",           "ITEM_DESC_RAW_SQUID") },
            { "Data_SteamedClam",        ("ITEM_NAME_STEAMED_CLAM",        "ITEM_DESC_STEAMED_CLAM") },

            // ═══ Materials ═══
            { "Data_FlexibleBone",       ("ITEM_NAME_FLEXIBLE_BONE",       "ITEM_DESC_FLEXIBLE_BONE") },
            { "Data_HardScale",          ("ITEM_NAME_HARD_SCALE",          "ITEM_DESC_HARD_SCALE") },
            { "Data_HardShell",          ("ITEM_NAME_HARD_SHELL",          "ITEM_DESC_HARD_SHELL") },
            { "Data_InkSac",             ("ITEM_NAME_INK_SAC",             "ITEM_DESC_INK_SAC") },
            { "Data_JellyfishTentacle",  ("ITEM_NAME_JELLYFISH_TENTACLE",  "ITEM_DESC_JELLYFISH_TENTACLE") },
            { "Data_ParalyzingMucus",    ("ITEM_NAME_PARALYZING_MUCUS",    "ITEM_DESC_PARALYZING_MUCUS") },
            { "Data_SeahorseScale",      ("ITEM_NAME_SEAHORSE_SCALE",      "ITEM_DESC_SEAHORSE_SCALE") },
            { "Data_SharpTail",          ("ITEM_NAME_SHARP_TAIL",          "ITEM_DESC_SHARP_TAIL") },
            { "Data_ShellPiece",         ("ITEM_NAME_SHELL_PIECE",         "ITEM_DESC_SHELL_PIECE") },
            { "Data_SmallBone",          ("ITEM_NAME_SMALL_BONE",          "ITEM_DESC_SMALL_BONE") },
            { "Data_SwordfishHorn",      ("ITEM_NAME_SWORDFISH_HORN",      "ITEM_DESC_SWORDFISH_HORN") },

            // ═══ Resources ═══
            { "Data_Coral",              ("ITEM_NAME_CORAL",               "ITEM_DESC_CORAL") },
            { "Data_Rock",               ("ITEM_NAME_ROCK",                "ITEM_DESC_ROCK") },
            { "Data_Sand",               ("ITEM_NAME_SAND",                "ITEM_DESC_SAND") },
            { "Data_Seaweed",            ("ITEM_NAME_SEAWEED",             "ITEM_DESC_SEAWEED") },
            { "Data_SeaweedFiber",       ("ITEM_NAME_SEAWEED_FIBER",       "ITEM_DESC_SEAWEED_FIBER") },
            { "Data_Sponge",             ("ITEM_NAME_SPONGE",              "ITEM_DESC_SPONGE") },

            // ═══ Crafting / Tools ═══
            { "Data_CoralPickaxe",       ("ITEM_NAME_CORAL_PICKAXE",       "ITEM_DESC_CORAL_PICKAXE") },
            { "Data_CraftingTable",      ("ITEM_NAME_CRAFTING_TABLE",      "ITEM_DESC_CRAFTING_TABLE") },
            { "Data_Shelter",            ("ITEM_NAME_SHELTER",             "ITEM_DESC_SHELTER") },
            { "Data_StonePickaxe",       ("ITEM_NAME_STONE_PICKAXE",       "ITEM_DESC_STONE_PICKAXE") },
            { "Data_StoneShovel",        ("ITEM_NAME_STONE_SHOVEL",        "ITEM_DESC_STONE_SHOVEL") },
            { "Data_Sword",              ("ITEM_NAME_SWORD",               "ITEM_DESC_SWORD") },
        };

        return FillKeys<ItemData>(map, (so, nk, dk) =>
        {
            bool changed = false;
            if (string.IsNullOrEmpty(so.nameKey)) { so.nameKey = nk; changed = true; }
            if (string.IsNullOrEmpty(so.descKey)) { so.descKey = dk; changed = true; }
            return changed;
        });
    }

    // ========================================================
    // EquipmentDefinition
    // ========================================================
    private static int FillEquipmentKeys()
    {
        var map = new Dictionary<string, (string nameKey, string descKey)>
        {
            { "Acc_Bone",           ("EQUIP_NAME_BONE",          "EQUIP_DESC_BONE") },
            { "Acc_Horn",           ("EQUIP_NAME_HORN",          "EQUIP_DESC_HORN") },
            { "Acc_ShellNecklace",  ("EQUIP_NAME_SHELLNECKLACE", "EQUIP_DESC_SHELLNECKLACE") },
            { "Armor_Flexible",     ("EQUIP_NAME_FLEXIBLE",      "EQUIP_DESC_FLEXIBLE") },
            { "Armor_Scale",        ("EQUIP_NAME_SCALE",         "EQUIP_DESC_SCALE") },
            { "Armor_Shell",        ("EQUIP_NAME_SHELL",         "EQUIP_DESC_SHELL") },
            { "Weapon_Bladed",      ("EQUIP_NAME_BLADED",        "EQUIP_DESC_BLADED") },
            { "Weapon_CrudeKnife",  ("EQUIP_NAME_CRUDEKNIFE",    "EQUIP_DESC_CRUDEKNIFE") },
            { "Weapon_Heavy",       ("EQUIP_NAME_HEAVY",         "EQUIP_DESC_HEAVY") },
        };

        return FillKeys<EquipmentDefinition>(map, (so, nk, dk) =>
        {
            bool changed = false;
            if (string.IsNullOrEmpty(so.nameKey)) { so.nameKey = nk; changed = true; }
            if (string.IsNullOrEmpty(so.descKey)) { so.descKey = dk; changed = true; }
            return changed;
        });
    }

    // ========================================================
    // ColorModuleDefinition (Skills + Passives)
    // ========================================================
    private static int FillColorModuleKeys()
    {
        var map = new Dictionary<string, (string nameKey, string descKey)>
        {
            // Passives
            { "Passive_Black_Brave",      ("PASSIVE_NAME_BRAVE",      "PASSIVE_DESC_BRAVE") },
            { "Passive_Black_Confidence", ("PASSIVE_NAME_CONFIDENCE", "PASSIVE_DESC_CONFIDENCE") },
            { "Passive_Black_Cruel",      ("PASSIVE_NAME_CRUEL",      "PASSIVE_DESC_CRUEL") },
            { "Passive_Blue_Basic",       ("PASSIVE_NAME_BASIC",      "PASSIVE_DESC_BASIC") },
            { "Passive_Blue_Stun",        ("PASSIVE_NAME_STUN",       "PASSIVE_DESC_STUN") },
            { "Passive_Red_Predator",     ("PASSIVE_NAME_PREDATOR",   "PASSIVE_DESC_PREDATOR") },
            { "Passive_Red_Sense",        ("PASSIVE_NAME_SENSE",      "PASSIVE_DESC_SENSE") },

            // Skills
            { "Skill_Black_Crit",         ("SKILL_NAME_CRIT",         "SKILL_DESC_CRIT") },
            { "Skill_Black_Rage",         ("SKILL_NAME_RAGE",         "SKILL_DESC_RAGE") },
            { "Skill_Blue_Hard",          ("SKILL_NAME_HARD",         "SKILL_DESC_HARD") },
            { "Skill_Blue_Stretch",       ("SKILL_NAME_STRETCH",      "SKILL_DESC_STRETCH") },
            { "Skill_Red_Dig",            ("SKILL_NAME_DIG",          "SKILL_DESC_DIG") },
            { "Skill_Red_Sharp",          ("SKILL_NAME_SHARP",        "SKILL_DESC_SHARP") },
        };

        return FillKeys<ColorModuleDefinition>(map, (so, nk, dk) =>
        {
            bool changed = false;
            if (string.IsNullOrEmpty(so.nameKey)) { so.nameKey = nk; changed = true; }
            if (string.IsNullOrEmpty(so.descKey)) { so.descKey = dk; changed = true; }
            return changed;
        });
    }

    // ========================================================
    // RelicDefinition
    // ========================================================
    private static int FillRelicKeys()
    {
        // 유물은 아직 로컬라이제이션 테이블에 키가 없으므로
        // 에셋 이름 기반으로 자동 생성합니다.
        // 나중에 테이블에 해당 키를 추가하면 됩니다.
        var guids = AssetDatabase.FindAssets("t:RelicDefinition");
        int filled = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<RelicDefinition>(path);
            if (so == null) continue;

            string assetName = so.name; // 예: "Relic_CrackedShell"
            string keyBase = AssetNameToKey(assetName, "Relic_");

            bool changed = false;
            if (string.IsNullOrEmpty(so.nameKey))
            {
                so.nameKey = $"RELIC_NAME_{keyBase}";
                changed = true;
            }
            if (string.IsNullOrEmpty(so.descKey))
            {
                so.descKey = $"RELIC_DESC_{keyBase}";
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(so);
                filled++;
                Debug.Log($"[KeyFiller] {assetName} → {so.nameKey}, {so.descKey}");
            }
        }

        return filled;
    }

    // ========================================================
    // 공통 유틸리티
    // ========================================================

    /// <summary>
    /// 에셋 이름에서 접두어를 제거하고 UPPER_SNAKE_CASE로 변환
    /// 예: "Relic_CrackedShell" → "CRACKED_SHELL"
    /// </summary>
    private static string AssetNameToKey(string assetName, string prefix)
    {
        string name = assetName;
        if (name.StartsWith(prefix))
            name = name.Substring(prefix.Length);

        // CamelCase → UPPER_SNAKE_CASE
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                result.Append('_');
            result.Append(char.ToUpper(c));
        }
        return result.ToString();
    }

    /// <summary>
    /// 지정된 타입의 SO를 모두 찾아서 매핑에 따라 키를 입력
    /// </summary>
    private static int FillKeys<T>(
        Dictionary<string, (string nameKey, string descKey)> map,
        System.Func<T, string, string, bool> applyFunc) where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        int filled = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<T>(path);
            if (so == null) continue;

            if (map.TryGetValue(so.name, out var keys))
            {
                if (applyFunc(so, keys.nameKey, keys.descKey))
                {
                    EditorUtility.SetDirty(so);
                    filled++;
                    Debug.Log($"[KeyFiller] {so.name} → {keys.nameKey}, {keys.descKey}");
                }
            }
            else
            {
                Debug.LogWarning($"[KeyFiller] 매핑 없음: {so.name} (건너뜀)");
            }
        }

        return filled;
    }
}