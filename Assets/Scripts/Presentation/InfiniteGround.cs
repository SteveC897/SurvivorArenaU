using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 用 3x3 块“平铺 Sprite”铺出无限草地（Presentation 层）。
    /// 相机移动越过 32 单位边界时整体重排，因为每块尺寸是 1 单位贴图的整数倍，
    /// 重排后图案在视觉上连续。不生成 Tilemap 资源，也不依赖外部拼接图。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfiniteGround : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Sprite groundSprite;
        [SerializeField] private Material spriteMaterial;

        [Header("平铺")]
        [SerializeField, Min(1)] private int chunkSize = 32;
        [SerializeField, Min(0)] private int radius = 1;
        [SerializeField] private Color tint = new Color(0.86f, 0.92f, 0.86f, 1f);

        private Transform[] _chunks;
        private Camera _camera;
        private int _lastBaseX = int.MinValue;
        private int _lastBaseY = int.MinValue;

        private void Awake()
        {
            if (groundSprite == null)
            {
                Debug.LogError($"{nameof(InfiniteGround)} 缺少草地 Sprite。", this);
                enabled = false;
                return;
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError($"{nameof(InfiniteGround)} 找不到主相机。", this);
                enabled = false;
                return;
            }

            int side = radius * 2 + 1;
            _chunks = new Transform[side * side];

            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    var chunkObject = new GameObject($"Ground_{x - radius}_{z - radius}");
                    chunkObject.transform.SetParent(transform, false);

                    var renderer = chunkObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = groundSprite;
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    renderer.tileMode = SpriteTileMode.Continuous;
                    renderer.size = new Vector2(chunkSize, chunkSize);
                    renderer.sortingOrder = YSort.GroundOrder;
                    renderer.color = tint;
                    if (spriteMaterial != null)
                    {
                        renderer.sharedMaterial = spriteMaterial;
                    }

                    _chunks[z * side + x] = chunkObject.transform;
                }
            }

            Reposition(0, 0);
        }

        private void LateUpdate()
        {
            if (_camera == null || _chunks == null)
            {
                return;
            }

            Vector3 position = _camera.transform.position;
            int baseX = Mathf.FloorToInt(position.x / chunkSize);
            int baseY = Mathf.FloorToInt(position.y / chunkSize);

            if (baseX != _lastBaseX || baseY != _lastBaseY)
            {
                Reposition(baseX, baseY);
            }
        }

        private void Reposition(int baseX, int baseY)
        {
            _lastBaseX = baseX;
            _lastBaseY = baseY;

            int side = radius * 2 + 1;
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    int gridX = baseX + x - radius;
                    int gridY = baseY + z - radius;
                    _chunks[z * side + x].position = new Vector3(
                        gridX * (float)chunkSize,
                        gridY * (float)chunkSize,
                        0f);
                }
            }
        }

        private void OnValidate()
        {
            chunkSize = Mathf.Max(1, chunkSize);
            radius = Mathf.Max(0, radius);
        }
    }
}
