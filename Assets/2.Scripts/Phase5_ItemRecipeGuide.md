# Phase 5: 아이템/레시피 등록 가이드

## 아이템 ID 체계 (권장)

| 범위 | 분류 |
|------|------|
| 1-99 | 채집 자원 |
| 100-199 | 몹 드랍 재료 |
| 200-299 | 음식 |
| 300-399 | 회복 아이템 |
| 400-499 | 도구 |
| 500-599 | 무기 |
| 600-699 | 갑옷 |
| 700-799 | 장신구 |

---

## 1. 채집 자원 (6종)

| ID | 이름 | ItemType | 설명 |
|----|------|----------|------|
| 1 | Seaweed (해초) | Resource | 이미 있음 |
| 2 | Coral (산호) | Resource | 이미 있음 |
| 3 | Rock (암석) | Resource | 이미 있음 |
| 4 | Sand (모래) | Resource | 이미 있음 |
| 5 | Sponge (해면) | Resource | **새로 추가** - 암석 옆에서 스폰 |
| 6 | SeaweedFiber (해초섬유) | Material | **새로 추가** - 제작으로 획득 |

---

## 2. 몹 드랍 재료 (12종)

| ID | 이름 | ItemType | 드랍 몹 |
|----|------|----------|---------|
| 101 | SmallCrustaceanMeat (작은 갑각류살) | Material | 꽃게, 바닷가재 |
| 102 | HardShell (단단한 껍질) | Material | 꽃게, 바닷가재 |
| 103 | SquidMeat (오징어살) | Material | 오징어 |
| 104 | InkSac (먹물주머니) | Material | 오징어 |
| 105 | JellyfishTentacle (해파리 촉수) | Material | 해파리 |
| 106 | ParalyzingMucus (마비 점액) | Material | 해파리 |
| 107 | SeahorseScale (해마 비늘) | Material | 해마 |
| 108 | FlexibleBone (유연한 뼈) | Material | 해마 |
| 109 | SharpTail (날카로운 꼬리) | Material | 노랑가오리 |
| 110 | HardScale (단단한 비늘) | Material | 참돔 |
| 111 | SwordfishHorn (청새치 뿔) | Material | 청새치 (보스) |
| 112 | SmallBone (작은 뼈) | Material | 멸치, 정어리 |
| 113 | Shellpiece (조개껍질) | Material | 키조개, 바지락 |

---

## 3. 음식 (5종) - 배고픔 회복 전용

| ID | 이름 | ItemType | 효과 | 재료 |
|----|------|----------|------|------|
| 201 | GrilledCrustacean (갑각류 구이) | Food | 배고픔 +40 | 작은 갑각류살 + 해초 |
| 202 | GrilledFish (생선 구이) | Food | 배고픔 +45 | 큰 물고기살 + 산호 |
| 203 | SteamedClam (조개 찜) | Food | 배고픔 +35 | 조개껍질 + 해초 |
| 204 | GrilledSquid (오징어 구이) | Food | 배고픔 +30 | 오징어살 + 산호 |
| 205 | JellyfishJelly (해파리 젤 식사) | Food | 배고픔 +25 | 해파리 촉수 + 해초 |

---

## 4. 회복 아이템 (3종) - HP 회복 전용

| ID | 이름 | ItemType | 효과 | 재료 |
|----|------|----------|------|------|
| 301 | SpongeBandage (해면 붕대) | Consumable | HP +25 | 해면 + 해초섬유 |
| 302 | ParalysisOintment (마비 점액 연고) | Consumable | HP +40 | 마비 점액 + 해면 + 해초섬유 |
| 303 | InkCoagulant (먹물 응고제) | Consumable | HP +60 | 먹물주머니 + 암석 + 해면 |

---

## 5. 무기 (3종)

| ID | 이름 | ItemType | 효과 | 재료 |
|----|------|----------|------|------|
| 501 | CrudeTentacleKnife (조잡한 촉수 칼) | Weapon | ATK +2 | 산호 + 암석 + 해초 |
| 502 | BladedTentacle (날붙이 촉수) | Weapon | ATK +3 | 날카로운 꼬리 + 암석 + 해초 |
| 503 | HeavyTentacleWeight (무거운 촉수 추) | Weapon | ATK +2, 스턴 +5% | 암석 + 산호 + 단단한 껍질 |

---

## 6. 갑옷 (3종)

| ID | 이름 | ItemType | 효과 | 재료 |
|----|------|----------|------|------|
| 601 | ShellArmor (갑각 보호구) | Armor | 최대HP +10 | 단단한 껍질 + 해초섬유 |
| 602 | ScaleShield (비늘 보호막) | Armor | DEF +1 | 단단한 비늘 + 해초섬유 |
| 603 | FlexibleArmor (연성 보호복) | Armor | EVA +3% | 해마 비늘 + 해초섬유 |

---

## 7. 장신구 (3종)

| ID | 이름 | ItemType | 효과 | 재료 |
|----|------|----------|------|------|
| 701 | BoneOrnament (뼈 장식) | Accessory | CRIT +2% | 작은 뼈 + 산호 |
| 702 | HornAmulet (뿔 부적) | Accessory | CRIT_DMG +10% | 청새치 뿔 + 해초섬유 |
| 703 | ShellNecklace (조개 목걸이) | Accessory | 최대HP +5 | 조개껍질 + 해초 |

---

## 레시피 등록 방법

### Unity 에디터에서:

1. **Project 창** > `Assets/Inventory/Recipe` 폴더로 이동
2. **우클릭** > `Create` > `Crafting` > `Recipe`
3. 새 레시피 설정:
   - `Result Item ID`: 결과물 아이템 ID
   - `Result Count`: 생성 개수
   - `Category`: Food / Medicine / Weapon / Armor / Accessory / Material
   - `Ingredients`: + 버튼으로 재료 추가 (itemID, count)

### 예시: 해초섬유 레시피

```
Result Item ID: 6
Result Count: 1
Category: Material
Ingredients:
  - itemID: 1 (Seaweed), count: 2
```

### 예시: 갑각류 구이 레시피

```
Result Item ID: 201
Result Count: 1
Category: Food
Ingredients:
  - itemID: 101 (SmallCrustaceanMeat), count: 1
  - itemID: 1 (Seaweed), count: 1
```

---

## 몹별 드랍 설정 (EnemyDefinition)

| 몹 | 음식 드랍 | 재료 드랍 | 재료2 드랍 |
|----|----------|----------|-----------|
| 꽃게 | 101 (50%) | 102 (70%) | - |
| 바닷가재 | 101 (50%) | 102 (70%) | - |
| 오징어 | 103 (50%) | 104 (70%) | - |
| 해파리 | - | 105 (70%) | 106 (30%) |
| 해마 | - | 107 (70%) | 108 (30%) |
| 노랑가오리 | 202 (50%) | 109 (80%) | - |
| 참돔 | 202 (50%) | 110 (80%) | - |
| 청새치 | 202 (70%) | 111 (100%) | - |
| 멸치 | - | 112 (50%) | - |
| 정어리 | - | 112 (50%) | - |
| 키조개 | - | 113 (80%) | - |
| 바지락 | - | 113 (60%) | - |

---

## 체크리스트

### ItemData 생성 (Assets > Create > Inventory > Item Data)
- [ ] 해면 (ID: 5)
- [ ] 해초섬유 (ID: 6)
- [ ] 몹 드랍 재료 13종 (ID: 101-113)
- [ ] 음식 5종 (ID: 201-205)
- [ ] 회복 아이템 3종 (ID: 301-303)
- [ ] 무기 3종 (ID: 501-503)
- [ ] 갑옷 3종 (ID: 601-603)
- [ ] 장신구 3종 (ID: 701-703)

### CraftingRecipe 생성 (Assets > Create > Crafting > Recipe)
- [ ] 해초섬유 레시피
- [ ] 음식 5종 레시피
- [ ] 회복 아이템 3종 레시피
- [ ] 무기 3종 레시피
- [ ] 갑옷 3종 레시피
- [ ] 장신구 3종 레시피

### EnemyDefinition 드랍 설정
- [ ] 12종 적 모두 dropFood, dropMaterial 연결
