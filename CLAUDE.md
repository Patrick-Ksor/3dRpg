# CLAUDE.md

Unity 6 (URP 17, Input System, UI Toolkit + uGUI hybrid) 3D turn-based roguelike RPG. Game code lives in `Assets/Scripts`; the rules reference is `Assets/Resources/RunMechanics.md` (keep it in sync with code changes).

## Scenes and flow
- `Assets/Scenes/MainMenu.unity`: `UI/MainMenuManager` loads `CharacterDefinitionSO` from `Resources/Characters`, stores the chosen prefab in `RunManager`, loads `BattleScene`.
- `Assets/Scenes/BattleScene.unity`: `GameBootstrapper` (a `[RuntimeInitializeOnLoadMethod]` static class) builds the camera, light, floor, BGM, EventSystem and the manager objects at runtime, then calls `uiManager.Initialize` and `battleManager.Initialize`. Enemy pools and VFX/aura prefabs are serialized on `BaseBattleManager` in this scene.
- Press `R` in battle to reload the scene (dev shortcut).

## Key scripts
- `RunManager`: `DontDestroyOnLoad` singleton. Run state (encounter, gold, upgrades, relics), enemy scaling formulas, room generation, reward pool and merchant pricing (`GenerateRewardChoices`, `GenerateMerchantChoices`), `RunStatistics`.
- `BaseBattleManager`: battle state machine, turn queue by speed, player/enemy turns, damage formula, enemy spawning (tiers, elites, affixes, mini-boss, boss), event rooms, summon minions with pooling, evasion, end-of-battle flow.
- `Unit`: stats, status effects, attack animation coroutines (melee lunge, projectiles, instant VFX), death.
- `BattleUIManager`: UI Toolkit HUD from `Resources/UI/GameUi.uxml/.uss` (falls back to a code-built tree), command menu, intermission/reward cards, room choices, run summary. `FloatingHealthBar` (world-space bars, affix/status icons), `DamagePopSpawner`, `TraitAuraController`.
- ScriptableObjects: `AttackAction` (`Assets/Attacks`), `LegendaryRelicSO` (`Resources/Relics`), `CharacterDefinitionSO` (`Resources/Characters`), `BattleItem`, `StatusIconConfig`. `AttackUpgrade`/`RewardChoice` are plain classes.
- Editor tools: `Assets/Editor/RelicAssetGenerator.cs`, `Assets/Scripts/Editor/BattleEnvironmentGenerator.cs`.

## Conventions and gotchas
- Units are re-instantiated every encounter; upgrades are re-applied from `RunManager` lists. Anything stored only on a `Unit` is lost between encounters.
- Attack enhancements work by cloning the `AttackAction` asset (`Instantiate`) and replacing the reference on the unit; attacks are matched by `actionName`, so names must be unique per hero.
- A hero's `Unit.unitName` is set from the prefab name and is the key for `LegendaryRelicSO.targetCharacterName`.
- Third-party packs under `Assets/` (SlimUI, Vefects, Hovl Studio, Violet Theme Ui, etc.) and `Assets/_Recovery` are not game code.
- Known balance/code issues are listed at the bottom of `RunMechanics.md`.
