using System;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 测试场景唯一的敌人：蝙蝠（Gameplay 层）。
    /// 行为只有直线追向主角，进入光环后由 KillAuraSystem 判定死亡。
    /// 实例由 EnemySpawner 通过 Core.ObjectPool 池化复用；
    /// 死亡时播放一小段胀大淡出后通过 Released 事件回到池中。
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

        private Transform _visual;
        private SpriteRenderer _renderer;
        private Transform _target;
        private State _state = State.Inactive;
        private float _speed;
        private float _bobPhase;
        private float _deathTime;

        /// <summary>被击杀时触发；Spawner 在这里移除活跃列表、计数并发布事件。</summary>
        public event Action<EnemyBat> Killed;

        /// <summary>死亡表现播放完毕、可以归还对象池时触发。</summary>
        public event Action<EnemyBat> Released;

        public bool IsAlive => _state == State.Alive;

        public Vector2 Position => transform.position;

        public float Radius => radius;

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
            float enemyRadius)
        {
            if (_renderer == null)
            {
                return;
            }

            transform.position = position;
            _target = target;
            _speed = speed;
            radius = Mathf.Max(0.01f, enemyRadius);
            _state = State.Alive;
            _deathTime = 0f;

            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }

            _renderer.color = Color.white;
            _renderer.flipX = false;
            _renderer.sortingOrder = YSort.OrderForY(position.y);
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one;
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
        /// 由 OffscreenRecycler 对跑出屏幕太远的活蝙蝠调用。
        /// </summary>
        public void Relocate(Vector2 position)
        {
            if (_state != State.Alive)
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

            _renderer.sortingOrder = YSort.OrderForY(position.y);
        }

        private void TickDying(float deltaTime)
        {
            _deathTime += deltaTime;
            float progress = Mathf.Clamp01(_deathTime / deathDuration);

            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one * (1f + 0.8f * progress);
            _visual.localRotation = Quaternion.Euler(0f, 0f, 35f * progress);

            Color color = Color.white;
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
        }
    }
}
