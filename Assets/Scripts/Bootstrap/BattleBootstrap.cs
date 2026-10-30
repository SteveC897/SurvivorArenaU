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
    /// 负责：
    ///   1. 读取 Data.BattleConfig / WaveTable；
    ///   2. new 出 Core 服务与 Gameplay 系统；
    ///   3. 把 Platform 输入、Presentation 相机视口注入 Gameplay；
    ///   4. 每帧按固定顺序驱动它们。
    /// Gameplay 不引用 Presentation，HUD 只通过 Core.EventBus 接收状态。
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
        [SerializeField] private AuraView auraView;

        [Header("调试")]
        [SerializeField] private bool logWaveStarts = true;

        private GameClock _clock;
        private IPlayerInput _input;
        private KillAuraSystem _killAura;
        private OffscreenRecycler _recycler;
        private BattleSession _session;

        private void Start()
        {
            // 测试场景会在后台截图/长时间挂机，保证失焦时仍推进。
            Application.runInBackground = true;

            if (config == null || mainCamera == null || playerMotor == null || spawner == null || auraView == null)
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

            // Platform：Gameplay 只拿到 Core.IPlayerInput，不知道底层用的是旧 Input。
            _input = new LegacyPlayerInput(KeyCode.R);

            // Gameplay
            playerMotor.Initialize(_input, config.PlayerMoveSpeed);
            spawner.Initialize(playerMotor.transform, config);
            auraView.Initialize(config.AuraRadius);

            var viewport = new CameraViewport(mainCamera);
            _recycler = new OffscreenRecycler(spawner, playerMotor.transform, config, viewport);
            _killAura = new KillAuraSystem(spawner, playerMotor.transform, config.AuraRadius);

            var scheduler = new WaveScheduler(waveTable);
            _session = new BattleSession(_clock, scheduler, spawner, playerMotor, config, logWaveStarts);
            _session.Start();
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            _input.Poll();

            if (_session.IsVictory)
            {
                if (_input.RestartPressed)
                {
                    ReloadScene();
                }

                return;
            }

            _clock.Tick(Time.deltaTime);
            playerMotor.Tick(_clock.DeltaTime);
            _session.Tick(_clock.DeltaTime);
        }

        private void LateUpdate()
        {
            if (_session == null || _session.IsVictory)
            {
                return;
            }

            // 此时 EnemyBat.Update 已经跑完：先判光环击杀，再处理跑得太远的蝙蝠。
            _killAura.Tick();
            _recycler.Tick();
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
