using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine;

/// <summary>
/// 菜单流程状态。大厅场景加载完成后进入，负责实例化大厅 UI 预制体。
/// </summary>
public class DemoMenuProcedure : ProcedureStateBase
{
    private const string HallUIPrefabPath = "Prefabs/UI/DemoHallUI";

    /// <summary>
    /// 大厅 UI 打开链路的取消源。OnLeave 时取消，避免状态切换后 UI 仍完成实例化。
    /// </summary>
    private CancellationTokenSource menuCts;

    /// <summary>
    /// 大厅 UI 打开链路版本号。每次进入自增；任务在关键 await 后比对，忽略过期结果。
    /// </summary>
    private int menuVersion;

    /// <summary>
    /// 同步进入状态：触发大厅 UI 预制体的异步加载（fire-and-forget），立即返回等待玩家操作。
    /// </summary>
    public override void OnEnter()
    {
        Log.Info("[Demo] Menu: hall ready. waiting for player action...");
        _ = TryPlayBgmAsync();

        if (!GameEntry.UI.HasUIForm(HallUIPrefabPath))
        {
            menuCts = new CancellationTokenSource();
            int version = ++menuVersion;
            _ = OpenHallUIAsync(version, menuCts.Token);
        }
    }

    private static async Task TryPlayBgmAsync()
    {
        try
        {
            IAudioModule audio = RFrameworkModuleEntry.GetModule<IAudioModule>();
            if (audio != null)
            {
                await audio.PlayBgmAsync(
                    "Audio/music_background.wav", 0.25f, true, 0.25f);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[Demo] Optional hall BGM failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 异步打开大厅 UI 预制体。失败仅记录日志，不阻塞流程。
    /// 关键 await 后检查取消与版本，忽略过期结果，避免状态已切换后重新打开大厅界面。
    /// </summary>
    private async Task OpenHallUIAsync(int version, CancellationToken ct)
    {
        try
        {
            IUIForm uiForm = await GameEntry.UI.OpenUIFormAsync(HallUIPrefabPath, 100, ct: ct);
            if (ct.IsCancellationRequested || version != menuVersion)
            {
                // 已取消或状态已离开：OpenUIFormAsync 已实例化 UI，必须显式关闭，
                // 否则离开 Menu 后大厅界面会残留
                GameEntry.UI.CloseUIForm(HallUIPrefabPath);
                return;
            }

            if (uiForm == null || uiForm.Handle == null)
            {
                Log.Error("[Demo] Menu: failed to load hall UI prefab '{0}'.", HallUIPrefabPath);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 状态已离开或框架关闭，正常结束。
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested || version != menuVersion)
            {
                return;
            }

            Log.Error("[Demo] Menu: open hall UI failed: {0}", ex);
        }
    }

    /// <summary>
    /// 同步离开：取消并释放大厅 UI 打开链路，避免状态切换后 UI 仍完成实例化。
    /// </summary>
    public override void OnLeave(bool isShutdown)
    {
        menuCts?.Cancel();
        menuCts?.Dispose();
        menuCts = null;

        // UI 模块挂在常驻框架根节点下，切换 Single 场景不会自动销毁大厅窗口。
        // 因此流程离开菜单时必须主动关闭，避免大厅 UI 覆盖远征场景。
        if (GameEntry.UI != null && GameEntry.UI.HasUIForm(HallUIPrefabPath))
        {
            GameEntry.UI.CloseUIForm(HallUIPrefabPath);
        }
    }
}
