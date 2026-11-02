using System;
using SurvivorArena.Core;
using SurvivorArena.Data;
using SurvivorArena.Gameplay;
using SurvivorArena.Platform;
using SurvivorArena.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivorArena.Bootstrap
{
    /// <summary>
    /// 组合根（Bootstrap 层）：唯一允许同时认识所有层的地方。
    /// 创建属性表、背包、经验/升级、武器、拾取、宝箱、投射物与表现层，再按固定顺序驱动。
    /// 升级时只暂停 Gameplay（GamePause），不修改 Time.timeScale，UI 动画与输入仍走真实时间。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] private BattleConfig config;

        [Header("场景引用")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private EnemySpawner spawner;

        [Header("调试")]
        [SerializeField] private bool logWaveStarts = true;

        private GameClock _clock;
        private IPlayerInput _input;
        private PlayerStats _playerStats;
        private Loadout _loadout;
        private ExperienceSystem _experience;
        private UpgradeSystem _upgrades;
        private PickupSystem _pickups;
        private ProjectileSystem _projectiles;
        private AreaEffectSystem _areas;
        private OrbitEffectSystem _orbits;
        private WeaponSystem _weapons;
        private ChestSystem _chests;
        private OffscreenRecycler _recycler;
        private BattleSession _session;
        private CombatPresenter _presenter;

        /// <summary>供后续系统/调试读取；正常系统由组合根注入。</summary>
        public PlayerStats Stats => _playerStats;

        public Loadout CurrentLoadout => _loadout;

        private void Start()
        {
            Application.runInBackground = true;
            GamePause.Reset();

            if (config == null || mainCamera == null || playerMotor == null || spawner == null)
            {
                Debug.LogError($"{nameof(BattleBootstrap)} 的场景引用或配置不完整。", this);
                enabled = false;
                return;
            }

            if (config.WaveTableCsv == null)
            {
                Debug.LogError($"{nameof(BattleBootstrap)} 的 BattleConfig 没有挂载波次表 CSV。", config);
                enabled = false;
                return;
            }

            ContentCatalog catalog = config.ContentCatalog;
            if (catalog == null)
            {
                Debug.LogError($"{nameof(BattleBootstrap)} 的 BattleConfig 没有挂载 ContentCatalog。", config);
                enabled = false;
                return;
            }

            WeaponDefinition startingWeapon = catalog.FindWeapon("magic_wand");
            if (startingWeapon == null)
            {
                Debug.LogError($"{nameof(BattleBootstrap)} 的内容目录缺少开局武器 magic_wand（强能魔杖）。", catalog);
                enabled = false;
                return;
            }

            WaveTable waveTable;
            try
            {
                waveTable = WaveTable.Parse(config.WaveTableCsv.text);
                waveTable.ValidateForRun(config.RunDurationSeconds);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[波次表] 解析失败：{exception.Message}", config.WaveTableCsv);
                enabled = false;
                return;
            }

            // Core
            _clock = new GameClock();

            // Platform
            _input = new LegacyPlayerInput(KeyCode.R);

            // Gameplay：属性表 → 背包 → 经验/升级
            _playerStats = new PlayerStats(config);
            _loadout = new Loadout(_playerStats, config.MaxWeaponSlots, config.MaxPassiveSlots);
            _experience = new ExperienceSystem();
            _upgrades = new UpgradeSystem(
                _playerStats,
                _loadout,
                _experience,
                catalog,
                config.RandomSeed);

            playerMotor.Initialize(_input, _playerStats.MoveSpeed);
            spawner.Initialize(playerMotor.transform, config);

            var viewport = new CameraViewport(mainCamera);
            _recycler = new OffscreenRecycler(spawner, playerMotor.transform, config, viewport);
            _projectiles = new ProjectileSystem(spawner, viewport);
            _areas = new AreaEffectSystem(spawner);
            _orbits = new OrbitEffectSystem(spawner);
            _pickups = new PickupSystem(playerMotor.transform, config, _playerStats);
            _weapons = new WeaponSystem(
                _playerStats,
                _loadout,
                spawner,
                playerMotor.transform,
                playerMotor,
                _projectiles,
                _areas,
                _orbits,
                config);
            _chests = new ChestSystem(spawner, playerMotor.transform, config, _upgrades);

            var scheduler = new WaveScheduler(waveTable);
            _session = new BattleSession(_clock, scheduler, spawner, playerMotor, config, logWaveStarts);
            _session.Start();

            // 开局武器：强能魔杖。必须在 BattleStarted 之后加，HUD 会先清空槽位再收到装备事件。
            _loadout.AddWeapon(startingWeapon);
            _loadout.PublishChanged();
            _experience.Publish();

            SpriteRenderer playerRenderer = playerMotor.GetComponentInChildren<SpriteRenderer>(true);
            Material spriteMaterial = playerRenderer != null ? playerRenderer.sharedMaterial : null;
            if (spriteMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    spriteMaterial = new Material(shader);
                }
            }

            _presenter = new CombatPresenter(
                transform,
                _projectiles,
                _areas,
                _orbits,
                _pickups,
                _chests,
                catalog.XpGemGreen,
                catalog.XpGemRed,
                spriteMaterial);

            Debug.Log(
                $"[战斗开始] 伊梅尔达 · 初始武器「{startingWeapon.DisplayName}」；" +
                $"内容 {catalog.Weapons?.Length ?? 0} 武器 / {catalog.Passives?.Length ?? 0} 被动。");
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            _input.Poll();

            bool paused = _upgrades != null && _upgrades.IsChoosing;
            GamePause.SetPaused(paused);

            if (_session.IsVictory)
            {
                GamePause.SetPaused(false);
                if (_input.RestartPressed)
                {
                    ReloadScene();
                }

                return;
            }

            if (paused)
            {
                return;
            }

            _clock.Tick(Time.deltaTime);
            float deltaTime = _clock.DeltaTime;

            playerMotor.Tick(deltaTime);
            _session.Tick(deltaTime);

            if (_session.IsVictory)
            {
                GamePause.SetPaused(false);
                return;
            }

            _weapons.Tick(deltaTime);
            _projectiles.Tick(deltaTime);
            _areas.Tick(deltaTime);
            _orbits.Tick(deltaTime);
            _pickups.Tick(deltaTime);
            _chests.Tick(deltaTime);
            _upgrades.Tick();

            // 如果本帧刚弹出升级，立即冻结逻辑；UI 仍按真实时间运行。
            GamePause.SetPaused(_upgrades.IsChoosing);
        }

        private void LateUpdate()
        {
            if (_session == null)
            {
                return;
            }

            if (!_session.IsVictory && !(_upgrades != null && _upgrades.IsChoosing))
            {
                _recycler.Tick();
            }

            _presenter?.Tick();
        }

        private void OnDestroy()
        {
            GamePause.Reset();
            _experience?.Dispose();
            _upgrades?.Dispose();
            _pickups?.Dispose();
            _chests?.Dispose();
        }

        private void ReloadScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(scene.name);
            }
        }
    }
}
