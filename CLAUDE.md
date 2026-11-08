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

## Battle UI
- Layout is `Resources/UI/GameUi.uxml`; all styling is `GameUi.uss` (dungeon palette = variables in `:root` at the top; edit colours there). There is no code-built fallback tree any more, so a broken UXML means an empty HUD (an error is logged).
- Icons are applied from USS (`.icon-*` classes with `url("/Assets/...")`) so they work in builds; do not load icons with `AssetDatabase`.
- `BattleUIManager` public API is called by `BaseBattleManager`. It also owns the turn-order bar (`UpdateTurnOrder`, called from `ProcessNextTurn`), eased HP/MP bars, in-place stat rows, tooltips (`RegisterTooltip`), battle log, and overlay fade/stagger animations.
- `UI/RewardCardBuilder` builds every reward, merchant and room card; add new card variants there. Merchant cards (price > 0) show the price, the missing gold, and need a second click ("CONFIRM?") to buy.
- The run summary is a stat grid (`summary-grid`) plus the relics/upgrades collected (`summary-relics`), filled in `ShowRunSummary`.
- World-space health bars and damage pops (`FloatingHealthBar`, uGUI) use a shared canvas at sorting order -10, i.e. under the UI Toolkit panel, so overlays cover them.
- Panel scale mode is `ConstantPhysicalSize` (see `Assets/UI Toolkit/PanelSettings.asset`); px sizes in the USS scale with screen DPI.
- Testing in the Editor via Unity-MCP: the player loop does not tick while the Editor window is unfocused, so set `Application.runInBackground = true` first; `screenshot-game-view` then shows the real HUD. Load `BattleScene` from a running game with `SceneManager.LoadScene` (the `scene-open` tool rejected the path).

## Main menu
- `Assets/UI Toolkit/MainMenu/MainMenu.uxml` + `MainMenuStyles.uss` (same palette variables as `GameUi.uss`; keep them in sync). `UI/MainMenuManager` drives it: hero carousel with dots, stat bars and skill list read from the hero prefab's `Unit` component (so changing a prefab's stats or attacks updates the menu), Start and Quit.
- Bar scales (the value that fills a bar) are in `MainMenuManager.StatDefs`; raise them if a hero's stats can exceed them.
- Editing the UXML/USS while in play mode rebuilds the tree and orphans the script's element references; stop and restart play after style edits.

## Battle arena
- Built at runtime by `Arena/ArenaBuilder` (called from `GameBootstrapper`), driven by the `ArenaThemeSO` asset at `Resources/Arenas/DungeonArena.asset`: tiled floor, walls on the +X and +Z sides (the camera looks toward +X/+Z, so the camera side is left open), columns, torches with real flickering point lights, and props scattered outside the fighting area. Prefab sizes/pivots are measured from renderer bounds at runtime, so no dimensions are hard-coded.
- `Arena/ArenaController` re-tints torches, fog and sun per encounter (tier 1 warm, tier 2 ember, tier 3 violet, mini-boss fiery, boss red); `BaseBattleManager.SetupBattle` calls `ArenaController.Instance.Apply(...)`.
- If the theme is missing or invalid the bootstrapper falls back to a plain floor plane.
- With `disableLegacySceneDecor` on, the hand-placed `Dungeon` root and the scene's `Directional Light` in `BattleScene` are deactivated at runtime (not deleted). If walls face the wrong way, adjust `wallYawOffset` on the theme.
- `Editor/BattleEnvironmentGenerator` (village scatter) is legacy and points at `Assets/OccaSoftware`, which is git-ignored.

## Conventions and gotchas
- Units are re-instantiated every encounter; upgrades are re-applied from `RunManager` lists. Anything stored only on a `Unit` is lost between encounters.
- Attack enhancements work by cloning the `AttackAction` asset (`Instantiate`) and replacing the reference on the unit; attacks are matched by `actionName`, so names must be unique per hero.
- A hero's `Unit.unitName` is set from the prefab name and is the key for `LegendaryRelicSO.targetCharacterName`.
- Third-party packs under `Assets/` (SlimUI, Vefects, Hovl Studio, Violet Theme Ui, etc.) and `Assets/_Recovery` are not game code.
- Known balance/code issues are listed at the bottom of `RunMechanics.md`.
