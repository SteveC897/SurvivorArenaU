using System.Collections;
using SurvivorArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 测试场景 HUD（Presentation 层）。
    /// 只订阅 Core.EventBus 的局内事件来刷新显示，不持有任何 Gameplay 对象；
    /// 左上角空装备栏、顶部计时/波次、右上击杀数、局末胜利面板都在这里。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHud : MonoBehaviour
    {
        [Header("常驻信息")]
        [SerializeField] private Text timerText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text killsText;

        [Header("胜利面板")]
        [SerializeField] private CanvasGroup victoryGroup;
        [SerializeField] private Text victoryStatsText;
        [SerializeField, Min(0f)] private float victoryFadeDuration = 0.35f;

        [Header("计时颜色")]
        [SerializeField] private Color timerColor = new Color(1f, 0.96f, 0.86f, 1f);
        [SerializeField] private Color timerWarningColor = new Color(1f, 0.55f, 0.34f, 1f);
        [SerializeField, Min(0f)] private float warningThreshold = 30f;

        private int _lastSeconds = -1;
        private int _lastWaveIndex = int.MinValue;
        private int _lastWaveTotal = -1;
        private int _lastKills = -1;

        private void Awake()
        {
            ApplyFont();
            HideVictory();
        }

        private void OnEnable()
        {
            // 先退订再订阅，避免对象被反复启用时重复注册。
            Unsubscribe();
            EventBus<BattleStartedEvent>.Subscribe(OnBattleStarted);
            EventBus<WaveStartedEvent>.Subscribe(OnWaveStarted);
            EventBus<BattleTimeChangedEvent>.Subscribe(OnTimeChanged);
            EventBus<EnemyKilledEvent>.Subscribe(OnEnemyKilled);
            EventBus<BattleVictoryEvent>.Subscribe(OnVictory);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            EventBus<BattleStartedEvent>.Unsubscribe(OnBattleStarted);
            EventBus<WaveStartedEvent>.Unsubscribe(OnWaveStarted);
            EventBus<BattleTimeChangedEvent>.Unsubscribe(OnTimeChanged);
            EventBus<EnemyKilledEvent>.Unsubscribe(OnEnemyKilled);
            EventBus<BattleVictoryEvent>.Unsubscribe(OnVictory);
        }

        private void OnBattleStarted(BattleStartedEvent evt)
        {
            HideVictory();
            SetTime(evt.DurationSeconds);
            SetWave(0, evt.TotalWaves);
            SetKills(0);
        }

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            SetWave(evt.WaveIndex, evt.TotalWaves);
        }

        private void OnTimeChanged(BattleTimeChangedEvent evt)
        {
            SetTime(evt.RemainingSeconds);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            SetKills(evt.TotalKills);
        }

        private void OnVictory(BattleVictoryEvent evt)
        {
            ShowVictory(evt.SurvivedSeconds, evt.Kills);
        }

        private void SetTime(float remainingSeconds)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
            if (totalSeconds == _lastSeconds)
            {
                return;
            }

            _lastSeconds = totalSeconds;
            if (timerText != null)
            {
                timerText.text = FormatTime(totalSeconds);
                timerText.color = totalSeconds <= warningThreshold && totalSeconds > 0
                    ? timerWarningColor
                    : timerColor;
            }
        }

        private void SetWave(int waveIndex, int waveTotal)
        {
            if (waveIndex == _lastWaveIndex && waveTotal == _lastWaveTotal)
            {
                return;
            }

            _lastWaveIndex = waveIndex;
            _lastWaveTotal = waveTotal;
            if (waveText != null)
            {
                waveText.text = $"波次 {waveIndex}/{waveTotal}";
            }
        }

        private void SetKills(int kills)
        {
            if (kills == _lastKills)
            {
                return;
            }

            _lastKills = kills;
            if (killsText != null)
            {
                killsText.text = $"击杀 {kills}";
            }
        }

        private void ShowVictory(float survivedSeconds, int kills)
        {
            if (victoryStatsText != null)
            {
                victoryStatsText.text = $"存活 {FormatTime(survivedSeconds)}　·　击杀 {kills}";
            }

            if (victoryGroup == null)
            {
                return;
            }

            StopAllCoroutines();
            victoryGroup.gameObject.SetActive(true);
            victoryGroup.alpha = 0f;
            StartCoroutine(FadeInVictory());
        }

        private void HideVictory()
        {
            if (victoryGroup == null)
            {
                return;
            }

            StopAllCoroutines();
            victoryGroup.alpha = 0f;
            victoryGroup.gameObject.SetActive(false);
        }

        private IEnumerator FadeInVictory()
        {
            float elapsed = 0f;
            while (elapsed < victoryFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                victoryGroup.alpha = Mathf.Clamp01(elapsed / victoryFadeDuration);
                yield return null;
            }

            victoryGroup.alpha = 1f;
        }

        private void ApplyFont()
        {
            Font font = HudFont.Get();
            if (font == null)
            {
                return;
            }

            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
                texts[i].SetAllDirty();
            }
        }

        private static string FormatTime(float seconds)
        {
            return FormatTime(Mathf.CeilToInt(Mathf.Max(0f, seconds)));
        }

        private static string FormatTime(int totalSeconds)
        {
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }

        private void OnValidate()
        {
            victoryFadeDuration = Mathf.Max(0f, victoryFadeDuration);
            warningThreshold = Mathf.Max(0f, warningThreshold);
        }
    }
}
