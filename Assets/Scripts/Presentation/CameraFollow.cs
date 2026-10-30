using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>主角跟随相机（Presentation 层）。像素对齐交给 PixelPerfectCamera，这里只做平滑跟随。</summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        private Vector3 _velocity;
        private bool _initialized;

        private void Awake()
        {
            if (target == null)
            {
                Debug.LogError($"{nameof(CameraFollow)} 缺少跟随目标。", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 current = transform.position;
            var goal = new Vector3(target.position.x, target.position.y, current.z);

            if (!_initialized)
            {
                _initialized = true;
                transform.position = goal;
                return;
            }

            transform.position = Vector3.SmoothDamp(current, goal, ref _velocity, smoothTime, Mathf.Infinity, Time.deltaTime);
        }

        private void OnValidate()
        {
            smoothTime = Mathf.Max(0f, smoothTime);
        }
    }
}
