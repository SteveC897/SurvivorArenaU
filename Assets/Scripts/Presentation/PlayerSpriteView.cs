using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 主角的帧动画（Presentation 层）。
    /// 只读取 Gameplay.PlayerMotor 的 IsMoving / MoveDirection，不参与移动或输入；
    /// 在 LateUpdate 执行，确保读到的是本帧已经移动后的状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpriteView : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerMotor motor;

        [Header("动画")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField, Min(0.1f)] private float idleFps = 4f;
        [SerializeField, Min(0.1f)] private float walkFps = 8f;

        private float _timer;
        private int _frameIndex;
        private bool _walking;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                Debug.LogError($"{nameof(PlayerSpriteView)} 缺少 SpriteRenderer。", this);
                enabled = false;
            }

            if (motor == null)
            {
                Debug.LogError($"{nameof(PlayerSpriteView)} 缺少 PlayerMotor 引用。", this);
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null || motor == null)
            {
                return;
            }

            bool walking = motor.IsMoving;
            if (walking != _walking)
            {
                _walking = walking;
                _frameIndex = 0;
                _timer = 0f;
            }

            Sprite[] frames = walking ? walkFrames : idleFrames;
            float fps = walking ? walkFps : idleFps;

            if (frames != null && frames.Length > 0 && fps > 0f)
            {
                _timer += Time.deltaTime;
                float frameDuration = 1f / fps;
                while (_timer >= frameDuration)
                {
                    _timer -= frameDuration;
                    _frameIndex = (_frameIndex + 1) % frames.Length;
                }

                spriteRenderer.sprite = frames[_frameIndex];
            }

            Vector2 moveDirection = motor.MoveDirection;
            if (Mathf.Abs(moveDirection.x) > 0.05f)
            {
                spriteRenderer.flipX = moveDirection.x < 0f;
            }

            spriteRenderer.sortingOrder = YSort.OrderForY(transform.position.y);
        }

        private void OnValidate()
        {
            idleFps = Mathf.Max(0.1f, idleFps);
            walkFps = Mathf.Max(0.1f, walkFps);
        }
    }
}
