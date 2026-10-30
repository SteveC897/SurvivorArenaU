using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 击杀光环的纯表现（Presentation 层）。
    /// 在主角脚下生成一张运行时径向渐变贴图，做“淡填充 + 亮边 + 呼吸”的效果；
    /// 真正的击杀判定在 Gameplay.KillAuraSystem，这里不碰敌人。
    ///
    /// 贴图完全由代码生成，不新增美术资源；半径由组合根从 BattleConfig 注入，
    /// 保证视觉圆和判定半径来自同一个数值。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuraView : MonoBehaviour
    {
        private const int TextureSize = 128;
        private const float SpriteRadiusUnits = 2f;

        [Header("表现")]
        [SerializeField] private Color auraColor = new Color(1f, 0.87f, 0.42f, 1f);
        [SerializeField, Range(0f, 1f)] private float baseAlpha = 0.85f;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.4f;
        [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.12f;

        [Header("初始半径（运行时会被 BattleConfig 覆盖）")]
        [SerializeField, Min(0.1f)] private float radius = 2f;

        private SpriteRenderer _auraRenderer;
        private Sprite _auraSprite;
        private Texture2D _auraTexture;
        private float _radius = 2f;

        private void Awake()
        {
            _radius = Mathf.Max(0.1f, radius);
            if (_auraRenderer == null)
            {
                BuildVisual();
            }
        }

        /// <summary>由组合根在 Start 注入实际判定半径。</summary>
        public void Initialize(float auraRadius)
        {
            _radius = Mathf.Max(0.1f, auraRadius);
            if (_auraRenderer == null)
            {
                BuildVisual();
            }
            else
            {
                ApplyRadius();
            }
        }

        private void LateUpdate()
        {
            if (_auraRenderer == null)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            Color color = auraColor;
            color.a = Mathf.Clamp01(baseAlpha * pulse);
            _auraRenderer.color = color;
        }

        private void BuildVisual()
        {
            var visualObject = new GameObject("AuraVisual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = Vector3.zero;

            _auraRenderer = visualObject.AddComponent<SpriteRenderer>();
            _auraTexture = CreateAuraTexture();
            _auraSprite = Sprite.Create(
                _auraTexture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                32f,
                0,
                SpriteMeshType.FullRect);

            _auraRenderer.sprite = _auraSprite;
            _auraRenderer.color = auraColor;
            _auraRenderer.sortingOrder = YSort.AuraOrder;

            SpriteRenderer playerRenderer = GetComponent<SpriteRenderer>();
            if (playerRenderer != null)
            {
                _auraRenderer.sharedMaterial = playerRenderer.sharedMaterial;
            }

            ApplyRadius();
        }

        private void ApplyRadius()
        {
            if (_auraRenderer != null)
            {
                _auraRenderer.transform.localScale = Vector3.one * (_radius / SpriteRadiusUnits);
            }
        }

        private static Texture2D CreateAuraTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "KillAura_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[TextureSize * TextureSize];
            const float half = TextureSize * 0.5f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 0f;
                    if (distance < 1f)
                    {
                        // 内部很淡的填充，叠加一圈清晰亮边，保证在草地上可读。
                        float fill = Mathf.Lerp(0.03f, 0.07f, distance * distance);
                        float rimIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 0.955f, distance));
                        float rimOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.965f, 0.998f, distance));
                        alpha = fill + rimIn * rimOut * 0.62f;
                        alpha *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.985f, 1f, distance));
                    }

                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(
                        Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void OnDestroy()
        {
            if (_auraSprite != null)
            {
                Destroy(_auraSprite);
            }

            if (_auraTexture != null)
            {
                Destroy(_auraTexture);
            }
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.1f, radius);
            baseAlpha = Mathf.Clamp01(baseAlpha);
            pulseSpeed = Mathf.Max(0f, pulseSpeed);
            pulseAmount = Mathf.Clamp(pulseAmount, 0f, 0.5f);
        }
    }
}
