using System.Collections.Generic;
using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 局内世界表现（Presentation 层）：把 Gameplay 的纯数据快照画成 Sprite。
    /// 只读取 ProjectileSystem / AreaEffectSystem / OrbitEffectSystem / PickupSystem / ChestSystem，
    /// 不参与任何判定。所有 Sprite 走同一个池，避免每帧 new GameObject。
    /// </summary>
    public sealed class CombatPresenter
    {
        private sealed class View
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
        }

        private readonly ProjectileSystem _projectiles;
        private readonly AreaEffectSystem _areas;
        private readonly OrbitEffectSystem _orbits;
        private readonly PickupSystem _pickups;
        private readonly ChestSystem _chests;
        private readonly Sprite _gemGreen;
        private readonly Sprite _gemRed;
        private readonly Sprite _circle;
        private readonly Sprite _box;
        private readonly Sprite _chest;
        private readonly Material _material;
        private readonly Transform _root;
        private readonly List<View> _views = new List<View>();
        private int _used;

        public CombatPresenter(
            Transform parent,
            ProjectileSystem projectiles,
            AreaEffectSystem areas,
            OrbitEffectSystem orbits,
            PickupSystem pickups,
            ChestSystem chests,
            Sprite gemGreen,
            Sprite gemRed,
            Material spriteMaterial)
        {
            _projectiles = projectiles;
            _areas = areas;
            _orbits = orbits;
            _pickups = pickups;
            _chests = chests;
            _gemGreen = gemGreen;
            _gemRed = gemRed;
            _material = spriteMaterial;
            _circle = CreateCircleSprite();
            _box = CreateBoxSprite();
            _chest = CreateChestSprite();

            var rootObject = new GameObject("CombatView");
            rootObject.transform.SetParent(parent, false);
            _root = rootObject.transform;
        }

        /// <summary>由组合根在 LateUpdate 调用；此时本帧的玩法结果已经确定。</summary>
        public void Tick()
        {
            _used = 0;
            DrawProjectiles();
            DrawAreas();
            DrawOrbits();
            DrawGems();
            DrawChests();

            for (int i = _used; i < _views.Count; i++)
            {
                if (_views[i].GameObject.activeSelf)
                {
                    _views[i].GameObject.SetActive(false);
                }
            }
        }

        private void DrawProjectiles()
        {
            IReadOnlyList<Projectile> projectiles = _projectiles.Active;
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                Sprite sprite = projectile.Sprite != null ? projectile.Sprite : _box;
                Draw(
                    sprite,
                    projectile.Position,
                    projectile.Scale,
                    projectile.Rotation,
                    projectile.Color,
                    YSort.OrderForY(projectile.Position.y) + 30);
            }
        }

        private void DrawAreas()
        {
            IReadOnlyList<AreaEffect> areas = _areas.Active;
            for (int i = 0; i < areas.Count; i++)
            {
                AreaEffect area = areas[i];
                Sprite sprite = area.Sprite != null ? area.Sprite : (area.IsRectangle ? _box : _circle);
                Vector2 scale = area.IsRectangle
                    ? area.Size
                    : new Vector2(area.Radius * 2f, area.Radius * 2f);
                Draw(
                    sprite,
                    area.Position,
                    scale,
                    area.Angle,
                    area.Color,
                    YSort.GroundOrder + 200);
            }
        }

        private void DrawOrbits()
        {
            IReadOnlyList<OrbitEffect> orbits = _orbits.Active;
            for (int i = 0; i < orbits.Count; i++)
            {
                OrbitEffect orbit = orbits[i];
                if (orbit.Center == null)
                {
                    continue;
                }

                Vector2 center = orbit.Center.position;
                float step = Mathf.PI * 2f / Mathf.Max(1, orbit.Count);
                for (int n = 0; n < orbit.Count; n++)
                {
                    float angle = orbit.Angle * Mathf.Deg2Rad + step * n;
                    Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                        orbit.Radius;
                    Draw(
                        orbit.Sprite != null ? orbit.Sprite : _box,
                        position,
                        orbit.VisualScale,
                        angle * Mathf.Rad2Deg,
                        Color.white,
                        YSort.OrderForY(position.y) + 40);
                }
            }
        }

        private void DrawGems()
        {
            IReadOnlyList<XpGem> gems = _pickups.ActiveGems;
            for (int i = 0; i < gems.Count; i++)
            {
                XpGem gem = gems[i];
                Sprite sprite = gem.Value >= 10 ? _gemRed : _gemGreen;
                if (sprite == null)
                {
                    sprite = _gemGreen != null ? _gemGreen : _circle;
                }

                float bob = Mathf.Sin(Time.time * 5f + gem.BobPhase) * 0.06f;
                Draw(
                    sprite,
                    gem.Position + new Vector2(0f, bob),
                    0.55f,
                    0f,
                    Color.white,
                    YSort.OrderForY(gem.Position.y) - 20);
            }
        }

        private void DrawChests()
        {
            IReadOnlyList<Chest> chests = _chests.ActiveChests;
            for (int i = 0; i < chests.Count; i++)
            {
                Chest chest = chests[i];
                float bob = Mathf.Sin(Time.time * 3f + chest.BobPhase) * 0.08f;
                Draw(
                    _chest,
                    chest.Position + new Vector2(0f, 0.15f + bob),
                    0.9f,
                    0f,
                    new Color(1f, 0.88f, 0.45f, 1f),
                    YSort.OrderForY(chest.Position.y) - 10);
            }
        }

        private void Draw(
            Sprite sprite,
            Vector2 position,
            float scale,
            float rotation,
            Color color,
            int sortingOrder)
        {
            Draw(sprite, position, new Vector2(scale, scale), rotation, color, sortingOrder);
        }

        private void Draw(
            Sprite sprite,
            Vector2 position,
            Vector2 scale,
            float rotation,
            Color color,
            int sortingOrder)
        {
            View view = GetView();
            view.GameObject.SetActive(true);
            view.GameObject.transform.position = new Vector3(position.x, position.y, 0f);
            view.GameObject.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            view.GameObject.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            view.Renderer.sprite = sprite;
            view.Renderer.color = color;
            view.Renderer.sortingOrder = sortingOrder;
        }

        private View GetView()
        {
            if (_used < _views.Count)
            {
                return _views[_used++];
            }

            var gameObject = new GameObject("WorldSprite");
            gameObject.transform.SetParent(_root, false);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = _material;
            var view = new View { GameObject = gameObject, Renderer = renderer };
            _views.Add(view);
            _used++;
            return view;
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CombatCircle_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Sprite CreateBoxSprite()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "CombatBox_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }

        private static Sprite CreateChestSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Chest_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 color = new Color32(92, 48, 24, 255);
                    if (y >= 11)
                    {
                        color = new Color32(230, 170, 60, 255);
                    }
                    else if (y <= 2)
                    {
                        color = new Color32(40, 24, 16, 255);
                    }

                    if (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                    {
                        color = new Color32(26, 16, 10, 255);
                    }

                    if (x >= 7 && x <= 8 && y >= 5 && y <= 10)
                    {
                        color = new Color32(255, 220, 90, 255);
                    }

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
