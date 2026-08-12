namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 层级预设常量。数值越大越靠前。
    /// 参考 UniWindow 的 WindowLayer 设计，以 100 为步进便于插入自定义层级。
    /// </summary>
    public static class UILayer
    {
        /// <summary>最底层（背景、场景 UI）</summary>
        public const int Bottom = 0;

        /// <summary>HUD 层（血条、小地图、状态栏）</summary>
        public const int HUD = 100;

        /// <summary>普通面板层（背包、角色、技能）</summary>
        public const int Panel = 200;

        /// <summary>弹窗层（确认框、提示、详情）</summary>
        public const int Popup = 300;

        /// <summary>系统层（Loading、全屏遮罩、GM）</summary>
        public const int System = 400;

        /// <summary>最顶层（Toast、Tooltip、光标提示）</summary>
        public const int Top = 500;
    }
}
