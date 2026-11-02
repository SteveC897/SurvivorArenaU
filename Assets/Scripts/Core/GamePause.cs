namespace SurvivorArena.Core
{
    /// <summary>
    /// 局内逻辑暂停状态（Core 层）。
    /// 升级三选一/宝箱界面打开时由组合根置位；EnemyBat 等自驱动 MonoBehaviour 读取它决定是否推进，
    /// 不使用 Time.timeScale，保证 UI 动画/输入仍按真实时间运行。
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        public static void Reset()
        {
            IsPaused = false;
        }
    }
}
