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
- `BattleUIManager`: UI Toolkit HUD from `Resources/UI/GameUi.uxml/.uss`, command menu, intermission/reward cards, room choices, run summary. `FloatingHealthBar` (world-space bars, affix/status icons), `DamagePopSpawner`, `TraitAuraController`.
- ScriptableObjects: `AttackAction` (`Assets/Attacks`), `LegendaryRelicSO` (`Resources/Relics`), `CharacterDefinitionSO` (`Resources/Characters`), `BattleItem`, `StatusIconConfig`. `AttackUpgrade`/`RewardChoice` are plain classes.
- Editor tools: `Assets/Editor/RelicAssetGenerator.cs`, `Assets/Scripts/Editor/BattleEnvironmentGenerator.cs`.

## Battle UI
- Design mockups the UI follows are in `Docs/ui-mockups/` (they include features the game lacks: XP, inventory, hotkeys; those were intentionally left out).
- Layout is `Resources/UI/GameUi.uxml`; all styling is `GameUi.uss` (dungeon palette = variables in `:root` at the top; edit colours there). There is no code-built fallback tree any more, so a broken UXML means an empty HUD (an error is logged).
- Icons are applied from USS (`.icon-*` classes with `url("/Assets/...")`) so they work in builds; do not load icons with `AssetDatabase`.
- `BattleUIManager` public API is called by `BaseBattleManager`. It also owns the turn-order bar (`UpdateTurnOrder`, called from `ProcessNextTurn`), eased HP/MP bars, in-place stat rows, tooltips (`RegisterTooltip`), battle log, and overlay fade/stagger animations.
- `UI/RewardCardBuilder` builds every reward, merchant and room card; add new card variants there. Merchant cards (price > 0) show the price, the missing gold, and need a second click ("CONFIRM?") to buy.
- Loot screen (`ShowIntermission`) is pick-then-confirm: click a card (`reward-card--selected`), then the Take button; the footer also shows gold and foes defeated. After a win the combat-log header shows a gold CLAIM SPOILS button (`ShowClaimButton`); clicking it opens the loot screen. Taking a reward shows a claimed state with CONTINUE, which then applies it and opens Choose Your Path (progress track, pick-then-ENTER cards with Reward/Danger rows). Rarity words are derived from the reward type. `BaseBattleManager.BattleRounds` counts rounds.
- The tooltip is a fixed panel left of the command panel (`tooltip--fixed`) for command buttons; other anchors still place it next to the element.
- `UI/SceneFader` (DontDestroyOnLoad, created on demand) fades to black around scene loads; use `SceneFader.Load(name)` for player-facing transitions (the `R` dev reload still uses `SceneManager.LoadScene`).
- Reward cards use a sprite from `Resources/Rewards/<Name>` when one exists, otherwise a generic pictogram class (`RewardCardBuilder.GetFallbackIconClass`, icons defined in `GameUi.uss`). Full-screen overlays hide the HUD and the nameplate canvas (`overlay-open` class, `FloatingHealthBar.SetCanvasVisible`).
- Damage pops (`DamagePopSpawner`) are TMP labels on the shared canvas with an outline, a punch-in scale and an eased rise.
- The run summary is a stat grid (`summary-grid`) plus the relics/upgrades collected (`summary-relics`), filled in `ShowRunSummary`.
- Nameplates are UI Toolkit elements (`UI/Nameplates.cs`, layer `nameplate-layer`, `np-*` classes); `FloatingHealthBar` just feeds each unit's world position to its plate. Clicking an enemy plate chooses the attack target (`BattleUIManager.OnPlateClicked`; Attack/Special then hit that target, AoE hits all). With several enemies and no plate clicked yet this turn, a single-target skill waits: a target list (name + HP, hover highlights the enemy) appears under Commands, enemy plates pulse, and clicking a list row or a plate fires it. Command buttons are ignored unless it is the player's turn. Nameplates are spread apart by `Nameplates.SeparatePlates` so close enemies do not overlap. Damage pops stay uGUI on the shared canvas at sorting order -10 (under the panel).
- Look: warm gold on dark brown (palette variables at the top of `GameUi.uss`), flat 2px `--line` borders. Fonts live in `Assets/Fonts` (Cinzel for headings, Barlow Condensed for body; both OFL). In USS use `-unity-font-definition: url(...)`: `-unity-font` is overridden by the default theme and silently does nothing. Fonts added after a USS was imported need a reimport of that USS.
- HUD layout: turn order top centre, run info top left, hero card bottom left, combat log bottom centre, command panel bottom right (Attack/Special/Item/Defend rows with sub-labels filled in `ShowCommandMenu`). World-space nameplates (name above the HP bar) are built in `FloatingHealthBar.Init`.
- Panel scale mode is `ScaleWithScreenSize`, reference 1280x720, match 0.5 (`Assets/UI Toolkit/PanelSettings.asset`), so USS px values equal the px in the Docs/ui-mockups frames (the mock notes say 1920x1080; 1280x720 gives the same layout).
- Testing in the Editor via Unity-MCP: the player loop does not tick while the Editor window is unfocused, so set `Application.runInBackground = true` first; `screenshot-game-view` then shows the real HUD. Load `BattleScene` from a running game with `SceneManager.LoadScene` (the `scene-open` tool rejected the path).

## Main menu
- `Assets/UI Toolkit/MainMenu/MainMenu.uxml` + `MainMenuStyles.uss` (same palette variables as `GameUi.uss`; keep them in sync). `UI/MainMenuManager` drives it: hero list with role labels (left; `CharacterDefinitionSO.role`), big portrait ring and description (centre), coloured stat bars and the starting skill (right) read from the hero prefab's `Unit` component (so changing a prefab's stats or attacks updates the menu), Start and Quit.
- Bar scales (the value that fills a bar) are in `MainMenuManager.StatDefs`; raise them if a hero's stats can exceed them.
- Editing the UXML/USS while in play mode rebuilds the tree and orphans the script's element references; stop and restart play after style edits.

## Battle arena
- Built at runtime by `Arena/ArenaBuilder` (called from `GameBootstrapper`), driven by the `ArenaThemeSO` asset at `Resources/Arenas/DungeonArena.asset`: tiled floor, walls on the +X and +Z sides (the camera looks toward +X/+Z, so the camera side is left open), columns, torches with real flickering point lights, and props scattered outside the fighting area. Prefab sizes/pivots are measured from renderer bounds at runtime, so no dimensions are hard-coded.
- `Arena/ArenaController` re-tints torches, fog and sun per encounter (tier 1 warm, tier 2 ember, tier 3 violet, mini-boss fiery, boss red); `BaseBattleManager.SetupBattle` calls `ArenaController.Instance.Apply(...)`.
- If the theme is missing or invalid the bootstrapper falls back to a plain floor plane.
- With `disableLegacySceneDecor` on, the hand-placed `Dungeon` root and the scene's `Directional Light` in `BattleScene` are deactivated at runtime (not deleted). If walls face the wrong way, adjust `wallYawOffset` on the theme.
- `Editor/BattleEnvironmentGenerator` (village scatter) is legacy and points at `Assets/OccaSoftware`, which is git-ignored.

## Conventions and gotchas
- Impact feel lives in `HitFeedback.Play(target, damage, crit)` (called next to each `DamagePopSpawner.Show` for a hit): target recoil (`Unit.PlayRecoil`), `CameraShake` trauma, and a very short `HitStop` on crits/kills. Unit movement is script-driven (root motion is forced off); the idle state is resolved from the controller's default state because prefabs name it `<Hero>_Idle`.
- Potions are run state (`RunManager.RunItems`), not prefab inventory: units are rebuilt each encounter, so `BaseBattleManager` copies the pack onto the hero and `PlayerUseItem` removes the potion from the run. Item rewards are `RewardChoice.battleItem`.
- Enemy corpses sink into the floor and deactivate (`Unit.SinkToGround`); first-time hints are `BattleUIManager.ShowTip` (flags `tip_*` in PlayerPrefs); the select screen's 3D model is `UI/HeroPreview` (RenderTexture rig at y=-500).
- Hero base stats (HP+MP+ATK+DEF+SPD+Luck) total 200 each; see the table in `RunMechanics.md` before changing a hero prefab.
- `BattleCamera` (added to the camera by `GameBootstrapper`) holds the camera presets; press `C` in battle to cycle them, the choice is saved in PlayerPrefs (`BattleCameraPreset`). Edit the `Presets` array to add or tune a view; preset 1 (High three-quarter) is the default, preset 0 is the original low over-the-shoulder camera.
- Units are re-instantiated every encounter; upgrades are re-applied from `RunManager` lists. Anything stored only on a `Unit` is lost between encounters.
- Attack enhancements work by cloning the `AttackAction` asset (`Instantiate`) and replacing the reference on the unit; attacks are matched by `actionName`, so names must be unique per hero.
- A hero's `Unit.unitName` is set from the prefab name and is the key for `LegendaryRelicSO.targetCharacterName`.
- Third-party packs under `Assets/` (SlimUI, Vefects, Hovl Studio, Violet Theme Ui, etc.) and `Assets/_Recovery` are not game code.
- Known balance/code issues are listed at the bottom of `RunMechanics.md`.
