# PROJECT_CONTEXT.md — ASTRA EDEN

> Living context file for humans and AI coding assistants working on this repository.  
> Keep this file updated whenever the core direction, scene flow, architecture, or implementation status changes.
>
> **Last verified against repo:** 2026-06-16  
> **Sections marked "snapshot"** = đối chiếu trực tiếp với code/manifest; **các section khác** = direction/design, không phải state.

---

## 1. Project Identity

**Project name:** ASTRA EDEN  
**Engine:** Unity 3D (URP)  
**Target platform for current development:** PC / Mac desktop build  
**Main genre:** Third-person 3D anime action adventure + light survival + dinosaur companion RPG  
**Current project goal:** Build a playable vertical slice before the August deadline.

**Key package versions (from `Packages/manifest.json`):**

- `com.unity.cinemachine` — 3.1.7 (đã cài, nhưng hiện chưa script/scene nào dùng)
- `com.unity.inputsystem` — 1.19.0 (project dùng Input System mới, file generated: `Assets/Scripts/GameSystem/InputControls.cs`)
- `com.unity.render-pipelines.universal` — 17.3.0 (URP)

ASTRA EDEN is a stylized third-person action game set on **Eden-7**, a mysterious island where dinosaurs, prehistoric creatures, ancient sci-fi ruins, Core Shards, and crystal corruption coexist.

The player controls a **Seeker** who must survive, explore zones, fight corrupted dinosaurs, gather resources, unlock dinosaur companions, restore ancient terminals, and eventually confront Alpha/Boss creatures.

---

## 2. Core Fantasy

The player should feel like:

- They are stranded on a dangerous prehistoric sci-fi island.
- They are slowly turning hostile wilderness into known territory.
- Their **Core Shard** gives them special power to fight, activate ruins, and connect with dinosaur companions.
- Dinosaur companions are not decorations; they help with combat, traversal, utility, or support.
- Every zone clear should feel like real progression toward escaping or understanding Eden-7.

---

## 3. Current Scope Direction

Do **not** build the full dream version first.

For the current development milestone, prioritize a **strong vertical slice**:

- 1 playable hero initially.
- 1 active companion initially.
- 1 safe camp / hub.
- 1 playable zone.
- 2–3 enemy archetypes.
- 1 mini-boss or alpha boss.
- Basic gather → loot → inventory → upgrade/reward loop.
- Basic shop or basic gacha, not both deeply polished at the same time.
- Save/load stable enough for demo.
- Combat feel, camera, enemy behavior, and UI clarity are more important than adding many characters.

Full-version ideas such as many heroes, multiple biomes, co-op, complex gacha lore, large skin shop, and deep crafting should be treated as later expansion unless specifically planned.

---

## 4. Current Implementation Status

This section reflects the latest known state of the Unity project.

### 4.1. Already Working / Mostly Working

- Player movement is working (`PlayerController.cs`, mới cập nhật để mượt hơn).
- Player combat core (`PlayerCombatController.cs`) + animation event relay (`PlayerAnimationEventRelay.cs`) + animator bridge (`PlayerAnimatorBridge.cs`).
- Player VFX system (`PlayerVFXController.cs`) — đã pass VFX cho skill/đánh; đã fix lỗi "đánh khi chưa tới vùng".
- Skill / weapon / equipment / status effect data đã có dạng ScriptableObject (`Combat/Data/`).
- Character data + runtime: `CharacterData`, `CharacterBaseStats`, `CharacterRuntimeStats`, `CharacterProgressData`, `CharacterEnums`, `SkinData`.
- Character runtime components: `CharacterHealth`, `CharacterKnockback`, `RagdollOnDeath`, `DissolveOnDeath` (dùng chung cho cả player và enemy).
- Main menu (`MainMenuController.cs`) với Start / Continue / Settings / Quit.
- Settings panel (`SettingsPanelController.cs`) — Audio + Controls.
- Scene transition / portal flow: `ScenePortal.cs`, `ScenePortalFade.cs`.
- Loading screen (`LoadingScreenController.cs`).
- Save system: `AstraSaveSystem.cs` + `GameDataManager.cs` (currency, HP/stamina/energy, last scene, scene positions).
- Player position restore + auto-save: `PlayerPositionRestore.cs`, `AutoSavePlayerPosition.cs`.
- Recall portal: `RecallPortal.cs` (Teleport), `RecallPortalManager.cs` + `RecallUIButton.cs` (UI), prefab `Assets/Prefabs/RecallPortal.prefab`.
- Enemy: `EnemyData`, `EnemyPatrol` (chase + patrol), `EnemyAttackHitbox` (active hitbox window), `EnemyAnimationEventRelay`.
- NPC dialog/canvas: `NPCDialogueTrigger.cs` + `BillboardObjectList.cs` (text trên đầu NPC).
- HUD: `HUDTopStatusController.cs`, `CharacterStatsHUD.cs`, `EnemyHUDRange.cs`.
- Editor tools: `EnemyHUDBuilder`, `EnemyTemplateApplier`, `DinoRagdollBuilder`, `EdenTerrainAutoPainter`, `SeekerMaterialConverter`, `GaiaUserDataMapBuilder`.
- Door interactables: `DoubleDoorOpener.cs`, `VerticalDoorOpener.cs`.
- Custom shadow caster cho player: `PlayerShadowCaster.cs`.

### 4.2. Not Finished / Needs Work

- Enemy detection vẫn là radius-based qua `EnemyPatrol`; cần đổi sang **FOV + line-of-sight raycast**.
- Enemy hit/death đã đi vào commit gần đây nhưng còn phải polish (stagger, fallback death cho enemy thiếu clip, knockback tinh chỉnh).
- Loot drop system chưa có (chưa có `LootDropSpawner`/`PickupItem`/`LootTableData`).
- Gather/resource node chưa implement.
- Inventory + currency UI chưa hoàn chỉnh (đã có `Gold` data direction nhưng chưa có inventory service/UI).
- Cooldown remaining number UI chưa hoàn chỉnh.
- Companion system chưa có script nào (chưa implement).
- Shop/gacha — chưa làm, đợi inventory/currency/save ổn định.
- Camera vẫn dùng `CameraController.cs` custom — Cinemachine 3.1.7 đã cài nhưng chưa migrate.

---

## 5. Recommended Next Development Order

The current best priority order is:

1. **Enemy perception upgrade**
   - Replace pure circle detection with FOV + LOS raycast.
   - Enemy should detect player only if inside vision angle/range and not blocked.
   - Keep hearing/proximity as optional later.

2. **Enemy combat correctness**
   - Damage only through active hitbox windows or animation events.
   - Add attack cooldown and recovery.
   - Add clear telegraph before hit.
   - Add chase → attack → recover → chase loop.

3. **Hit feedback and death**
   - Add hurt reaction.
   - Add knockback/stagger where appropriate.
   - Add fallback death animation/ragdoll/disable-agent behavior for enemies without death clips.
   - Add VFX/SFX/camera shake lightly.

4. **Loot drop foundation**
   - Enemy death spawns drops.
   - Basic pickup object.
   - Pickup adds item/currency to inventory or simple test counter.

5. **Gathering system**
   - Resource node with interact key.
   - Gather animation or progress time.
   - Node gives material.
   - Optional node respawn later.

6. **Basic inventory / item data**
   - ItemData ScriptableObject.
   - Inventory stack for materials/currency.
   - Minimal UI list.

7. **Reward loop / result feedback**
   - After enemy/gather/mini-objective, show clear feedback.
   - Add result screen later after zone clear.

8. **Basic shop OR basic gacha**
   - Shop is safer and more useful first.
   - Gacha can be added after inventory/currency is stable.

9. **Companion MVP**
   - Companion follows player.
   - Companion attacks only when commanded.
   - Companion skill later.

10. **Mini-boss / vertical slice polish**
    - Add one larger enemy with clear telegraph.
    - Add boss HP UI.
    - Add reward after defeat.

---

## 6. Core Gameplay Loop

Target loop for the vertical slice:

```text
Main Menu
→ Continue / New Game
→ Beacon Camp / Safe Hub
→ Prepare loadout
→ Enter zone
→ Explore
→ Detect enemy / fight
→ Gather resources
→ Loot drops
→ Complete objective or defeat mini-boss
→ Return to camp
→ Upgrade / shop / save
→ Repeat
```

The game should always push the player through this loop before adding extra features.

---

## 7. Important Scenes

Known or planned scene direction:

- `MainMenu`
- `Loading`
- `World_Eden7`
- `Beacon_Camp`
- Possible later zones:
  - `Zone_BeachCrash`
  - `Zone_PrimevalForest`
  - `Zone_RuinedLab`
  - `Zone_CrystalCore`

Current project already references or has worked with:

- `World_Eden7`
- `Beacon_Camp`
- Loading flow
- Main menu flow

When editing scene transition code, do not break continue/save/position restore behavior.

---

## 8. Key Existing / Known Scripts

Snapshot trạng thái thực tế (cập nhật 2026-06-16). Preserve their responsibilities unless refactoring is explicitly requested. Folder root: `Assets/Scripts/`.

### Scene / Save / Loading (`Gameplay/`, `GameSystem/`, `UI/`)

- `GameDataManager` — persistent data (currency, HP/stamina/energy, last scene, scene positions).
- `AstraSaveSystem` — low-level save/load layer (file IO, profile/slot).
- `ScenePortal` + `ScenePortalFade` — portal/scene transition with fade.
- `LoadingScreenController` — loading progress + load target scene.
- `PlayerPositionRestore` — restore player position after scene load / continue.
- `AutoSavePlayerPosition` — auto save player position theo interval/event.
- `RecallPortal` (`Teleport/`) — runtime portal object.
- `RecallPortalManager` + `RecallUIButton` (`UI/`) — spawn recall portal trước mặt player từ UI button.

### Player / Input / Combat (`Player/`, `Combat/Data/`, `Characters/`)

- `PlayerInputReader` — Unity Input System wrapper (attack, dash, run, jump, skill1, skill2, ...).
- `InputControls` (auto-generated từ `.inputactions`).
- `PlayerController` — movement, jump, dash, sprint.
- `PlayerCombatController` — combo, skill, hitbox window.
- `PlayerAnimatorBridge` — kết nối state máy animator với combat/movement.
- `PlayerAnimationEventRelay` — nhận animation event (hit window open/close, footstep, ...).
- `PlayerVFXController` — spawn/destroy VFX gắn với skill và normal attack.
- `PlayerShadowCaster` (`Rendering/`) — custom shadow caster cho player.
- `CharacterData` — SO: identity, stats, prefab, portrait, skill refs.
- `CharacterBaseStats`, `CharacterRuntimeStats`, `CharacterProgressData`, `CharacterEnums`, `SkinData`.
- `CharacterHealth`, `CharacterKnockback`, `RagdollOnDeath`, `DissolveOnDeath` (`Characters/Runtime/`) — dùng chung cho player + enemy.
- `SkillData`, `WeaponData`, `EquipmentData`, `StatusEffectData`, `CombatEnums` (`Combat/Data/`).

### Enemy (`Enemy/`)

- `EnemyData` — SO enemy definition.
- `EnemyPatrol` — patrol + chase player (hiện đang radius detection, cần đổi FOV).
- `EnemyAttackHitbox` — active hitbox window (damage chỉ khi window mở).
- `EnemyAnimationEventRelay` — animation events cho attack window và death.

### UI (`UI/`)

- `MainMenuController`, `SettingsPanelController`, `LoadingScreenController`.
- `HUDTopStatusController`, `CharacterStatsHUD`, `EnemyHUDRange`.
- `BillboardObjectList` (text trên đầu NPC), `NPCDialogueTrigger` (`Gameplay/`).
- `CursorManager`.

### Interactables (`Gameplay/`)

- `DoubleDoorOpener`, `VerticalDoorOpener`.
- `Gameplay/Props/AttachPropToRightHand`, `RotateAroundX`.

### Editor tools (`Editor/`)

- `DinoRagdollBuilder` — build ragdoll cho dinosaur.
- `EnemyHUDBuilder`, `EnemyTemplateApplier` — setup enemy.
- `EdenTerrainAutoPainter`, `GaiaUserDataMapBuilder` — terrain tooling.
- `SeekerMaterialConverter` — convert material cho character Seeker.

### Camera (`Camera/`)

- `CameraController.cs` — script camera duy nhất hiện tại, 4 mode (Exploration, Combat, LockOn, Boss).
- **Cinemachine 3.1.7 đã cài trong packages nhưng chưa scene/script nào sử dụng** — đã verify bằng grep `CinemachineBrain`/`CinemachineCamera` trên tất cả `.unity` files = 0 hit.
- Camera target nên ổn định, không tự xoay trừ khi right-mouse hold hoặc lock-on.
- Khi user nói "camera" / "Cinemachine" → confirm trước có migrate hay không, không tự ý đụng `CameraController.cs` legacy.

---

## 9. Input Direction

Default keyboard/mouse direction:

| Action | Suggested Input |
|---|---|
| Move | WASD |
| Jump | Space |
| Sprint | Left Shift |
| Dodge | Left Ctrl / Alt |
| Basic Attack | J or Left Mouse |
| Heavy Attack | K or Right Mouse |
| Skill 1 | Q |
| Skill 2 | E |
| Ultimate | R |
| Interact / Gather | F |
| Lock-on | Tab |
| Companion command | T |
| Companion skill | G |
| Inventory | B |
| Character | C |
| Companion | V |
| Map | M |
| Pause | Esc |

Important input rule:

- UI click should not accidentally trigger attack.
- Keep input reading separate from action execution.
- Do not directly put too much gameplay logic into UI buttons.

---

## 10. Combat Design Rules

Combat should feel:

- Fast but readable.
- Responsive, not spammy.
- Clear on hit timing.
- Fair: damage should match the visible attack.
- Strong hit feedback: VFX, SFX, hit stop, stagger, or animation reaction.

### Player Combat MVP

- Basic combo: 3–4 hits.
- Heavy attack.
- Skill 1.
- Skill 2.
- Ultimate later if needed.
- Dodge with stamina cost.
- Optional lock-on after core combat is stable.

### Damage Rules

- Do not apply melee damage only because the enemy entered attack state.
- Use hitboxes, animation events, or active attack windows.
- Separate `Hitbox` from `Hurtbox` / `DamageReceiver`.
- Damage calculation should eventually live in a small service/class, not be duplicated everywhere.

### Hit Feedback Rules

When an enemy is hit, use at least some of these:

- Damage number or HP bar change.
- Hit VFX.
- Hit SFX.
- Short hit stop.
- Small knockback.
- Hurt animation if available.
- Stagger for heavy/poise-breaking attacks.
- Flash material or outline briefly.

---

## 11. Enemy AI Direction

Current enemy behavior is too simple if it only detects the player by a circular range.

Target enemy AI should use FSM:

```text
Idle
→ Patrol
→ Detect
→ Chase
→ Attack
→ Recover
→ Chase / ReturnToOrigin
→ Hurt / Stagger
→ Dead
```

### Enemy Perception MVP

Use:

- Vision range.
- Vision angle.
- Raycast line-of-sight from head/sensor to player torso.
- Optional aggro memory so enemy does not forget instantly.
- Return-to-origin after losing player for several seconds.

### Suggested Values

| Setting | Suggested Range |
|---|---:|
| Sight Range | 12–20 m |
| Sight Angle | 90–120 degrees total cone |
| Attack Range | Depends on enemy size |
| Aggro Keep Range | 20–30 m |
| Lose Target Time | 5–8 s |
| Pack Alert Radius | 8–12 m |

### Enemy Archetypes for MVP

Build only a few first:

1. **Melee raptor**
   - Chase, bite/claw, low poise.
2. **Ranged spitter**
   - Keep distance, spit projectile/acid, weak if rushed.
3. **Tanker herbivore / armored enemy**
   - Slow, high HP/poise, big telegraph.

Bosses should not be built before basic enemy combat feels correct.

---

## 12. Companion Direction

Companion is a core pillar, but should be implemented after player/enemy combat is stable.

### Companion MVP

Start with one companion only:

- Follows player using NavMeshAgent.
- Teleports back if too far/stuck.
- Does not block player movement too much.
- Has idle/follow state.
- Can attack current target when player presses command key.
- Has cooldown for command attack.
- Does not play the game automatically for the player.

### Companion Roles Later

- Damage companion: attacks enemy.
- Support companion: heal/buff/detect resources.
- Mount companion: traversal.
- Utility companion: break obstacle / activate object.

Do not implement all roles at once.

---

## 13. Character / Roster Direction

The full design includes multiple character classes:

- Sword Fighter
- Lancer
- Mage
- Gunner
- Archer
- Support

The default playable character is the **Seeker Prototype**, with male/female variants possible. For the current demo, one playable version is enough.

Other planned characters include:

- Auren Vale — sword fighter / balanced hero.
- Kaia Thorn — lancer / anti-large target.
- Selis Arca — mage / crystal-tech AOE.
- Rex Calder — gunner / burst and trap.
- Mira Solen — archer / mobile ranged hunter.
- Yuna Eir — support / heal and buff.
- Darius Flint — heavy blade / guardian style.

For the current build, do not spend too much time implementing all characters. Use ScriptableObject data so more characters can be added later.

---

## 14. Item / Loot / Inventory Direction

### MVP Item Types

Start simple:

- Currency: `Gold`
- Material: `Wood`, `Fiber`, `Crystal`, `Ore`, `Claw`, `Hide`, etc.
- Upgrade material.
- Boss drop later.
- Gacha ticket later.

### Basic Flow

```text
Enemy dies / resource node gathered
→ LootDropSpawner creates pickup
→ Player touches or interacts
→ InventoryService adds item stack
→ UI updates
→ SaveManager can capture inventory state
```

### Important Rule

Inventory must exist before shop/gacha becomes serious.

Shop and gacha both depend on:

- Currency.
- Item definitions.
- Inventory add/remove.
- Save/load.

---

## 15. Shop / Gacha Direction

### Shop First

Build a basic shop before gacha if time is limited.

Shop MVP:

- NPC or terminal opens shop UI.
- List of items.
- Price in Gold.
- Buy item.
- Inventory updates.
- Currency decreases.
- Save after purchase.

### Gacha Later

Gacha can be added once inventory/currency/save are stable.

Gacha MVP:

- Single banner.
- 1-roll button.
- Consumes ticket or currency.
- Gives random item/character placeholder.
- Shows result card.
- Saves history/state.

Avoid complex pity/duplicate systems until the simple version works.

---

## 16. Save / Load Rules

Save/load is essential for this project.

Current known save direction includes:

- Currency.
- Player HP/stamina/energy.
- Last scene.
- Scene positions dictionary.
- Continue button behavior.

Recommended save data later:

- Profile id / save slot.
- Last scene.
- Player position per scene.
- Character unlock/progress.
- Companion unlock/progress.
- Inventory.
- Currency.
- Quest progress.
- Settings.

Important:

- Save on important scene transition.
- Save after shop/gacha/upgrade.
- Save after stage clear or boss defeat.
- Load backup if main file is corrupted later.
- Do not break continue behavior when changing scene flow.

---

## 17. UI / UX Direction

### Core UI Screens

- Main Menu.
- Loading Screen.
- Battle HUD.
- Pause Menu.
- Settings.
- Inventory.
- Shop.
- Gacha later.
- Result screen later.
- Companion screen later.

### Battle HUD MVP

Must show:

- HP.
- Stamina.
- Energy.
- Skill icons.
- Cooldown state.
- Optional cooldown remaining number.
- Enemy/boss HP when needed.

UI should be clear before it is fancy.

---

## 18. Camera Direction

**Trạng thái hiện tại (2026-06-16):** vẫn dùng `Assets/Scripts/Camera/CameraController.cs` custom với 4 mode (Exploration, Combat, LockOn, Boss). Cinemachine 3.1.7 đã được thêm vào `Packages/manifest.json` nhưng **chưa scene nào có CinemachineBrain** và **chưa script nào import `Cinemachine`**.

Direction (future): migrate sang **Cinemachine** khi quyết định.

Target style: third-person action camera kiểu Genshin-like exploration/combat, scope đơn giản.

### Camera Requirements

- Follow player smoothly.
- Keep up with dodge/sprint speed.
- Right mouse hold rotate camera.
- Camera không tự xoay khi không có input.
- Combat camera phải giữ player và target dễ đọc.
- Lock-on thêm sau khi base camera ổn định.

### Suggested Cinemachine Setup (khi migrate)

- Main Camera với Cinemachine Brain.
- `VCam_Explore`.
- `VCam_Combat` later.
- `VCam_LockOn` later.
- `VCam_Boss` later.

Không overbuild camera modes trước khi player/enemy combat ổn định. Trước khi đụng camera, hỏi user: giữ `CameraController` legacy hay migrate sang Cinemachine.

---

## 19. Art / Style Direction

Visual style:

- Anime stylized.
- Prehistoric sci-fi.
- Crystal / core energy accents.
- Clear silhouettes.
- Readable VFX, not too noisy.

Important motifs:

- Core Shard.
- Cyan/teal energy.
- Crystal corruption.
- Ancient lab/ruin technology.
- Dinosaur companions and corrupted dinosaurs.

---

## 20. Architecture Direction

### Actual Folder Structure (snapshot 2026-06-16)

```text
Assets/
  Scenes/                  -> MainMenu.unity, Loading.unity, World_Eden7.unity, Beacon_Camp.unity
  Scripts/
    Camera/                -> CameraController.cs
    Characters/
      Data/                -> CharacterData, CharacterBaseStats, CharacterRuntimeStats,
                              CharacterProgressData, CharacterEnums, SkinData
      Runtime/             -> CharacterHealth, CharacterKnockback,
                              RagdollOnDeath, DissolveOnDeath
    Combat/
      Data/                -> SkillData, WeaponData, EquipmentData,
                              StatusEffectData, CombatEnums
    Data/                  -> SO_Skill_Normal.asset
    Editor/                -> DinoRagdollBuilder, EnemyHUDBuilder, EnemyTemplateApplier,
                              EdenTerrainAutoPainter, GaiaUserDataMapBuilder,
                              SeekerMaterialConverter
    Enemy/                 -> EnemyData, EnemyPatrol, EnemyAttackHitbox,
                              EnemyAnimationEventRelay
    GameSystem/            -> AstraSaveSystem, InputControls
    Gameplay/              -> GameDataManager, ScenePortal, ScenePortalFade,
                              PlayerPositionRestore, AutoSavePlayerPosition,
                              NPCDialogueTrigger, DoubleDoorOpener, VerticalDoorOpener
      Props/               -> AttachPropToRightHand, RotateAroundX
    Player/                -> PlayerController, PlayerCombatController,
                              PlayerInputReader, PlayerAnimatorBridge,
                              PlayerAnimationEventRelay, PlayerVFXController
    Rendering/             -> PlayerShadowCaster
    Teleport/              -> RecallPortal
    UI/                    -> MainMenuController, SettingsPanelController,
                              LoadingScreenController, HUDTopStatusController,
                              CharacterStatsHUD, EnemyHUDRange,
                              RecallPortalManager, RecallUIButton,
                              BillboardObjectList, CursorManager
  Prefabs/                 -> RecallPortal.prefab, Eden Warden Outpost.prefab,
                              Water 1.prefab, Enemy/, Environment/, Vroids/,
                              ATYLIZED RUINS/, Animation/
  Animations/, Materials/, Shaders/, Textures/, Fonts/, Sounds/, VFX/,
  Resources/, Settings/, Terrain/,
  _Project/                -> Generated/, Materials/, ScriptableObjects/
  md/                      -> PROJECT_CONTEXT.md (file này)
  Artsystack - Fantasy RPG GUI/, GentlelandSettings/, TextMesh Pro/,
  TutorialInfo/, _TerrainAutoUpgrade/, Packages/
```

Tổng số script C# trong `Assets/Scripts/`: ~56 file (2026-06-16).

> Folder structure cũ đã được đề xuất (`Assets/_Project/Scripts/...`) **không phải** structure thực tế. Repo hiện dùng `Assets/Scripts/<feature>` ở root. Không migrate trừ khi user yêu cầu rõ — Unity GUID references rất dễ vỡ.

### Architecture Principles

- Prefer small components with clear responsibility.
- Use ScriptableObject for data definitions.
- Keep runtime state separate from static data.
- Keep UI rendering separate from gameplay logic.
- Avoid putting all logic in one giant manager.
- Avoid too many singletons.
- Use serialized fields for Unity references.
- Validate null references with helpful errors.
- Use events/callbacks for UI updates where possible.

### Acceptable Singleton-like Managers

- `GameDataManager` / `SaveManager`.
- `SceneFlowManager` if used.
- `AudioManager`.
- `UIManager` only if it manages screen stack cleanly.

Do not create a new global manager for every small feature.

---

## 21. ScriptableObject Data Direction

Important data assets:

### `CharacterData`

Should include:

- Character id.
- Display name.
- Class.
- Rarity.
- Prefab.
- Portrait/icon.
- Base stats.
- Normal attack.
- Heavy attack.
- Skill 1.
- Skill 2.
- Ultimate.
- Animator/controller references if needed.

### `SkillData`

Should include:

- Skill id.
- Skill name.
- Skill type.
- Cooldown.
- Stamina cost.
- Energy cost/gain.
- Damage multiplier.
- VFX prefab.
- Animation trigger/name.
- Hitbox config.
- Status effect optional.

### `EnemyData`

Should include:

- Enemy id.
- Archetype.
- Stats.
- Move speed.
- Sight range/angle.
- Attack range.
- Prefab.
- Attack list.
- Loot table reference.

### `ItemData`

Should include:

- Item id.
- Display name.
- Type.
- Rarity.
- Stackable flag.
- Max stack.
- Icon.
- Description.

### `LootTableData`

Should include:

- Entries.
- Drop weights.
- Quantity range.
- Guaranteed drops optional.

---

## 22. Coding Rules for AI Assistants

When an AI coding assistant works on this repo, follow these rules:

1. **Do not rewrite the whole project.**
   - Make targeted changes.
   - Preserve existing public fields and serialized references when possible.

2. **Do not invent missing scripts without checking existing names first.**
   - If a script already exists, extend it carefully.
   - If a new script is needed, explain where to attach it.

3. **Do not break Inspector assignments.**
   - Renaming serialized fields can break Unity references.
   - Use `[FormerlySerializedAs]` when renaming important serialized fields.

4. **Prefer clear Unity setup instructions.**
   - Tell which GameObject gets which component.
   - Tell which fields to drag into Inspector.
   - Tell which animation events or colliders are needed.

5. **Keep scope small per patch.**
   - One feature or one bug fix at a time.
   - Avoid mixing camera, combat, save, and UI changes in one patch unless necessary.

6. **Use defensive checks.**
   - Null checks.
   - Helpful `Debug.LogWarning` or `Debug.LogError`.
   - Avoid silent failure.

7. **Use Unity Input System correctly.**
   - Do not mix old `Input.GetKey` heavily unless the existing project already does so intentionally.
   - Avoid UI click triggering gameplay attack.

8. **Use NavMesh correctly for enemies/companions.**
   - Check agent enabled/on NavMesh.
   - Avoid setting destination every frame if not needed.
   - Handle stuck cases.

9. **Combat damage should be event/window based.**
   - Never apply enemy melee damage just because the enemy is close unless it is an intentional aura.

10. **Before adding shop/gacha, make sure inventory/currency/save are stable.**

---

## 23. Known Technical Risks

- Camera lag or wrong rotation during dodge/sprint.
- Enemy attacks damaging player even after player moved away.
- Enemy models without death animations.
- Unity Input System errors if old/new input APIs are mixed incorrectly.
- Scaled parent objects causing VFX/trail warnings.
- Scene transition breaking player restore position.
- Continue/load behavior not restoring correct scene/position.
- Shop/gacha added too early before inventory/save causes messy architecture.
- Too many systems built before core loop is playable.

---

## 24. Definition of Done for Current Vertical Slice

The demo is considered strong enough when:

- Player can move, sprint, dodge, attack, and use at least 2 skills.
- Camera feels stable during normal movement and dodge.
- At least 2 enemy types can detect, chase, attack, take damage, and die.
- Enemy damage is fair and tied to visible attack hit windows.
- Enemy drops loot.
- Player can pick up loot.
- Inventory or at least material/currency count updates.
- Player can gather from at least 1 resource node.
- There is one simple objective or mini-boss.
- There is a camp/hub or return point.
- Save/load/continue works reliably enough for testing.
- UI shows HP/stamina/energy and skill cooldown state.
- No critical input-lock or scene-transition bug remains.

---

## 25. What to Cut If Deadline Is Close

Cut these first:

1. Real co-op.
2. Many playable heroes.
3. Many companions.
4. Deep gacha pity/duplicate systems.
5. Large skin shop.
6. Complex crafting.
7. Ride/mount traversal.
8. Multiple biomes.
9. Final boss.

Do **not** cut these:

- Stable player controller.
- Stable camera.
- Core combat feel.
- Enemy AI basics.
- Hit feedback.
- Loot/gather loop.
- Save/load.
- Basic HUD.

---

## 26. Current Best Roadmap Until Deadline

### Phase A — Fix Combat Foundation

- Enemy FOV + LOS.
- Enemy hitbox timing.
- Enemy hurt/death.
- Player hit feedback.
- Cooldown remaining UI if quick.

### Phase B — Build Exploration Reward Loop

- Loot drop.
- Pickup.
- Basic inventory/currency.
- Gather node.
- Save inventory/currency.

### Phase C — Add Meta Loop

- Basic shop.
- Basic upgrade or simple material usage.
- Camp interaction polish.

### Phase D — Add Companion MVP

- Follow.
- Command attack.
- Cooldown.
- Companion HUD small indicator.

### Phase E — Vertical Slice Polish

- One mini-boss.
- Boss HP UI.
- Result/reward screen.
- VFX/SFX pass.
- Bug fixing.
- Build demo.

---

## 27. One-Sentence Project Direction

ASTRA EDEN should first become a polished small third-person dinosaur action vertical slice with fair combat, useful companion foundation, gather/loot progression, and stable scene/save flow — then expand into gacha, more heroes, more companions, and bigger zones.

---

## 28. Related Design Documents

Keep these design files near the repo or documentation folder if possible:

- `Kịch bản Game.docx`
- `Kịch bản Game (1).docx`
- `astra_eden_master_task_breakdown_detailed.xlsx`

These contain larger design details such as roster, companion ideas, enemy/boss list, technical architecture, and production task breakdown. This `PROJECT_CONTEXT.md` is the condensed working context for daily development and AI assistant prompts.
