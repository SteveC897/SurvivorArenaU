using System;
using System.Collections.Generic;
using System.IO;
using SurvivorArena.Bootstrap;
using SurvivorArena.Data;
using SurvivorArena.Gameplay;
using SurvivorArena.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

            var auraView = playerObject.AddComponent<AuraView>();

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

            // Bootstrap：唯一同时认识所有层的组合根
            var bootstrapObject = new GameObject("BattleBootstrap");
            var bootstrap = bootstrapObject.AddComponent<BattleBootstrap>();
            SetRef(bootstrap, "config", config);
            SetRef(bootstrap, "mainCamera", camera);
            SetRef(bootstrap, "playerMotor", playerMotor);
            SetRef(bootstrap, "spawner", spawner);
            SetRef(bootstrap, "auraView", auraView);
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
            SetFloat(config, "auraRadius", 2f);
            SetFloat(config, "enemySpawnRadiusMin", 12f);
            SetFloat(config, "enemySpawnRadiusMax", 14f);
            SetFloat(config, "enemySpeedMin", 2.4f);
            SetFloat(config, "enemySpeedMax", 2.9f);
            SetFloat(config, "enemyRadius", 0.4f);
            SetInt(config, "enemyPrewarmCount", 48);
            SetInt(config, "enemyMaxSpawnsPerFrame", 8);
            SetFloat(config, "recycleDistance", 16f);
            SetFloat(config, "recycleMargin", 1f);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
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

            for (int i = 0; i < 6; i++)
            {
                RectTransform slot = CreateUi($"WeaponSlot{i + 1}", inventory, TopLeft, TopLeft,
                    new Vector2(i * 62f, -24f), new Vector2(56f, 56f));
                AddImage(slot, panelSprite, Color.white, true);
            }

            Text passiveLabel = CreateLabel("PassiveLabel", inventory, new Vector2(2f, -88f), "被动 0/6", font);
            passiveLabel.color = new Color(0.78f, 0.74f, 0.6f, 0.95f);

            for (int i = 0; i < 6; i++)
            {
                RectTransform slot = CreateUi($"PassiveSlot{i + 1}", inventory, TopLeft, TopLeft,
                    new Vector2(i * 62f, -112f), new Vector2(56f, 56f));
                AddImage(slot, panelSprite, Color.white, true);
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

            RectTransform hint = CreateUi("Hint", canvasObject.transform, BottomLeft, BottomLeft,
                new Vector2(24f, 18f), new Vector2(900f, 30f));
            AddText(hint, "WASD / 方向键 移动　·　光环内敌人即死", 18, TextAnchor.MiddleLeft,
                new Color(0.86f, 0.9f, 0.82f, 0.65f), font, 1f);

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
