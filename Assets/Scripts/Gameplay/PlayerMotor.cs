using SurvivorArena.Core;
using SurvivorArena.Core.Stats;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 主角移动（Gameplay 层）。
    /// 不直接读 Input：只依赖 Core.IPlayerInput，由 Platform 层实现、组合根注入；
    /// 也不直接做动画：只暴露 IsMoving / MoveDirection 给 Presentation 的 PlayerSpriteView。
    /// 移动速度来自 Core.Stats.Stat：所有加成必须通过 Stat.AddModifier 注册，
    /// 这里只读 Value，不缓存、不自己算倍率。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private IPlayerInput _input;
        private Stat _moveSpeed;
        private bool _controlEnabled = true;

        public bool ControlEnabled => _controlEnabled;

        public bool IsMoving { get; private set; }

        public Vector2 MoveDirection { get; private set; }

        /// <summary>由组合根在 Start 里注入输入与 MoveSpeed 属性。</summary>
        public void Initialize(IPlayerInput input, Stat moveSpeed)
        {
            _input = input;
            _moveSpeed = moveSpeed;

            if (_input == null)
            {
                Debug.LogError($"{nameof(PlayerMotor)} 缺少 {nameof(IPlayerInput)}。", this);
            }

            if (_moveSpeed == null)
            {
                Debug.LogError($"{nameof(PlayerMotor)} 缺少 MoveSpeed Stat。", this);
            }
        }

        public void SetControlEnabled(bool controlEnabled)
        {
            _controlEnabled = controlEnabled;
            if (!controlEnabled)
            {
                IsMoving = false;
                MoveDirection = Vector2.zero;
            }
        }

        /// <summary>由组合根每帧调用，保证与 GameClock 同一节拍。</summary>
        public void Tick(float deltaTime)
        {
            if (_input == null || _moveSpeed == null || !_controlEnabled || deltaTime <= 0f)
            {
                IsMoving = false;
                MoveDirection = Vector2.zero;
                return;
            }

            float speed = _moveSpeed.Value;
            if (speed <= 0f)
            {
                IsMoving = false;
                MoveDirection = Vector2.zero;
                return;
            }

            Vector2 move = _input.Move;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            MoveDirection = move;
            IsMoving = move.sqrMagnitude > 0.001f;

            if (IsMoving)
            {
                transform.position += (Vector3)(move * (speed * deltaTime));
            }
        }
    }
}
