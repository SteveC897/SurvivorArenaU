using SurvivorArena.Core;
using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// Core.IWorldViewport 的相机实现（Presentation 层）。
    /// 把 Camera 的正交尺寸/宽高比翻译成“半宽/半高”，供 Gameplay 的离屏回收使用；
    /// 这样 Gameplay 不需要引用 UnityEngine.Camera 或 Presentation 类型。
    /// </summary>
    public sealed class CameraViewport : IWorldViewport
    {
        private readonly Camera _camera;

        public CameraViewport(Camera camera)
        {
            _camera = camera;
        }

        public Vector2 Center => _camera != null ? (Vector2)_camera.transform.position : Vector2.zero;

        public bool TryGetHalfExtents(out float halfWidth, out float halfHeight)
        {
            halfWidth = 0f;
            halfHeight = 0f;

            if (_camera == null || !_camera.orthographic)
            {
                return false;
            }

            float aspect = _camera.aspect;
            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                aspect = 16f / 9f;
            }

            halfHeight = Mathf.Max(0.1f, _camera.orthographicSize);
            halfWidth = halfHeight * aspect;
            return true;
        }
    }
}
