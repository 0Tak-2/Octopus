using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// OCTO 로컬라이제이션 테이블 자동 생성 도구.
/// 메뉴: OCTO > Localization > Generate Tables
/// 한 번만 실행하면 KO/EN 테이블이 생성됩니다.
/// 위치: Assets/Editor/LocalizationTableGenerator.cs
/// </summary>
public class LocalizationTableGenerator
{
    [MenuItem("OCTO/Localization/Generate Tables")]
    public static void GenerateTables()
    {
        // 저장 경로 확인
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Localization"))
            AssetDatabase.CreateFolder("Assets/Data", "Localization");

        GenerateKoreanTable();
        GenerateEnglishTable();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "로컬라이제이션",
            "Assets/Data/Localization/ 에 KO, EN 테이블이 생성되었습니다!",
            "확인"
        );
    }

    private static void GenerateKoreanTable()
    {
        var table = ScriptableObject.CreateInstance<LocalizationTable>();
        table.language = SystemLanguage.Korean;
        table.entries = new List<LocalizationEntry>();

        // ═══ Passive Skills ═══
        Add(table, "PASSIVE_NAME_BRAVE", "용맹함");
        Add(table, "PASSIVE_DESC_BRAVE", "적 HP가 높을수록 ATK 증가 (100%→+25%)");
        Add(table, "PASSIVE_NAME_CONFIDENCE", "자신감");
        Add(table, "PASSIVE_DESC_CONFIDENCE", "내 HP 비례 ATK 증가 (100%→+20%)");
        Add(table, "PASSIVE_NAME_CRUEL", "악랄함");
        Add(table, "PASSIVE_DESC_CRUEL", "적 HP가 낮을수록 ATK 증가 (0%→+25%)");
        Add(table, "PASSIVE_NAME_BASIC", "기본기");
        Add(table, "PASSIVE_DESC_BASIC", "기본공격 25% 확률로 2회 공격");
        Add(table, "PASSIVE_NAME_STUN", "촉수강화");
        Add(table, "PASSIVE_DESC_STUN", "기본공격 25% 확률로 스턴");
        Add(table, "PASSIVE_NAME_PREDATOR", "포식");
        Add(table, "PASSIVE_DESC_PREDATOR", "적 HP 20% 이하 시 처형 + 피해량 100% 회복");
        Add(table, "PASSIVE_NAME_SENSE", "약자감지");
        Add(table, "PASSIVE_DESC_SENSE", "적 상태이상 1개당 ATK +6% (최대 30%)");

        // ═══ Active Skills ═══
        Add(table, "SKILL_NAME_CRIT", "치명적인 촉수");
        Add(table, "SKILL_DESC_CRIT", "해당 적 첫 공격 시 확정 치명타");
        Add(table, "SKILL_NAME_RAGE", "응징하는 촉수");
        Add(table, "SKILL_DESC_RAGE", "1턴간 ATK 1.75배");
        Add(table, "SKILL_NAME_HARD", "단단한 촉수");
        Add(table, "SKILL_DESC_HARD", "불완전 부여 (최대 4중첩)");
        Add(table, "SKILL_NAME_STRETCH", "늘어나는 촉수");
        Add(table, "SKILL_DESC_STRETCH", "3칸 이내 위치로 이동");
        Add(table, "SKILL_NAME_DIG", "파고드는 촉수");
        Add(table, "SKILL_DESC_DIG", "출혈 부여. 이미 출혈이면 피해의 50% 회복");
        Add(table, "SKILL_NAME_SHARP", "날카로운 촉수");
        Add(table, "SKILL_DESC_SHARP", "출혈 부여. 이미 출혈이면 ATK 40% 추가 피해");

        // ═══ Equipment ═══
        Add(table, "EQUIP_NAME_BONE", "뼈 장신구");
        Add(table, "EQUIP_DESC_BONE", "행운을 부르는 장식");
        Add(table, "EQUIP_NAME_HORN", "뿔 부적");
        Add(table, "EQUIP_DESC_HORN", "행운이 커질 것 같은 부적");
        Add(table, "EQUIP_NAME_SHELLNECKLACE", "조개 목걸이");
        Add(table, "EQUIP_DESC_SHELLNECKLACE", "갖고만 있어도 든든해지는 목걸이");
        Add(table, "EQUIP_NAME_FLEXIBLE", "유연한 갑옷");
        Add(table, "EQUIP_DESC_FLEXIBLE", "물 흐르듯 자연과 하나가 되는 갑옷");
        Add(table, "EQUIP_NAME_SCALE", "비늘 갑옷");
        Add(table, "EQUIP_DESC_SCALE", "비늘로 만들어 단단한 갑옷");
        Add(table, "EQUIP_NAME_SHELL", "껍데기 갑옷");
        Add(table, "EQUIP_DESC_SHELL", "엄청 든든해지는 갑옷");
        Add(table, "EQUIP_NAME_BLADED", "날촉수");
        Add(table, "EQUIP_DESC_BLADED", "아주 날카로워 보이는 촉수");
        Add(table, "EQUIP_NAME_CRUDEKNIFE", "촉수 칼");
        Add(table, "EQUIP_DESC_CRUDEKNIFE", "끝이 날카로운 촉수 칼");
        Add(table, "EQUIP_NAME_HEAVY", "무거운 촉수");
        Add(table, "EQUIP_DESC_HEAVY", "엄청 무거워 보이는 촉수");

        // ═══ Enemies ═══
        Add(table, "ENEMY_NAME_ANCHOVY", "멸치");
        Add(table, "ENEMY_NAME_CLAM", "조개");
        Add(table, "ENEMY_NAME_FLOWERCRAB", "꽃게");
        Add(table, "ENEMY_NAME_JELLYFISH", "해파리");
        Add(table, "ENEMY_NAME_LOBSTER", "랍스터");
        Add(table, "ENEMY_NAME_PENSHELL", "키조개");
        Add(table, "ENEMY_NAME_REDSNAPPER", "도미");
        Add(table, "ENEMY_NAME_SARDINE", "정어리");
        Add(table, "ENEMY_NAME_SEAHORSE", "해마");
        Add(table, "ENEMY_NAME_SQUID", "오징어");
        Add(table, "ENEMY_NAME_SWORDFISH", "황새치");
        Add(table, "ENEMY_NAME_YELLOWRAY", "노랑가오리");

        // ═══ Consumables ═══
        Add(table, "ITEM_NAME_INK_COAGULANT", "먹물 응고제");
        Add(table, "ITEM_DESC_INK_COAGULANT", "상처에 바르면 출혈이 뚝 멈추는 약");
        Add(table, "ITEM_NAME_PARALYSIS_OINTMENT", "마비 연고");
        Add(table, "ITEM_DESC_PARALYSIS_OINTMENT", "뻣뻣해진 몸을 부드럽게 풀어주는 연고");
        Add(table, "ITEM_NAME_SPONGE_BANDAGE", "스펀지 붕대");
        Add(table, "ITEM_DESC_SPONGE_BANDAGE", "폭신폭신해서 감으면 기분 좋은 붕대");

        // ═══ Food ═══
        Add(table, "ITEM_NAME_GRILLED_CRUSTACEAN", "구운 갑각류");
        Add(table, "ITEM_DESC_GRILLED_CRUSTACEAN", "노릇노릇 잘 구워진 갑각류. 든든하다!");
        Add(table, "ITEM_NAME_GRILLED_FISH", "구운 생선");
        Add(table, "ITEM_DESC_GRILLED_FISH", "고소한 냄새가 솔솔 나는 구운 생선");
        Add(table, "ITEM_NAME_GRILLED_SQUID", "구운 오징어");
        Add(table, "ITEM_DESC_GRILLED_SQUID", "쫄깃쫄깃 잘 구워진 오징어. 맛있겠다!");
        Add(table, "ITEM_NAME_JELLYFISH_JELLY", "해파리 젤리");
        Add(table, "ITEM_DESC_JELLYFISH_JELLY", "말랑말랑 투명한 젤리. 살짝 찌릿하다");
        Add(table, "ITEM_NAME_RAW_CLAM", "생 조개살");
        Add(table, "ITEM_DESC_RAW_CLAM", "신선한 조개살. 익히면 더 맛있을 것 같다");
        Add(table, "ITEM_NAME_RAW_CRUSTACEAN", "생 갑각류살");
        Add(table, "ITEM_DESC_RAW_CRUSTACEAN", "날것의 갑각류살. 구우면 맛있겠다");
        Add(table, "ITEM_NAME_RAW_FISH", "생 생선살");
        Add(table, "ITEM_DESC_RAW_FISH", "싱싱한 생선살. 요리해서 먹자");
        Add(table, "ITEM_NAME_RAW_JELLYFISH", "생 해파리 젤리");
        Add(table, "ITEM_DESC_RAW_JELLYFISH", "가공 전 해파리 젤리. 좀 위험해 보인다");
        Add(table, "ITEM_NAME_RAW_SQUID", "생 오징어살");
        Add(table, "ITEM_DESC_RAW_SQUID", "물컹물컹한 오징어살. 구워 먹으면 최고!");
        Add(table, "ITEM_NAME_STEAMED_CLAM", "찐 조개");
        Add(table, "ITEM_DESC_STEAMED_CLAM", "따끈따끈 갓 찐 조개. 촉수가 절로 간다");

        // ═══ Materials ═══
        Add(table, "ITEM_NAME_FLEXIBLE_BONE", "유연한 뼈");
        Add(table, "ITEM_DESC_FLEXIBLE_BONE", "잘 휘어지는 신기한 뼈. 뭔가 만들 수 있을 것 같다");
        Add(table, "ITEM_NAME_HARD_SCALE", "단단한 비늘");
        Add(table, "ITEM_DESC_HARD_SCALE", "아주 단단한 비늘. 갑옷 재료로 딱이다");
        Add(table, "ITEM_NAME_HARD_SHELL", "단단한 껍데기");
        Add(table, "ITEM_DESC_HARD_SHELL", "두껍고 단단한 껍데기. 방어구에 쓰면 좋겠다");
        Add(table, "ITEM_NAME_INK_SAC", "먹물 주머니");
        Add(table, "ITEM_DESC_INK_SAC", "짙은 먹물이 가득 찬 주머니. 여러모로 쓸모 있다");
        Add(table, "ITEM_NAME_JELLYFISH_TENTACLE", "해파리 촉수");
        Add(table, "ITEM_DESC_JELLYFISH_TENTACLE", "만지면 찌릿찌릿한 해파리 촉수");
        Add(table, "ITEM_NAME_PARALYZING_MUCUS", "마비 점액");
        Add(table, "ITEM_DESC_PARALYZING_MUCUS", "끈적끈적한 점액. 닿으면 몸이 굳는다");
        Add(table, "ITEM_NAME_SEAHORSE_SCALE", "해마 비늘");
        Add(table, "ITEM_DESC_SEAHORSE_SCALE", "작고 반짝이는 해마 비늘. 예쁘다");
        Add(table, "ITEM_NAME_SHARP_TAIL", "날카로운 꼬리");
        Add(table, "ITEM_DESC_SHARP_TAIL", "끝이 뾰족한 꼬리. 무기 재료로 쓸 수 있다");
        Add(table, "ITEM_NAME_SHELL_PIECE", "껍데기 조각");
        Add(table, "ITEM_DESC_SHELL_PIECE", "깨진 껍데기 조각. 모으면 쓸모가 있다");
        Add(table, "ITEM_NAME_SMALL_BONE", "작은 뼈");
        Add(table, "ITEM_DESC_SMALL_BONE", "자그마한 뼈. 간단한 도구를 만들 수 있다");
        Add(table, "ITEM_NAME_SWORDFISH_HORN", "황새치 뿔");
        Add(table, "ITEM_DESC_SWORDFISH_HORN", "길고 날카로운 황새치 뿔. 강력한 무기가 될 것 같다");

        // ═══ Resources ═══
        Add(table, "ITEM_NAME_CORAL", "산호");
        Add(table, "ITEM_DESC_CORAL", "알록달록 예쁜 산호. 단단해서 도구를 만들 수 있다");
        Add(table, "ITEM_NAME_ROCK", "돌");
        Add(table, "ITEM_DESC_ROCK", "흔하디 흔한 돌. 그래도 쓸모는 있다");
        Add(table, "ITEM_NAME_SAND", "모래");
        Add(table, "ITEM_DESC_SAND", "곱고 부드러운 모래. 건축 재료로 쓸 수 있다");
        Add(table, "ITEM_NAME_SEAWEED", "해초");
        Add(table, "ITEM_DESC_SEAWEED", "바닷속에서 흔들흔들 자라는 해초");
        Add(table, "ITEM_NAME_SEAWEED_FIBER", "해초 섬유");
        Add(table, "ITEM_DESC_SEAWEED_FIBER", "해초에서 뽑은 질긴 섬유. 묶거나 엮기 좋다");
        Add(table, "ITEM_NAME_SPONGE", "스펀지");
        Add(table, "ITEM_DESC_SPONGE", "물을 잔뜩 머금은 폭신한 스펀지");

        // ═══ Crafting ═══
        Add(table, "ITEM_NAME_CORAL_PICKAXE", "산호 곡괭이");
        Add(table, "ITEM_DESC_CORAL_PICKAXE", "산호로 만든 곡괭이. 꽤 단단하다");
        Add(table, "ITEM_NAME_CRAFTING_TABLE", "제작대");
        Add(table, "ITEM_DESC_CRAFTING_TABLE", "여러 가지를 만들 수 있는 작업대");
        Add(table, "ITEM_NAME_SHELTER", "쉼터");
        Add(table, "ITEM_DESC_SHELTER", "잠시 쉬어갈 수 있는 아늑한 공간");
        Add(table, "ITEM_NAME_STONE_PICKAXE", "돌 곡괭이");
        Add(table, "ITEM_DESC_STONE_PICKAXE", "돌로 만든 튼튼한 곡괭이");
        Add(table, "ITEM_NAME_STONE_SHOVEL", "돌 삽");
        Add(table, "ITEM_DESC_STONE_SHOVEL", "돌로 만든 삽. 모래를 팔 수 있다");
        Add(table, "ITEM_NAME_SWORD", "검");
        Add(table, "ITEM_DESC_SWORD", "날카롭게 다듬은 검. 든든하다");

        // ═══ Stats ═══
        Add(table, "STAT_ATK", "공격력");
        Add(table, "STAT_DEF", "방어력");
        Add(table, "STAT_EVA", "회피율");
        Add(table, "STAT_CRIT", "치명타율");
        Add(table, "STAT_HP", "체력");

        // ═══ Status Effects ═══
        Add(table, "STATUS_BLEED", "출혈");
        Add(table, "STATUS_STUN", "스턴");
        Add(table, "STATUS_IMPERFECT", "불완전");

        AssetDatabase.CreateAsset(table, "Assets/Data/Localization/LocalizationTable_KO.asset");
        Debug.Log($"[Localization] KO 테이블 생성 완료: {table.entries.Count}개 항목");
    }

    private static void GenerateEnglishTable()
    {
        var table = ScriptableObject.CreateInstance<LocalizationTable>();
        table.language = SystemLanguage.English;
        table.entries = new List<LocalizationEntry>();

        // ═══ Passive Skills ═══
        Add(table, "PASSIVE_NAME_BRAVE", "Bravery");
        Add(table, "PASSIVE_DESC_BRAVE", "ATK increases the higher the enemy's HP (100%→+25%)");
        Add(table, "PASSIVE_NAME_CONFIDENCE", "Confidence");
        Add(table, "PASSIVE_DESC_CONFIDENCE", "ATK increases proportional to your HP (100%→+20%)");
        Add(table, "PASSIVE_NAME_CRUEL", "Cruelty");
        Add(table, "PASSIVE_DESC_CRUEL", "ATK increases the lower the enemy's HP (0%→+25%)");
        Add(table, "PASSIVE_NAME_BASIC", "Fundamentals");
        Add(table, "PASSIVE_DESC_BASIC", "25% chance to strike twice with basic attacks");
        Add(table, "PASSIVE_NAME_STUN", "Tentacle Boost");
        Add(table, "PASSIVE_DESC_STUN", "25% chance to stun with basic attacks");
        Add(table, "PASSIVE_NAME_PREDATOR", "Predation");
        Add(table, "PASSIVE_DESC_PREDATOR", "Execute enemies below 20% HP and heal for 100% of damage dealt");
        Add(table, "PASSIVE_NAME_SENSE", "Weakness Sense");
        Add(table, "PASSIVE_DESC_SENSE", "ATK +6% per debuff on enemy (max 30%)");

        // ═══ Active Skills ═══
        Add(table, "SKILL_NAME_CRIT", "Lethal Tentacle");
        Add(table, "SKILL_DESC_CRIT", "Guaranteed critical hit on first attack against the target");
        Add(table, "SKILL_NAME_RAGE", "Punishing Tentacle");
        Add(table, "SKILL_DESC_RAGE", "ATK x1.75 for 1 turn");
        Add(table, "SKILL_NAME_HARD", "Hardened Tentacle");
        Add(table, "SKILL_DESC_HARD", "Inflicts Imperfect (stacks up to 4)");
        Add(table, "SKILL_NAME_STRETCH", "Stretching Tentacle");
        Add(table, "SKILL_DESC_STRETCH", "Move to a tile within 3 spaces");
        Add(table, "SKILL_NAME_DIG", "Piercing Tentacle");
        Add(table, "SKILL_DESC_DIG", "Inflicts Bleed. If already bleeding, heal for 50% of damage dealt");
        Add(table, "SKILL_NAME_SHARP", "Sharp Tentacle");
        Add(table, "SKILL_DESC_SHARP", "Inflicts Bleed. If already bleeding, deal 40% ATK as bonus damage");

        // ═══ Equipment ═══
        Add(table, "EQUIP_NAME_BONE", "Bone Ornament");
        Add(table, "EQUIP_DESC_BONE", "A little ornament that invites good fortune");
        Add(table, "EQUIP_NAME_HORN", "Horn Charm");
        Add(table, "EQUIP_DESC_HORN", "A charm that seems to make your luck grow");
        Add(table, "EQUIP_NAME_SHELLNECKLACE", "Shell Necklace");
        Add(table, "EQUIP_DESC_SHELLNECKLACE", "A necklace that feels reassuring just by having it");
        Add(table, "EQUIP_NAME_FLEXIBLE", "Flexible Armor");
        Add(table, "EQUIP_DESC_FLEXIBLE", "Armor that flows with the current like part of the sea");
        Add(table, "EQUIP_NAME_SCALE", "Scale Armor");
        Add(table, "EQUIP_DESC_SCALE", "Tough armor crafted from sturdy scales");
        Add(table, "EQUIP_NAME_SHELL", "Shell Armor");
        Add(table, "EQUIP_DESC_SHELL", "Armor that makes you feel really, really safe");
        Add(table, "EQUIP_NAME_BLADED", "Bladed Tentacle");
        Add(table, "EQUIP_DESC_BLADED", "A tentacle that looks really, really sharp");
        Add(table, "EQUIP_NAME_CRUDEKNIFE", "Crude Tentacle Knife");
        Add(table, "EQUIP_DESC_CRUDEKNIFE", "A tentacle knife with a sharp pointy tip");
        Add(table, "EQUIP_NAME_HEAVY", "Heavy Tentacle");
        Add(table, "EQUIP_DESC_HEAVY", "A tentacle that looks incredibly heavy");

        // ═══ Enemies ═══
        Add(table, "ENEMY_NAME_ANCHOVY", "Anchovy");
        Add(table, "ENEMY_NAME_CLAM", "Clam");
        Add(table, "ENEMY_NAME_FLOWERCRAB", "Flower Crab");
        Add(table, "ENEMY_NAME_JELLYFISH", "Jellyfish");
        Add(table, "ENEMY_NAME_LOBSTER", "Lobster");
        Add(table, "ENEMY_NAME_PENSHELL", "Pen Shell");
        Add(table, "ENEMY_NAME_REDSNAPPER", "Red Snapper");
        Add(table, "ENEMY_NAME_SARDINE", "Sardine");
        Add(table, "ENEMY_NAME_SEAHORSE", "Seahorse");
        Add(table, "ENEMY_NAME_SQUID", "Squid");
        Add(table, "ENEMY_NAME_SWORDFISH", "Swordfish");
        Add(table, "ENEMY_NAME_YELLOWRAY", "Yellow Ray");

        // ═══ Consumables ═══
        Add(table, "ITEM_NAME_INK_COAGULANT", "Ink Coagulant");
        Add(table, "ITEM_DESC_INK_COAGULANT", "A remedy that stops bleeding right away when applied");
        Add(table, "ITEM_NAME_PARALYSIS_OINTMENT", "Paralysis Ointment");
        Add(table, "ITEM_DESC_PARALYSIS_OINTMENT", "A soothing ointment that loosens up a stiffened body");
        Add(table, "ITEM_NAME_SPONGE_BANDAGE", "Sponge Bandage");
        Add(table, "ITEM_DESC_SPONGE_BANDAGE", "A soft, squishy bandage that feels nice when wrapped");

        // ═══ Food ═══
        Add(table, "ITEM_NAME_GRILLED_CRUSTACEAN", "Grilled Crustacean");
        Add(table, "ITEM_DESC_GRILLED_CRUSTACEAN", "A nicely grilled crustacean. Very filling!");
        Add(table, "ITEM_NAME_GRILLED_FISH", "Grilled Fish");
        Add(table, "ITEM_DESC_GRILLED_FISH", "A grilled fish with a lovely savory aroma");
        Add(table, "ITEM_NAME_GRILLED_SQUID", "Grilled Squid");
        Add(table, "ITEM_DESC_GRILLED_SQUID", "Chewy grilled squid. Looks delicious!");
        Add(table, "ITEM_NAME_JELLYFISH_JELLY", "Jellyfish Jelly");
        Add(table, "ITEM_DESC_JELLYFISH_JELLY", "A soft, translucent jelly. Slightly tingly");
        Add(table, "ITEM_NAME_RAW_CLAM", "Raw Clam Meat");
        Add(table, "ITEM_DESC_RAW_CLAM", "Fresh clam meat. Probably tastier if cooked");
        Add(table, "ITEM_NAME_RAW_CRUSTACEAN", "Raw Crustacean Meat");
        Add(table, "ITEM_DESC_RAW_CRUSTACEAN", "Raw crustacean meat. Would be great grilled");
        Add(table, "ITEM_NAME_RAW_FISH", "Raw Fish Meat");
        Add(table, "ITEM_DESC_RAW_FISH", "Fresh fish meat. Let's cook it up");
        Add(table, "ITEM_NAME_RAW_JELLYFISH", "Raw Jellyfish Jelly");
        Add(table, "ITEM_DESC_RAW_JELLYFISH", "Unprocessed jellyfish jelly. Looks a bit risky");
        Add(table, "ITEM_NAME_RAW_SQUID", "Raw Squid Meat");
        Add(table, "ITEM_DESC_RAW_SQUID", "Slimy squid meat. Best when grilled!");
        Add(table, "ITEM_NAME_STEAMED_CLAM", "Steamed Clam");
        Add(table, "ITEM_DESC_STEAMED_CLAM", "A freshly steamed clam. Your tentacles reach for it on their own");

        // ═══ Materials ═══
        Add(table, "ITEM_NAME_FLEXIBLE_BONE", "Flexible Bone");
        Add(table, "ITEM_DESC_FLEXIBLE_BONE", "A curious bone that bends easily. Could craft something with this");
        Add(table, "ITEM_NAME_HARD_SCALE", "Hard Scale");
        Add(table, "ITEM_DESC_HARD_SCALE", "A very tough scale. Perfect for armor crafting");
        Add(table, "ITEM_NAME_HARD_SHELL", "Hard Shell");
        Add(table, "ITEM_DESC_HARD_SHELL", "A thick, sturdy shell. Would make great armor material");
        Add(table, "ITEM_NAME_INK_SAC", "Ink Sac");
        Add(table, "ITEM_DESC_INK_SAC", "A pouch full of dark ink. Useful in many ways");
        Add(table, "ITEM_NAME_JELLYFISH_TENTACLE", "Jellyfish Tentacle");
        Add(table, "ITEM_DESC_JELLYFISH_TENTACLE", "A jellyfish tentacle that tingles when you touch it");
        Add(table, "ITEM_NAME_PARALYZING_MUCUS", "Paralyzing Mucus");
        Add(table, "ITEM_DESC_PARALYZING_MUCUS", "Sticky mucus. Your body goes stiff if it touches you");
        Add(table, "ITEM_NAME_SEAHORSE_SCALE", "Seahorse Scale");
        Add(table, "ITEM_DESC_SEAHORSE_SCALE", "A small, shimmering seahorse scale. Pretty");
        Add(table, "ITEM_NAME_SHARP_TAIL", "Sharp Tail");
        Add(table, "ITEM_DESC_SHARP_TAIL", "A pointy-tipped tail. Can be used for weapon crafting");
        Add(table, "ITEM_NAME_SHELL_PIECE", "Shell Piece");
        Add(table, "ITEM_DESC_SHELL_PIECE", "A broken shell fragment. Collect enough and it'll come in handy");
        Add(table, "ITEM_NAME_SMALL_BONE", "Small Bone");
        Add(table, "ITEM_DESC_SMALL_BONE", "A tiny bone. Can be used to make simple tools");
        Add(table, "ITEM_NAME_SWORDFISH_HORN", "Swordfish Horn");
        Add(table, "ITEM_DESC_SWORDFISH_HORN", "A long, sharp swordfish horn. Could become a powerful weapon");

        // ═══ Resources ═══
        Add(table, "ITEM_NAME_CORAL", "Coral");
        Add(table, "ITEM_DESC_CORAL", "Colorful, pretty coral. Hard enough to craft tools with");
        Add(table, "ITEM_NAME_ROCK", "Rock");
        Add(table, "ITEM_DESC_ROCK", "A plain old rock. Still useful, though");
        Add(table, "ITEM_NAME_SAND", "Sand");
        Add(table, "ITEM_DESC_SAND", "Fine, soft sand. Can be used as building material");
        Add(table, "ITEM_NAME_SEAWEED", "Seaweed");
        Add(table, "ITEM_DESC_SEAWEED", "Seaweed that sways gently in the ocean current");
        Add(table, "ITEM_NAME_SEAWEED_FIBER", "Seaweed Fiber");
        Add(table, "ITEM_DESC_SEAWEED_FIBER", "Tough fiber pulled from seaweed. Great for tying and weaving");
        Add(table, "ITEM_NAME_SPONGE", "Sponge");
        Add(table, "ITEM_DESC_SPONGE", "A squishy sponge soaked full of water");

        // ═══ Crafting ═══
        Add(table, "ITEM_NAME_CORAL_PICKAXE", "Coral Pickaxe");
        Add(table, "ITEM_DESC_CORAL_PICKAXE", "A pickaxe made of coral. Surprisingly sturdy");
        Add(table, "ITEM_NAME_CRAFTING_TABLE", "Crafting Table");
        Add(table, "ITEM_DESC_CRAFTING_TABLE", "A workbench where you can craft all sorts of things");
        Add(table, "ITEM_NAME_SHELTER", "Shelter");
        Add(table, "ITEM_DESC_SHELTER", "A cozy spot where you can rest for a while");
        Add(table, "ITEM_NAME_STONE_PICKAXE", "Stone Pickaxe");
        Add(table, "ITEM_DESC_STONE_PICKAXE", "A sturdy pickaxe made of stone");
        Add(table, "ITEM_NAME_STONE_SHOVEL", "Stone Shovel");
        Add(table, "ITEM_DESC_STONE_SHOVEL", "A stone shovel. Can dig through sand");
        Add(table, "ITEM_NAME_SWORD", "Sword");
        Add(table, "ITEM_DESC_SWORD", "A finely sharpened sword. Feels reassuring");

        // ═══ Stats ═══
        Add(table, "STAT_ATK", "ATK");
        Add(table, "STAT_DEF", "DEF");
        Add(table, "STAT_EVA", "EVA");
        Add(table, "STAT_CRIT", "CRIT");
        Add(table, "STAT_HP", "HP");

        // ═══ Status Effects ═══
        Add(table, "STATUS_BLEED", "Bleed");
        Add(table, "STATUS_STUN", "Stun");
        Add(table, "STATUS_IMPERFECT", "Imperfect");

        AssetDatabase.CreateAsset(table, "Assets/Data/Localization/LocalizationTable_EN.asset");
        Debug.Log($"[Localization] EN 테이블 생성 완료: {table.entries.Count}개 항목");
    }

    private static void Add(LocalizationTable table, string key, string value)
    {
        table.entries.Add(new LocalizationEntry { key = key, value = value });
    }
}