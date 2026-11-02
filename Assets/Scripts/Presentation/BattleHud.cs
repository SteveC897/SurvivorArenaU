using System.Collections;
using SurvivorArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 战斗场景 HUD（Presentation 层）。
    /// 只订阅 Core.EventBus：计时/波次/击杀、经验条与等级、6+6 装备槽、宝箱/ Boss 提示、胜利面板。
    /// 不持有任何 Gameplay 对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHud : MonoBehaviour
    {
        [Header("常驻信息")]
        [SerializeField] private Text timerText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text killsText;

        [Header("经验与等级")]
        [SerializeField] private Image xpFill;
        [SerializeField] private Text levelText;
        [SerializeField] private Text xpText;

        [Header("装备槽")]
        [SerializeField] private Text weaponLabelText;
        [SerializeField] private Text passiveLabelText;
        [SerializeField] private Image[] weaponSlots;
        [SerializeField] private Image[] passiveSlots;

        [Header("提示")]
        [SerializeField] private Text toastText;

        [Header("胜利面板")]
        [SerializeField] private CanvasGroup victoryGroup;
        [SerializeField] private Text victoryStatsText;
        [SerializeField, Min(0f)] private float victoryFadeDuration = 0.35f;

        [Header("计时颜色")]
        [SerializeField] private Color timerColor = new Color(1f, 0.96f, 0.86f, 1f);
        [SerializeField] private Color timerWarningColor = new Color(1f, 0.55f, 0.34f, 1f);
        [SerializeField, Min(0f)] private float warningThreshold = 30f;

        private Image[] _weaponIcons;
        private Image[] _passiveIcons;
        private Text[] _weaponLevels;
        private Text[] _passiveLevels;
        private Coroutine _toastCoroutine;
        private int _lastSeconds = -1;
        private int _lastWaveIndex = int.MinValue;
        private int _lastWaveTotal = -1;
        private int _lastKills = -1;

        private void Awake()
        {
            BuildSlotOverlays();
            ApplyFont();
            HideVictory();
            HideToast();

            if (xpFill != null)
            {
                xpFill.type = Image.Type.Filled;
                xpFill.fillMethod = Image.FillMethod.Horizontal;
                xpFill.fillOrigin = 0;
                xpFill.fillAmount = 0f;
            }

            SetLevel(1, 0, 5);
            SetLoadoutEmpty();
        }

        private void OnEnable()
        {
            Unsubscribe();
            EventBus<BattleStartedEvent>.Subscribe(OnBattleStarted);
            EventBus<WaveStartedEvent>.Subscribe(OnWaveStarted);
            EventBus<BattleTimeChangedEvent>.Subscribe(OnTimeChanged);
            EventBus<EnemyKilledEvent>.Subscribe(OnEnemyKilled);
            EventBus<ExperienceChangedEvent>.Subscribe(OnExperienceChanged);
            EventBus<LoadoutChangedEvent>.Subscribe(OnLoadoutChanged);
            EventBus<BossSpawnedEvent>.Subscribe(OnBossSpawned);
            EventBus<ChestOpenedEvent>.Subscribe(OnChestOpened);
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
            EventBus<ExperienceChangedEvent>.Unsubscribe(OnExperienceChanged);
            EventBus<LoadoutChangedEvent>.Unsubscribe(OnLoadoutChanged);
            EventBus<BossSpawnedEvent>.Unsubscribe(OnBossSpawned);
            EventBus<ChestOpenedEvent>.Unsubscribe(OnChestOpened);
            EventBus<BattleVictoryEvent>.Unsubscribe(OnVictory);
        }

        private void OnBattleStarted(BattleStartedEvent evt)
        {
            HideVictory();
            HideToast();
            SetTime(evt.DurationSeconds);
            SetWave(0, evt.TotalWaves);
            SetKills(0);
            SetLevel(1, 0, 5);
            SetLoadoutEmpty();
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

        private void OnExperienceChanged(ExperienceChangedEvent evt)
        {
            SetLevel(evt.Level, evt.Xp, evt.XpToNext);
        }

        private void OnLoadoutChanged(LoadoutChangedEvent evt)
        {
            ApplySlots(_weaponIcons, _weaponLevels, evt.Weapons);
            ApplySlots(_passiveIcons, _passiveLevels, evt.Passives);

            if (weaponLabelText != null)
            {
                int count = evt.Weapons != null ? evt.Weapons.Length : 0;
                weaponLabelText.text = $"武器 {count}/6";
            }

            if (passiveLabelText != null)
            {
                int count = evt.Passives != null ? evt.Passives.Length : 0;
                passiveLabelText.text = $"被动 {count}/6";
            }
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            ShowToast(
                $"宝箱蝙蝠出现！击杀它掉落宝箱（{evt.ChestRewards} 项奖励）",
                new Color(1f, 0.82f, 0.35f, 1f));
        }

        private void OnChestOpened(ChestOpenedEvent evt)
        {
            ShowToast(
                $"宝箱：{evt.Summary}",
                new Color(0.92f, 0.95f, 0.7f, 1f));
        }

        private void OnVictory(BattleVictoryEvent evt)
        {
            HideToast();
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

        private void SetLevel(int level, int xp, int xpToNext)
        {
            if (levelText != null)
            {
                levelText.text = $"等级 {level}";
            }

            if (xpText != null)
            {
                xpText.text = $"{xp}/{Mathf.Max(1, xpToNext)}";
            }

            if (xpFill != null)
            {
                xpFill.fillAmount = xpToNext > 0 ? Mathf.Clamp01((float)xp / xpToNext) : 0f;
            }
        }

        private void ApplySlots(Image[] icons, Text[] levels, LoadoutSlotInfo[] slots)
        {
            if (icons == null)
            {
                return;
            }

            for (int i = 0; i < icons.Length; i++)
            {
                bool has = slots != null && i < slots.Length;
                if (icons[i] != null)
                {
                    icons[i].enabled = has && slots[i].Icon != null;
                    icons[i].sprite = has ? slots[i].Icon : null;
                }

                if (levels != null && i < levels.Length && levels[i] != null)
                {
                    levels[i].enabled = has;
                    levels[i].text = has ? slots[i].Level.ToString() : string.Empty;
                }
            }
        }

        private void SetLoadoutEmpty()
        {
            ApplySlots(_weaponIcons, _weaponLevels, null);
            ApplySlots(_passiveIcons, _passiveLevels, null);
            if (weaponLabelText != null)
            {
                weaponLabelText.text = "武器 0/6";
            }

            if (passiveLabelText != null)
            {
                passiveLabelText.text = "被动 0/6";
            }
        }

        private void BuildSlotOverlays()
        {
            Font font = HudFont.Get();
            _weaponIcons = new Image[weaponSlots != null ? weaponSlots.Length : 0];
            _passiveIcons = new Image[passiveSlots != null ? passiveSlots.Length : 0];
            _weaponLevels = new Text[_weaponIcons.Length];
            _passiveLevels = new Text[_passiveIcons.Length];

            for (int i = 0; i < _weaponIcons.Length; i++)
            {
                BuildSlotOverlay(weaponSlots[i], font, out _weaponIcons[i], out _weaponLevels[i]);
            }

            for (int i = 0; i < _passiveIcons.Length; i++)
            {
                BuildSlotOverlay(passiveSlots[i], font, out _passiveIcons[i], out _passiveLevels[i]);
            }
        }

        private static void BuildSlotOverlay(
            Image slot,
            Font font,
            out Image icon,
            out Text level)
        {
            icon = null;
            level = null;
            if (slot == null)
            {
                return;
            }

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(slot.transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);
            icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            var levelObject = new GameObject("Level", typeof(RectTransform), typeof(Text));
            var levelRect = (RectTransform)levelObject.transform;
            levelRect.SetParent(slot.transform, false);
            levelRect.anchorMin = new Vector2(1f, 0f);
            levelRect.anchorMax = new Vector2(1f, 0f);
            levelRect.pivot = new Vector2(1f, 0f);
            levelRect.anchoredPosition = new Vector2(-2f, 1f);
            levelRect.sizeDelta = new Vector2(24f, 16f);
            level = levelObject.GetComponent<Text>();
            level.font = font;
            level.fontSize = 12;
            level.fontStyle = FontStyle.Bold;
            level.alignment = TextAnchor.LowerRight;
            level.color = new Color(1f, 0.92f, 0.55f, 1f);
            level.raycastTarget = false;
            level.horizontalOverflow = HorizontalWrapMode.Overflow;
            level.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = levelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            level.enabled = false;
        }

        private void ShowToast(string message, Color color)
        {
            if (toastText == null)
            {
                return;
            }

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
            }

            _toastCoroutine = StartCoroutine(ToastRoutine(message, color));
        }

        private IEnumerator ToastRoutine(string message, Color color)
        {
            toastText.gameObject.SetActive(true);
            toastText.text = message;
            toastText.color = color;

            yield return new WaitForSecondsRealtime(2.8f);

            float elapsed = 0f;
            Color faded = color;
            while (elapsed < 0.35f)
            {
                elapsed += Time.unscaledDeltaTime;
                faded.a = 1f - Mathf.Clamp01(elapsed / 0.35f);
                toastText.color = faded;
                yield return null;
            }

            toastText.gameObject.SetActive(false);
            _toastCoroutine = null;
        }

        private void HideToast()
        {
            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }

            if (toastText != null)
            {
                toastText.gameObject.SetActive(false);
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
