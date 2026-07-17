using UnityRFramework.Runtime;

/// <summary>
/// 大厅根窗口。挂载于手动拼好的 DemoHallUI 预制体
/// （含 TopBar / Body 三栏 / RightPanel 内 Notice + Expedition）。
/// 仅负责生命周期钩子：所有界面装配都在预制体内完成，不在代码里拼面板。
/// </summary>
public class DemoHallWindow : UIFormLogic
{
    protected override void OnInit(UIForm owner, object userData)
    {
        base.OnInit(owner, userData);
    }
}
