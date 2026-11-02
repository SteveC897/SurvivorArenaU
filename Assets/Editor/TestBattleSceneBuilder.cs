using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SurvivorArena.Bootstrap;
using SurvivorArena.Data;
using SurvivorArena.Gameplay;
using SurvivorArena.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SurvivorArena.EditorTools
{
    /// <summary>
    /// 用代码生成测试战斗场景（MainGame.unity），保证场景可复现、引用不丢。
    /// 菜单：Tools/Survivor Arena/Rebuild Test Battle Scene
    /// 批处理：Unity -batchmode -quit -projectPath ... -executeMethod SurvivorArena.EditorTools.TestBattleSceneBuilder.BuildFromCommandLine
    ///
    /// 生成的场景只保留“视图 + 组合根”：
    ///   Main Camera / Ground / Player / EnemySpawner / HUD / BattleBootstrap
    /// 数值全部写进 Assets/Data/BattleConfig.asset，运行时由 BattleBootstrap 注入各层。
    /// </summary>
    public static class TestBattleSceneBuilder
    {
        private const float RunDuration = 300f;
        private const string ScenePath = "Assets/Scenes/MainGame.unity";
        private const string BatPrefabPath = "Assets/Prefabs/EnemyBat.prefab";
        private const string MaterialPath = "Assets/Materials/SpriteUnlit.mat";
        private const string ConfigPath = "Assets/Data/BattleConfig.asset";
        private const string ContentCatalogPath = "Assets/Data/ContentCatalog.asset";
        private const string CsvPath = "Assets/DataDocs/level01_enemy_spawn.csv";
        private const string ArtRoot = "Assets/Art/Generated/";
        private const string UnlitShaderPath =
            "Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Unlit-Default.shader";

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        [MenuItem("Tools/Survivor Arena/Rebuild Test Battle Scene", priority = 1)]
        public static void BuildFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            try
            {
                BuildInternal();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[场景构建] 失败：{exception}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                throw;
            }
        }

        private static void BuildInternal()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Data");

            Material spriteMaterial = EnsureUnlitMaterial();
            Sprite idle0 = LoadSprite(ArtRoot + "player_imelda_idle_0.png");
            Sprite idle1 = LoadSprite(ArtRoot + "player_imelda_idle_1.png");
            Sprite walk0 = LoadSprite(ArtRoot + "player_imelda_walk_0.png");
            Sprite walk1 = LoadSprite(ArtRoot + "player_imelda_walk_1.png");
            Sprite batSprite = LoadSprite(ArtRoot + "enemy_bat.png");
            Sprite batRedSprite = LoadSprite(ArtRoot + "enemy_bat_red.png");
            Sprite grassSprite = LoadSprite(ArtRoot + "tile_grass.png");
            Sprite panelSprite = LoadSprite(ArtRoot + "ui_panel.png");

            TextAsset waveCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            if (waveCsv == null)
            {
                throw new FileNotFoundException($"找不到波次表 CSV：{CsvPath}");
            }

            // Data 层：把所有可调数值写进一个配置资产。
            BattleConfig config = BuildConfig(waveCsv);
            ContentCatalog contentCatalog = BuildContentCatalog();
            SetRef(config, "contentCatalog", contentCatalog);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            WaveTable waveTable = WaveTable.Parse(waveCsv.text);
            waveTable.ValidateForRun(config.RunDurationSeconds);
            Debug.Log($"[场景构建] 波次表校验通过：{waveTable.Summary}");

            GameObject batPrefab = BuildBatPrefab(batSprite, spriteMaterial);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Presentation：地面
            var groundObject = new GameObject("Ground");
            var ground = groundObject.AddComponent<InfiniteGround>();
            SetRef(ground, "groundSprite", grassSprite);
            SetRef(ground, "spriteMaterial", spriteMaterial);
            SetInt(ground, "chunkSize", 32);
            SetInt(ground, "radius", 1);
            SetColor(ground, "tint", new Color(0.88f, 0.94f, 0.88f, 1f));

            // Gameplay + Presentation：主角
            var playerObject = new GameObject("Player");
            var playerRenderer = playerObject.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = idle0;
            playerRenderer.sharedMaterial = spriteMaterial;

            var playerMotor = playerObject.AddComponent<PlayerMotor>();

            var playerSpriteView = playerObject.AddComponent<PlayerSpriteView>();
            SetRef(playerSpriteView, "spriteRenderer", playerRenderer);
            SetRef(playerSpriteView, "motor", playerMotor);
            SetArray(playerSpriteView, "idleFrames", idle0, idle1);
            SetArray(playerSpriteView, "walkFrames", walk0, walk1);

            // Gameplay：敌人池
            var spawnerObject = new GameObject("EnemySpawner");
            var spawner = spawnerObject.AddComponent<EnemySpawner>();
            SetRef(spawner, "batPrefab", batPrefab.GetComponent<EnemyBat>());
            SetArray(spawner, "batVariants", batSprite, batRedSprite);

            // Presentation：相机
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.05f, 0.035f, 1f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

            var cameraFollow = cameraObject.AddComponent<CameraFollow>();
            SetRef(cameraFollow, "target", playerObject.transform);
            SetFloat(cameraFollow, "smoothTime", 0.12f);

            var pixelPerfectCamera = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfectCamera.assetsPPU = 32;
            pixelPerfectCamera.refResolutionX = 640;
            pixelPerfectCamera.refResolutionY = 360;
            pixelPerfectCamera.upscaleRT = false;
            pixelPerfectCamera.pixelSnapping = true;
            pixelPerfectCamera.cropFrameX = false;
            pixelPerfectCamera.cropFrameY = false;

            // Presentation：HUD（只订阅 EventBus，不挂玩法引用）
            BattleHud hud = BuildHud(panelSprite);
            var levelUpView = hud.gameObject.AddComponent<LevelUpView>();
            SetRef(levelUpView, "panelSprite", panelSprite);

            // UI 点击需要 EventSystem（旧 Input Manager 的 StandaloneInputModule）。
            var eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(null);

            // Bootstrap：唯一同时认识所有层的组合根
            var bootstrapObject = new GameObject("BattleBootstrap");
            var bootstrap = bootstrapObject.AddComponent<BattleBootstrap>();
            SetRef(bootstrap, "config", config);
            SetRef(bootstrap, "mainCamera", camera);
            SetRef(bootstrap, "playerMotor", playerMotor);
            SetRef(bootstrap, "spawner", spawner);
            SetBool(bootstrap, "logWaveStarts", true);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"保存场景失败：{ScenePath}");
            }

            AddSceneToBuildSettings();
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[场景构建] 完成：{ScenePath}（Bootstrap 已接线，HUD 通过 EventBus 订阅）");
        }

        private static BattleConfig BuildConfig(TextAsset waveCsv)
        {
            BattleConfig config = AssetDatabase.LoadAssetAtPath<BattleConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BattleConfig>();
                config.name = "BattleConfig";
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            SetRef(config, "waveTableCsv", waveCsv);
            SetFloat(config, "runDurationSeconds", RunDuration);
            SetFloat(config, "playerMoveSpeed", 4.2f);
            SetFloat(config, "enemySpawnRadiusMin", 8f);
            SetFloat(config, "enemySpawnRadiusMax", 10f);
            SetFloat(config, "enemySpeedMin", 2.4f);
            SetFloat(config, "enemySpeedMax", 2.9f);
            SetFloat(config, "enemyRadius", 0.4f);
            SetInt(config, "enemyPrewarmCount", 48);
            SetInt(config, "enemyMaxSpawnsPerFrame", 8);
            SetFloat(config, "recycleDistance", 16f);
            SetFloat(config, "recycleMargin", 1f);
            SetInt(config, "randomSeed", 20261031);
            SetInt(config, "xpGemMaxCount", 256);
            SetFloat(config, "xpGemMagnetRadius", 2f);
            SetFloat(config, "xpGemPickupRadius", 0.6f);
            SetFloat(config, "xpGemMagnetSpeed", 12f);
            SetInt(config, "maxWeaponSlots", 6);
            SetInt(config, "maxPassiveSlots", 6);
            SetFloat(config, "baseProjectileSpeed", 8f);
            SetFloat(config, "projectileLifetime", 2.5f);
            SetFloat(config, "garlicBaseRadius", 2f);
            SetFloat(config, "enemyHealth", 5f);
            SetInt(config, "enemyXp", 1);
            SetFloatArray(config, "bossSpawnTimes", 60f, 150f, 270f);
            SetIntArray(config, "bossChestRewards", 1, 3, 5);
            SetFloat(config, "bossSpawnRadius", 7f);
            SetFloat(config, "bossHealthBase", 120f);
            SetFloat(config, "bossHealthPerSpawn", 120f);
            SetFloat(config, "bossSpeed", 1.2f);
            SetFloat(config, "bossRadius", 0.8f);
            SetFloat(config, "bossVisualScale", 1.8f);
            SetInt(config, "bossXp", 30);
            SetFloat(config, "chestPickupRadius", 1.2f);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// 生成 10 武器 + 10 被动的内容目录。数值来自
        /// Docs/吸血鬼幸存者-本体武器与被动系统详解.md 第八节「首批实现清单」。
        /// 防御三件（护盔/空虚之心/愈伤番茄）保留定义但 Offered=false：
        /// 当前战斗范围没有玩家受伤/生命流程，先不把它们放进三选一。
        /// </summary>
        private static ContentCatalog BuildContentCatalog()
        {
            Sprite bolt = LoadSprite(ArtRoot + "proj_bolt_blue.png");
            Sprite weapon01 = LoadIcon("weapon_01");
            Sprite weapon03 = LoadIcon("weapon_03");
            Sprite weapon05 = LoadIcon("weapon_05");
            Sprite weapon07 = LoadIcon("weapon_07");
            Sprite weapon11 = LoadIcon("weapon_11");
            Sprite weapon13 = LoadIcon("weapon_13");
            Sprite weapon15 = LoadIcon("weapon_15");
            Sprite weapon17 = LoadIcon("weapon_17");
            Sprite weapon19 = LoadIcon("weapon_19");
            Sprite weapon21 = LoadIcon("weapon_21");

            WeaponDefinition whip = W(
                "whip", "神鞭", "向面朝方向横挥，命中前方/背后范围内全部敌人。",
                weapon01, weapon01, WeaponBehavior.Whip,
                L(10f, 1.35f, 1f, 1f, 0.12f, 1, 1, 0.2f, 2f),
                L(10f, 1.35f, 1f, 1f, 0.12f, 2, 1, 0.2f, 2f),
                L(15f, 1.35f, 1f, 1f, 0.12f, 2, 1, 0.2f, 2f),
                L(20f, 1.35f, 1.1f, 1f, 0.12f, 2, 1, 0.2f, 2f),
                L(25f, 1.35f, 1.1f, 1f, 0.12f, 2, 1, 0.2f, 2f),
                L(30f, 1.35f, 1.2f, 1f, 0.12f, 2, 1, 0.2f, 2f),
                L(35f, 1.35f, 1.2f, 1f, 0.12f, 2, 1, 0.2f, 2f),
                L(40f, 1.35f, 1.2f, 1f, 0.12f, 2, 1, 0.2f, 2f));

            WeaponDefinition magicWand = W(
                "magic_wand", "强能魔杖", "自动锁定最近敌人，发射直线魔法弹；升级增加弹数与穿透。",
                weapon03, bolt, WeaponBehavior.MagicWand,
                L(10f, 1.2f, 1f, 1f, 0f, 1, 1),
                L(10f, 1.2f, 1f, 1f, 0f, 2, 1),
                L(10f, 1.0f, 1f, 1f, 0f, 2, 1),
                L(10f, 1.0f, 1f, 1f, 0f, 3, 1),
                L(20f, 1.0f, 1f, 1f, 0f, 3, 1),
                L(20f, 1.0f, 1f, 1f, 0f, 4, 1),
                L(20f, 1.0f, 1f, 1f, 0f, 4, 2),
                L(30f, 1.0f, 1f, 1f, 0f, 4, 2));

            WeaponDefinition knife = W(
                "knife", "飞刀", "朝面朝方向连发飞刀，穿透多个敌人；升级增加弹数与穿透。",
                weapon05, weapon05, WeaponBehavior.Knife,
                L(6.5f, 1f, 1f, 1f, 0f, 1, 1, 0.3f, 3f),
                L(6.5f, 1f, 1f, 1f, 0f, 2, 1, 0.3f, 3f),
                L(11.5f, 1f, 1f, 1f, 0f, 3, 1, 0.3f, 3f),
                L(11.5f, 1f, 1f, 1f, 0f, 4, 1, 0.3f, 3f),
                L(11.5f, 1f, 1f, 1f, 0f, 4, 2, 0.3f, 3f),
                L(11.5f, 1f, 1f, 1f, 0f, 5, 2, 0.3f, 3f),
                L(16.5f, 1f, 1f, 1f, 0f, 6, 2, 0.3f, 3f),
                L(16.5f, 1f, 1f, 1f, 0f, 6, 3, 0.3f, 3f));

            WeaponDefinition axe = W(
                "axe", "斧头", "向斜上方抛出并落下，穿透多个敌人。",
                weapon07, weapon07, WeaponBehavior.Axe,
                L(20f, 4f, 1f, 1f, 1.5f, 1, 3, 0.3f, 2f),
                L(20f, 4f, 1f, 1f, 1.5f, 2, 3, 0.3f, 2f),
                L(40f, 4f, 1f, 1f, 1.5f, 2, 3, 0.3f, 2f),
                L(40f, 4f, 1f, 1f, 1.5f, 2, 5, 0.3f, 2f),
                L(40f, 4f, 1f, 1f, 1.5f, 3, 5, 0.3f, 2f),
                L(60f, 4f, 1f, 1f, 1.5f, 3, 5, 0.3f, 2f),
                L(60f, 4f, 1f, 1f, 1.5f, 3, 7, 0.3f, 2f),
                L(80f, 4f, 1f, 1f, 1.5f, 3, 7, 0.3f, 2f));

            WeaponDefinition bible = W(
                "king_bible", "国王圣经", "召唤环绕自身的圣经，持续伤害附近敌人。",
                weapon11, weapon11, WeaponBehavior.KingBible,
                L(10f, 3f, 1f, 1f, 3f, 1, 1),
                L(10f, 3f, 1f, 1f, 3f, 2, 1),
                L(10f, 3f, 1.25f, 1.3f, 3f, 2, 1),
                L(20f, 3f, 1.25f, 1.3f, 3.5f, 2, 1),
                L(20f, 3f, 1.25f, 1.3f, 3.5f, 3, 1),
                L(20f, 3f, 1.5f, 1.6f, 3.5f, 3, 1),
                L(30f, 3f, 1.5f, 1.6f, 4f, 3, 1),
                L(30f, 3f, 1.5f, 1.6f, 4f, 4, 1));

            WeaponDefinition fireWand = W(
                "fire_wand", "火焰魔杖", "随机选择敌人发射火球，火球造成高额伤害。",
                weapon13, weapon13, WeaponBehavior.FireWand,
                L(20f, 3f, 1f, 0.75f, 0f, 3, 1),
                L(30f, 3f, 1f, 0.75f, 0f, 3, 1),
                L(40f, 3f, 1f, 0.95f, 0f, 3, 1),
                L(50f, 3f, 1f, 0.95f, 0f, 3, 1),
                L(60f, 3f, 1f, 1.15f, 0f, 3, 1),
                L(70f, 3f, 1f, 1.15f, 0f, 3, 1),
                L(80f, 3f, 1f, 1.35f, 0f, 3, 1),
                L(90f, 3f, 1f, 1.35f, 0f, 3, 1));

            WeaponDefinition garlic = W(
                "garlic", "大蒜", "以自身为中心的光环，持续对范围内敌人造成伤害。",
                weapon15, weapon15, WeaponBehavior.Garlic,
                L(5f, 1.3f, 1f, 1f, 0f, 1, 1),
                L(7f, 1.3f, 1.4f, 1f, 0f, 1, 1),
                L(8f, 1.2f, 1.4f, 1f, 0f, 1, 1),
                L(9f, 1.2f, 1.6f, 1f, 0f, 1, 1),
                L(11f, 1.1f, 1.6f, 1f, 0f, 1, 1),
                L(12f, 1.1f, 1.8f, 1f, 0f, 1, 1),
                L(13f, 1f, 1.8f, 1f, 0f, 1, 1),
                L(15f, 1f, 2f, 1f, 0f, 1, 1));

            WeaponDefinition santaWater = W(
                "santa_water", "圣徒水", "在周围投掷圣水，形成持续伤害的水洼。",
                weapon17, weapon17, WeaponBehavior.SantaWater,
                L(10f, 4.5f, 1f, 1f, 2f, 1, 1),
                L(10f, 4.5f, 1.2f, 1f, 2f, 2, 1),
                L(20f, 4.5f, 1.2f, 1f, 2.5f, 2, 1),
                L(20f, 4.5f, 1.4f, 1f, 2.5f, 3, 1),
                L(30f, 4.5f, 1.4f, 1f, 2.75f, 3, 1),
                L(30f, 4.5f, 1.6f, 1f, 2.75f, 4, 1),
                L(35f, 4.5f, 1.6f, 1f, 3f, 4, 1),
                L(40f, 4.5f, 1.8f, 1f, 3f, 4, 1));

            WeaponDefinition runetracer = W(
                "runetracer", "痕印石", "发射会在屏幕边缘反弹的符文，持续穿透敌人。",
                weapon19, weapon19, WeaponBehavior.Runetracer,
                L(10f, 3f, 1f, 1f, 2.25f, 1, 99),
                L(15f, 3f, 1f, 1.2f, 2.25f, 1, 99),
                L(20f, 3f, 1f, 1.2f, 2.5f, 1, 99),
                L(20f, 3f, 1f, 1.2f, 2.5f, 2, 99),
                L(25f, 3f, 1f, 1.4f, 2.5f, 2, 99),
                L(30f, 3f, 1f, 1.4f, 2.75f, 2, 99),
                L(30f, 3f, 1f, 1.4f, 2.75f, 3, 99),
                L(30f, 3f, 1f, 1.4f, 3.25f, 3, 99));

            WeaponDefinition lightning = W(
                "lightning_ring", "闪电戒指", "在随机敌人位置落下闪电，造成范围伤害。",
                weapon21, weapon21, WeaponBehavior.LightningRing,
                L(15f, 4.5f, 1f, 1f, 0f, 2, 1),
                L(15f, 4.5f, 1f, 1f, 0f, 3, 1),
                L(25f, 4.5f, 2f, 1f, 0f, 3, 1),
                L(25f, 4.5f, 2f, 1f, 0f, 4, 1),
                L(45f, 4.5f, 3f, 1f, 0f, 4, 1),
                L(45f, 4.5f, 3f, 1f, 0f, 5, 1),
                L(65f, 4.5f, 4f, 1f, 0f, 5, 1),
                L(65f, 4.5f, 4f, 1f, 0f, 6, 1));

            PassiveDefinition spinach = P(
                "spinach", "菠菜", "力量 +10%/级（共 +50%）。",
                LoadIcon("passive_01"), PassiveEffect.Might, PassiveModifierKind.PercentAdd,
                new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, 5, true);

            PassiveDefinition armor = P(
                "armor", "护盔", "防御 +1/级；反伤暂未接入伤害流程。",
                LoadIcon("passive_02"), PassiveEffect.Armor, PassiveModifierKind.FlatAdd,
                new[] { 1f, 2f, 3f, 4f, 5f }, 5, false);

            PassiveDefinition hollowHeart = P(
                "hollow_heart", "空虚之心", "生命上限每级 ×1.2；当前无受伤流程。",
                LoadIcon("passive_03"), PassiveEffect.MaxHealth, PassiveModifierKind.PercentMul,
                new[] { 0.2f, 0.44f, 0.728f, 1.0736f, 1.48832f }, 5, false);

            PassiveDefinition pummarola = P(
                "pummarola", "愈伤番茄", "恢复 +0.2/级；当前无受伤/回血流程。",
                LoadIcon("passive_04"), PassiveEffect.Recovery, PassiveModifierKind.FlatAdd,
                new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f }, 5, false);

            PassiveDefinition emptyTome = P(
                "empty_tome", "空白之书", "冷却时间 −8%/级（共 −40%）。",
                LoadIcon("passive_05"), PassiveEffect.Cooldown, PassiveModifierKind.PercentAdd,
                new[] { -0.08f, -0.16f, -0.24f, -0.32f, -0.4f }, 5, true);

            PassiveDefinition candelabrador = P(
                "candelabrador", "烛台", "攻击范围 +10%/级（共 +50%）。",
                LoadIcon("passive_06"), PassiveEffect.Area, PassiveModifierKind.PercentAdd,
                new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, 5, true);

            PassiveDefinition bracer = P(
                "bracer", "护腕", "飞射速度 +10%/级（共 +50%）。",
                LoadIcon("passive_07"), PassiveEffect.ProjectileSpeed, PassiveModifierKind.PercentAdd,
                new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, 5, true);

            PassiveDefinition spellbinder = P(
                "spellbinder", "咒缚盒", "持续时间 +10%/级（共 +50%）。",
                LoadIcon("passive_08"), PassiveEffect.Duration, PassiveModifierKind.PercentAdd,
                new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, 5, true);

            PassiveDefinition duplicator = P(
                "duplicator", "双鸾宝镯", "发射数量 +1/级（最多 +2）。",
                LoadIcon("passive_09"), PassiveEffect.Amount, PassiveModifierKind.FlatAdd,
                new[] { 1f, 2f }, 2, true);

            PassiveDefinition attractorb = P(
                "attractorb", "念力法球", "念力每级更大，拾取范围最多 ×3.98。",
                LoadIcon("passive_11"), PassiveEffect.Magnet, PassiveModifierKind.PercentMul,
                new[] { 0.5f, 0.995f, 1.49375f, 1.9925f, 2.98003f }, 5, true);

            Sprite gemGreen = LoadSprite(ArtRoot + "pickup_xp_gem_green.png");
            Sprite gemRed = LoadSprite(ArtRoot + "pickup_xp_gem_red.png");

            ContentCatalog catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(ContentCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                catalog.name = "ContentCatalog";
                AssetDatabase.CreateAsset(catalog, ContentCatalogPath);
            }

            catalog.SetContent(
                new[]
                {
                    magicWand, knife, whip, axe, bible, fireWand, garlic, santaWater,
                    runetracer, lightning
                },
                new[]
                {
                    spinach, armor, hollowHeart, pummarola, emptyTome, candelabrador,
                    bracer, spellbinder, duplicator, attractorb
                },
                gemGreen,
                gemRed);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static Sprite LoadIcon(string name)
        {
            return LoadSprite(ArtRoot + "Icons/" + name + ".png");
        }

        private static WeaponDefinition W(
            string id,
            string name,
            string description,
            Sprite icon,
            Sprite projectile,
            WeaponBehavior behavior,
            params WeaponLevelStats[] levels)
        {
            return new WeaponDefinition
            {
                Id = id,
                DisplayName = name,
                Description = description,
                Icon = icon,
                ProjectileSprite = projectile,
                Behavior = behavior,
                MaxLevel = levels.Length,
                Weight = 100,
                Levels = levels
            };
        }

        private static WeaponLevelStats L(
            float damage,
            float cooldown,
            float area = 1f,
            float speed = 1f,
            float duration = 0f,
            int amount = 1,
            int pierce = 1,
            float critChance = 0f,
            float critMultiplier = 1f)
        {
            return new WeaponLevelStats(
                damage,
                cooldown,
                area,
                speed,
                duration,
                amount,
                pierce,
                0f,
                critChance,
                critMultiplier);
        }

        private static PassiveDefinition P(
            string id,
            string name,
            string description,
            Sprite icon,
            PassiveEffect effect,
            PassiveModifierKind modifierKind,
            float[] values,
            int maxLevel,
            bool offered)
        {
            return new PassiveDefinition
            {
                Id = id,
                DisplayName = name,
                Description = description,
                Icon = icon,
                MaxLevel = maxLevel,
                Weight = 100,
                Effect = effect,
                ModifierKind = modifierKind,
                LevelValues = values,
                Offered = offered
            };
        }

        private static Material EnsureUnlitMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(UnlitShaderPath);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("找不到 URP 2D Sprite-Unlit-Default Shader。");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SpriteUnlit" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static GameObject BuildBatPrefab(Sprite batSprite, Material material)
        {
            var root = new GameObject("EnemyBat");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = batSprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 0;

            root.AddComponent<EnemyBat>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BatPrefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                throw new InvalidOperationException($"保存预制体失败：{BatPrefabPath}");
            }

            return prefab;
        }

        private static BattleHud BuildHud(Sprite panelSprite)
        {
            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            // 让 9-slice（ui_panel 为 48 PPU）的 12px 边在 UI 上仍是 12px。
            scaler.referencePixelsPerUnit = panelSprite != null ? panelSprite.pixelsPerUnit : 100f;

            var hud = canvasObject.AddComponent<BattleHud>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            RectTransform inventory = CreateUi("Inventory", canvasObject.transform, TopLeft, TopLeft,
                new Vector2(24f, -24f), new Vector2(366f, 170f));

            RectTransform backdrop = CreateUi("Backdrop", inventory, TopLeft, TopLeft,
                new Vector2(-10f, 10f), new Vector2(386f, 190f));
            AddImage(backdrop, null, new Color(0.02f, 0.03f, 0.02f, 0.42f), false);

            Text weaponLabel = CreateLabel("WeaponLabel", inventory, new Vector2(2f, 0f), "武器 0/6", font);
            weaponLabel.color = new Color(0.78f, 0.74f, 0.6f, 0.95f);

            var weaponSlots = new List<Image>();
            for (int i = 0; i < 6; i++)
            {
                RectTransform slot = CreateUi($"WeaponSlot{i + 1}", inventory, TopLeft, TopLeft,
                    new Vector2(i * 62f, -24f), new Vector2(56f, 56f));
                weaponSlots.Add(AddImage(slot, panelSprite, Color.white, true));
            }

            Text passiveLabel = CreateLabel("PassiveLabel", inventory, new Vector2(2f, -88f), "被动 0/6", font);
            passiveLabel.color = new Color(0.78f, 0.74f, 0.6f, 0.95f);

            var passiveSlots = new List<Image>();
            for (int i = 0; i < 6; i++)
            {
                RectTransform slot = CreateUi($"PassiveSlot{i + 1}", inventory, TopLeft, TopLeft,
                    new Vector2(i * 62f, -112f), new Vector2(56f, 56f));
                passiveSlots.Add(AddImage(slot, panelSprite, Color.white, true));
            }

            RectTransform timer = CreateUi("Timer", canvasObject.transform, TopCenter, TopCenter,
                new Vector2(0f, -14f), new Vector2(360f, 64f));
            Text timerText = AddText(timer, "5:00", 50, TextAnchor.MiddleCenter,
                new Color(1f, 0.96f, 0.86f, 1f), font, 2f, FontStyle.Bold);

            RectTransform wave = CreateUi("Wave", canvasObject.transform, TopCenter, TopCenter,
                new Vector2(0f, -82f), new Vector2(360f, 30f));
            Text waveText = AddText(wave, "波次 1/30", 20, TextAnchor.MiddleCenter,
                new Color(0.82f, 0.86f, 0.76f, 1f), font, 1f);

            RectTransform kills = CreateUi("Kills", canvasObject.transform, TopRight, TopRight,
                new Vector2(-28f, -24f), new Vector2(300f, 32f));
            Text killsText = AddText(kills, "击杀 0", 22, TextAnchor.MiddleRight,
                new Color(0.95f, 0.9f, 0.78f, 1f), font, 1f);

            // 底部经验条
            RectTransform xpBackground = CreateUi("XpBar", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(760f, 18f));
            AddImage(xpBackground, panelSprite, new Color(0.05f, 0.07f, 0.05f, 0.9f), true);

            RectTransform xpFillRect = CreateUi("XpFill", xpBackground,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(752f, 12f));
            Image xpFill = AddImage(xpFillRect, panelSprite, new Color(0.42f, 0.9f, 0.45f, 1f), false);

            RectTransform levelRect = CreateUi("Level", canvasObject.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(260f, 32f));
            Text levelText = AddText(levelRect, "等级 1", 22, TextAnchor.MiddleLeft,
                new Color(0.85f, 0.92f, 0.72f, 1f), font, 1f, FontStyle.Bold);

            RectTransform xpTextRect = CreateUi("XpText", canvasObject.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(260f, 32f));
            Text xpText = AddText(xpTextRect, "0/5", 20, TextAnchor.MiddleRight,
                new Color(0.78f, 0.84f, 0.72f, 1f), font, 1f);

            RectTransform hint = CreateUi("Hint", canvasObject.transform, BottomLeft, BottomLeft,
                new Vector2(24f, 70f), new Vector2(1000f, 30f));
            AddText(hint, "WASD / 方向键 移动　·　拾取经验宝石　·　升级三选一", 18, TextAnchor.MiddleLeft,
                new Color(0.86f, 0.9f, 0.82f, 0.65f), font, 1f);

            RectTransform toast = CreateUi("Toast", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 88f), new Vector2(1200f, 40f));
            Text toastText = AddText(toast, "", 24, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.4f, 1f), font, 2f, FontStyle.Bold);
            toast.gameObject.SetActive(false);

            RectTransform overlay = CreateStretchUi("Victory", canvasObject.transform);
            AddImage(overlay, null, new Color(0f, 0f, 0f, 0.72f), false);
            var victoryGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            victoryGroup.alpha = 0f;

            RectTransform panel = CreateUi("Panel", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 380f));
            AddImage(panel, panelSprite, Color.white, true);

            RectTransform title = CreateUi("Title", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 105f), new Vector2(640f, 110f));
            AddText(title, "胜利！", 76, TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.32f, 1f), font, 3f, FontStyle.Bold);

            RectTransform stats = CreateUi("Stats", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(680f, 44f));
            Text statsText = AddText(stats, "存活 5:00　·　击杀 0", 26, TextAnchor.MiddleCenter,
                new Color(0.94f, 0.92f, 0.85f, 1f), font, 1f);

            RectTransform restart = CreateUi("Restart", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -105f), new Vector2(600f, 36f));
            AddText(restart, "按 R 重新开始", 22, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.76f, 0.7f, 1f), font, 1f);

            overlay.gameObject.SetActive(false);

            SetRef(hud, "timerText", timerText);
            SetRef(hud, "waveText", waveText);
            SetRef(hud, "killsText", killsText);
            SetRef(hud, "xpFill", xpFill);
            SetRef(hud, "levelText", levelText);
            SetRef(hud, "xpText", xpText);
            SetRef(hud, "weaponLabelText", weaponLabel);
            SetRef(hud, "passiveLabelText", passiveLabel);
            SetRef(hud, "toastText", toastText);
            SetArray(hud, "weaponSlots", weaponSlots.Cast<Object>().ToArray());
            SetArray(hud, "passiveSlots", passiveSlots.Cast<Object>().ToArray());
            SetRef(hud, "victoryGroup", victoryGroup);
            SetRef(hud, "victoryStatsText", statsText);

            return hud;
        }

        private static Text CreateLabel(string name, Transform parent, Vector2 position, string content, Font font)
        {
            RectTransform rect = CreateUi(name, parent, TopLeft, TopLeft, position, new Vector2(300f, 20f));
            return AddText(rect, content, 16, TextAnchor.MiddleLeft,
                new Color(0.78f, 0.74f, 0.6f, 0.95f), font, 1f);
        }

        private static RectTransform CreateUi(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform CreateStretchUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool sliced)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private static Text AddText(
            RectTransform rect,
            string content,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Font font,
            float outlineWidth,
            FontStyle style = FontStyle.Normal)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;

            if (outlineWidth > 0f)
            {
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);
                outline.useGraphicAlpha = true;
            }

            return text;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite candidate)
                    {
                        sprite = candidate;
                        break;
                    }
                }
            }

            if (sprite == null)
            {
                throw new FileNotFoundException($"找不到 Sprite 资源：{path}");
            }

            return sprite;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path != ScenePath)
                {
                    scenes.Add(existing[i]);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException($"无法创建目录：{path}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            Mutate(target, field, property => property.objectReferenceValue = value);
        }

        private static void SetFloat(Object target, string field, float value)
        {
            Mutate(target, field, property => property.floatValue = value);
        }

        private static void SetInt(Object target, string field, int value)
        {
            Mutate(target, field, property => property.intValue = value);
        }

        private static void SetBool(Object target, string field, bool value)
        {
            Mutate(target, field, property => property.boolValue = value);
        }

        private static void SetColor(Object target, string field, Color value)
        {
            Mutate(target, field, property => property.colorValue = value);
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            Mutate(target, field, property =>
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });
        }

        private static void SetFloatArray(Object target, string field, params float[] values)
        {
            Mutate(target, field, property =>
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).floatValue = values[i];
                }
            });
        }

        private static void SetIntArray(Object target, string field, params int[] values)
        {
            Mutate(target, field, property =>
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).intValue = values[i];
                }
            });
        }

        private static void Mutate(Object target, string field, Action<SerializedProperty> mutate)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} 缺少序列化字段“{field}”，场景构建器与运行时脚本不一致。");
            }

            mutate(property);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
