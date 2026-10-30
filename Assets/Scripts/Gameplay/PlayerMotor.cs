using SurvivorArena.Core;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 主角移动（Gameplay 层）。
    /// 不直接读 Input：只依赖 Core.IPlayerInput，由 Platform 层实现、组合根注入；
    /// 也不直接做动画：只暴露 IsMoving / MoveDirection 给 Presentation 的 PlayerSpriteView。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private IPlayerInput _input;
        private float _moveSpeed = 4.2f;
        private bool _controlEnabled = true;

        public bool ControlEnabled => _controlEnabled;

        public bool IsMoving { get; private set; }

        public Vector2 MoveDirection { get; private set; }

        /// <summary>由组合根在 Start 里注入。</summary>
        public void Initialize(IPlayerInput input, float moveSpeed)
        {
            _input = input;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
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
            if (_input == null || !_controlEnabled || _moveSpeed <= 0f || deltaTime <= 0f)
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
                transform.position += (Vector3)(move * (_moveSpeed * deltaTime));
            }
        }
    }
}
