using SurvivorArena.Core;
using UnityEngine;

namespace SurvivorArena.Platform
{
    /// <summary>
    /// 旧 Input Manager 的输入实现（Platform 层）。
    /// Gameplay 只看到 Core.IPlayerInput，不关心底层是旧 Input 还是新 Input System；
    /// 以后迁移到 Input System 时只需要换这个类，不用动 PlayerMotor。
    /// </summary>
    public sealed class LegacyPlayerInput : IPlayerInput
    {
        private readonly KeyCode _restartKey;

        public LegacyPlayerInput(KeyCode restartKey = KeyCode.R)
        {
            _restartKey = restartKey;
        }

        public Vector2 Move { get; private set; }

        public bool RestartPressed { get; private set; }

        public void Poll()
        {
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (Move.sqrMagnitude > 1f)
            {
                Move.Normalize();
            }

            RestartPressed = Input.GetKeyDown(_restartKey);
        }
    }
}
