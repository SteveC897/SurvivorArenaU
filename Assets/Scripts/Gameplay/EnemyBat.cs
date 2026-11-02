using System;
using SurvivorArena.Core;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 蝙蝠敌人（Gameplay 层）。
    /// 直线追向主角；现在有生命值，由武器造成的伤害扣血，血量归零才死亡。
    /// 死亡时播放一小段胀大淡出后通过 Released 事件回到池中。
    /// 金色大蝙蝠作为宝箱携带者：血量更高、体型更大，死亡时由 EnemySpawner 发 IsBoss 事件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyBat : MonoBehaviour
    {
        private enum State
        {
            Inactive,
            Alive,
            Dying
        }

        [Header("碰撞与表现")]
        [SerializeField, Min(0.01f)] private float radius = 0.4f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.04f;
        [SerializeField, Min(0f)] private float bobSpeed = 9f;
        [SerializeField, Min(0.01f)] private float deathDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.08f;

        private Transform _visual;
        private SpriteRenderer _renderer;
        private Transform _target;
        private State _state = State.Inactive;
        private float _speed;
        private float _bobPhase;
        private float _deathTime;
        private float _maxHealth;
        private float _health;
        private int _xpValue;
        private bool _isBoss;
        private float _visualScale = 1f;
        private float _hitFlash;
        private Color _baseColor = Color.white;

        /// <summary>被击杀时触发；Spawner 在这里移除活跃列表、计数并发布事件。</summary>
        public event Action<EnemyBat> Killed;

        /// <summary>死亡表现播放完毕、可以归还对象池时触发。</summary>
        public event Action<EnemyBat> Released;

        public bool IsAlive => _state == State.Alive;

        public Vector2 Position => transform.position;

        public float Radius => radius;

        public float Health => _health;

        public float MaxHealth => _maxHealth;

        public int XpValue => _xpValue;

        public bool IsBoss => _isBoss;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_renderer == null)
            {
                Debug.LogError($"{nameof(EnemyBat)} 需要子物体上的 SpriteRenderer。", this);
                enabled = false;
                return;
            }

            _visual = _renderer.transform;
            _bobPhase = UnityEngine.Random.value * Mathf.PI * 2f;
        }

        /// <summary>由 EnemySpawner 在从池中取出后调用。</summary>
        public void Spawn(
            Vector2 position,
            Sprite sprite,
            float speed,
            Transform target,
            float enemyRadius,
            float maxHealth,
            int xpValue,
            bool isBoss,
            float visualScale)
        {
            if (_renderer == null)
            {
                return;
            }

            transform.position = position;
            _target = target;
            _speed = speed;
            radius = Mathf.Max(0.01f, enemyRadius);
            _maxHealth = Mathf.Max(1f, maxHealth);
            _health = _maxHealth;
            _xpValue = Mathf.Max(0, xpValue);
            _isBoss = isBoss;
            _visualScale = Mathf.Max(0.1f, visualScale);
            _baseColor = isBoss ? new Color(1f, 0.82f, 0.35f, 1f) : Color.white;
            _state = State.Alive;
            _deathTime = 0f;
            _hitFlash = 0f;

            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }

            _renderer.color = _baseColor;
            _renderer.flipX = false;
            _renderer.sortingOrder = YSort.OrderForY(position.y);
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one * _visualScale;
        }

        /// <summary>武器伤害入口。血量归零后走 Kill 的死亡事件。</summary>
        public void ApplyDamage(float damage)
        {
            if (_state != State.Alive || damage <= 0f)
            {
                return;
            }

            _health -= damage;
            _hitFlash = hitFlashDuration;

            if (_health <= 0f)
            {
                _health = 0f;
                Kill();
            }
        }

        /// <summary>立即判定死亡。重复调用安全。</summary>
        public void Kill()
        {
            if (_state != State.Alive)
            {
                return;
            }

            _state = State.Dying;
            _deathTime = 0f;
            Killed?.Invoke(this);
        }

        /// <summary>
        /// 离屏回收：只移动位置，不改变存活状态，不触发击杀/死亡表现。
        /// 由 OffscreenRecycler 对跑出屏幕太远的活蝙蝠调用；Boss 不参与回收。
        /// </summary>
        public void Relocate(Vector2 position)
        {
            if (_state != State.Alive || _isBoss)
            {
                return;
            }

            transform.position = position;

            if (_visual != null)
            {
                _visual.localPosition = Vector3.zero;
            }

            if (_renderer != null)
            {
                _renderer.sortingOrder = YSort.OrderForY(position.y);
            }
        }

        private void Update()
        {
            if (GamePause.IsPaused)
            {
                return;
            }

            switch (_state)
            {
                case State.Alive:
                    TickAlive(Time.deltaTime);
                    break;
                case State.Dying:
                    TickDying(Time.deltaTime);
                    break;
            }
        }

        private void TickAlive(float deltaTime)
        {
            Vector2 position = transform.position;

            if (_target != null)
            {
                Vector2 toTarget = (Vector2)_target.position - position;
                float distance = toTarget.magnitude;
                if (distance > 0.0001f)
                {
                    Vector2 direction = toTarget / distance;
                    position += direction * (_speed * deltaTime);
                    transform.position = position;

                    if (Mathf.Abs(direction.x) > 0.05f)
                    {
                        _renderer.flipX = direction.x < 0f;
                    }
                }
            }

            if (bobAmplitude > 0f)
            {
                float bob = Mathf.Sin(Time.time * bobSpeed + _bobPhase) * bobAmplitude;
                _visual.localPosition = new Vector3(0f, bob, 0f);
            }

            if (_hitFlash > 0f)
            {
                _hitFlash -= deltaTime;
                float t = Mathf.Clamp01(_hitFlash / hitFlashDuration);
                _renderer.color = Color.Lerp(_baseColor, Color.white, t);
            }
            else
            {
                _renderer.color = _baseColor;
            }

            _renderer.sortingOrder = YSort.OrderForY(position.y);
        }

        private void TickDying(float deltaTime)
        {
            _deathTime += deltaTime;
            float progress = Mathf.Clamp01(_deathTime / deathDuration);

            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one * (_visualScale * (1f + 0.8f * progress));
            _visual.localRotation = Quaternion.Euler(0f, 0f, 35f * progress);

            Color color = _baseColor;
            color.a = 1f - progress;
            _renderer.color = color;

            if (progress >= 1f)
            {
                _state = State.Inactive;
                Released?.Invoke(this);
            }
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            bobAmplitude = Mathf.Max(0f, bobAmplitude);
            bobSpeed = Mathf.Max(0f, bobSpeed);
            deathDuration = Mathf.Max(0.01f, deathDuration);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        }
    }
}
