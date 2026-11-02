using System.Collections.Generic;
using SurvivorArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 升级三选一界面（Presentation 层）。
    /// 订阅 LevelUpOfferedEvent / LevelUpClosedEvent，生成 3 张卡片；
    /// 点击卡片或按 1/2/3 通过 EventBus 回传 LevelUpChoiceEvent。
    /// 游戏暂停由 Gameplay.UpgradeSystem.IsChoosing 驱动，不修改 Time.timeScale。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelUpView : MonoBehaviour
    {
        private sealed class Card
        {
            public GameObject Root;
            public Image Icon;
            public Text Name;
            public Text Level;
            public Text Description;
            public Button Button;
        }

        [SerializeField] private Sprite panelSprite;

        private readonly List<Card> _cards = new List<Card>();
        private UpgradeOptionInfo[] _options;
        private GameObject _overlay;
        private Text _title;
        private Text _subtitle;
        private bool _locked;

        private void Awake()
        {
            BuildUi();
            Hide();
        }

        private void OnEnable()
        {
            Unsubscribe();
            EventBus<LevelUpOfferedEvent>.Subscribe(OnOffered);
            EventBus<LevelUpClosedEvent>.Subscribe(OnClosed);
            EventBus<BattleVictoryEvent>.Subscribe(OnVictory);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            EventBus<LevelUpOfferedEvent>.Unsubscribe(OnOffered);
            EventBus<LevelUpClosedEvent>.Unsubscribe(OnClosed);
            EventBus<BattleVictoryEvent>.Unsubscribe(OnVictory);
        }

        private void Update()
        {
            if (_overlay == null || !_overlay.activeSelf || _locked)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                Choose(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                Choose(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                Choose(2);
            }
        }

        private void OnOffered(LevelUpOfferedEvent evt)
        {
            _options = evt.Options;
            _locked = false;

            if (_overlay == null)
            {
                return;
            }

            _overlay.SetActive(true);
            _overlay.transform.SetAsLastSibling();
            _title.text = "升级！";
            _subtitle.text = $"等级 {evt.Level}　·　选择一张卡";

            int count = _options != null ? _options.Length : 0;
            for (int i = 0; i < _cards.Count; i++)
            {
                bool active = i < count;
                _cards[i].Root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                UpgradeOptionInfo option = _options[i];
                _cards[i].Icon.enabled = option.Icon != null;
                _cards[i].Icon.sprite = option.Icon;
                _cards[i].Name.text = option.DisplayName;
                _cards[i].Level.text = $"Lv {option.Level}/{option.MaxLevel}";
                _cards[i].Description.text = option.Description;
                _cards[i].Button.interactable = true;
            }
        }

        private void OnClosed(LevelUpClosedEvent evt)
        {
            Hide();
        }

        private void OnVictory(BattleVictoryEvent evt)
        {
            Hide();
        }

        private void Choose(int index)
        {
            if (_locked || _options == null || index < 0 || index >= _options.Length)
            {
                return;
            }

            _locked = true;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Button.interactable = false;
            }

            EventBus<LevelUpChoiceEvent>.Publish(new LevelUpChoiceEvent(index));
        }

        private void Hide()
        {
            if (_overlay != null)
            {
                _overlay.SetActive(false);
            }
        }

        private void BuildUi()
        {
            Font font = HudFont.Get();

            _overlay = CreateStretch("LevelUpOverlay", transform);
            AddImage(_overlay, null, new Color(0f, 0f, 0f, 0.76f));
            _overlay.transform.SetAsLastSibling();

            RectTransform titleRect = CreateUi(
                "Title", _overlay.transform, new Vector2(0.5f, 1f), new Vector2(0f, -100f),
                new Vector2(900f, 90f));
            _title = AddText(titleRect, "升级！", 64, TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.32f, 1f), font, 3f, FontStyle.Bold);

            RectTransform subtitleRect = CreateUi(
                "Subtitle", _overlay.transform, new Vector2(0.5f, 1f), new Vector2(0f, -175f),
                new Vector2(900f, 36f));
            _subtitle = AddText(subtitleRect, "等级 2 · 选择一张卡", 22, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.86f, 0.74f, 1f), font, 1f);

            for (int i = 0; i < 3; i++)
            {
                _cards.Add(BuildCard(i, font));
            }

            RectTransform hintRect = CreateUi(
                "Hint", _overlay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 54f),
                new Vector2(900f, 30f));
            AddText(hintRect, "点击卡片，或按 1 / 2 / 3 选择", 20, TextAnchor.MiddleCenter,
                new Color(0.78f, 0.82f, 0.76f, 0.8f), font, 1f);
        }

        private Card BuildCard(int index, Font font)
        {
            float x = (index - 1) * 390f;
            RectTransform cardRect = CreateUi(
                $"Card{index + 1}",
                _overlay.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(x, -20f),
                new Vector2(360f, 500f));

            var background = cardRect.gameObject.AddComponent<Image>();
            background.sprite = panelSprite;
            background.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = Color.white;

            var button = cardRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => Choose(index));

            RectTransform iconRect = CreateUi(
                "Icon", cardRect, new Vector2(0.5f, 1f), new Vector2(0f, -44f),
                new Vector2(140f, 140f));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            RectTransform nameRect = CreateUi(
                "Name", cardRect, new Vector2(0.5f, 1f), new Vector2(0f, -205f),
                new Vector2(320f, 42f));
            Text name = AddText(nameRect, "强能魔杖", 30, TextAnchor.MiddleCenter,
                new Color(1f, 0.9f, 0.6f, 1f), font, 2f, FontStyle.Bold);

            RectTransform levelRect = CreateUi(
                "Level", cardRect, new Vector2(0.5f, 1f), new Vector2(0f, -250f),
                new Vector2(300f, 30f));
            Text level = AddText(levelRect, "Lv 1/8", 20, TextAnchor.MiddleCenter,
                new Color(0.88f, 0.82f, 0.62f, 1f), font, 1f);

            RectTransform descRect = CreateUi(
                "Desc", cardRect, new Vector2(0.5f, 1f), new Vector2(0f, -345f),
                new Vector2(310f, 140f));
            Text description = AddText(descRect, "自动锁定最近敌人，发射魔法弹。", 19,
                TextAnchor.UpperCenter, new Color(0.9f, 0.9f, 0.86f, 1f), font, 1f);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;

            return new Card
            {
                Root = cardRect.gameObject,
                Icon = icon,
                Name = name,
                Level = level,
                Description = description,
                Button = button
            };
        }

        private static GameObject CreateStretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static RectTransform CreateUi(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void AddImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = true;

            if (outlineWidth > 0f)
            {
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);
                outline.useGraphicAlpha = true;
            }

            return text;
        }
    }
}
