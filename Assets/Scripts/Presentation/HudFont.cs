using UnityEngine;

namespace SurvivorArena.Presentation
{
    /// <summary>
    /// 局内 HUD 的字体（Presentation 层）。
    /// 项目还没有 TMP 中文字体资源，测试场景直接取系统中文字体，
    /// 保证“胜利 / 波次 / 击杀”等中文能正常显示；换字体只需改这里。
    /// </summary>
    public static class HudFont
    {
        private static Font _cached;

        private static readonly string[] Candidates =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "PingFang SC",
            "Arial Unicode MS",
            "Arial"
        };

        public static Font Get()
        {
            if (_cached != null)
            {
                return _cached;
            }

            try
            {
                _cached = Font.CreateDynamicFontFromOSFont(Candidates, 48);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"创建系统中文字体失败，回退到内置字体：{exception.Message}");
            }

            if (_cached == null)
            {
                _cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _cached;
        }
    }
}
