using System;
using System.Threading;
using RFramework;
using UnityRFramework.Runtime;
using Game.Config;

/// <summary>
/// 发射流程状态。初始化框架基础设置，加载大厅场景后切入菜单流程。
/// 状态的同步生命周期（OnEnter/OnLeave）只负责启动/取消内部异步任务，
/// 真正的初始化与场景加载在 fire-and-forget 任务中完成，不阻塞主线程。
/// </summary>
public class DemoLaunchProcedure : ProcedureStateBase
{
    /// <summary>
    /// 启动链路的取消源。OnLeave 时取消，确保离场后旧任务不再切换状态或访问已销毁对象。
    /// </summary>
    private CancellationTokenSource launchCts;

    /// <summary>
    /// 启动链路版本号。每次进入或重试自增；任务在关键 await 后比对，
    /// 过期结果直接忽略，避免旧链路污染新状态。
    /// </summary>
    private int launchVersion;

    /// <summary>
    /// 同步进入状态：启动启动链路（fire-and-forget），立即返回。
    /// 真正的初始化与场景加载在内部异步任务中完成。
    /// </summary>
    public override void OnEnter()
    {
        launchCts = new CancellationTokenSource();
        int version = ++launchVersion;
        _ = RunLaunchAsync(version, launchCts.Token);
    }

    /// <summary>
    /// 同步离开：取消并释放启动链路，不等待任务。
    /// </summary>
    public override void OnLeave(bool isShutdown)
    {
        launchCts?.Cancel();
        launchCts?.Dispose();
        launchCts = null;
    }

    /// <summary>
    /// 启动序列本体：设帧率、初始化资源、预热数据层、加载大厅场景，
    /// 全部完成后切到菜单。关键 await 后检查取消与版本，忽略过期结果。
    /// </summary>
    private async System.Threading.Tasks.Task RunLaunchAsync(int version, CancellationToken ct)
    {
        try
        {
            GameEntry.Base.FrameRate = 60;
            GameEntry.Base.GameSpeed = 1f;

            Log.Info("[Demo] Launch: initializing resource module...");
            await GameEntry.Resource.InitializeAsync();
            Log.Info("[Demo] Launch: resource module ready. Starting adventure guild.");

            // 数据层预热：加载全部配置表 + 本地化语言包 + 拉取公告（WebRequest 演示）
            await DemoDataLoader.LoadAllAsync(ct);
            ct.ThrowIfCancellationRequested();
            DemoGameState.ResetSession(GameEntry.Config.GetAllConfigs<Demo_CharacterConfig>());
            // LoadLanguageAsync 只缓存语言包；大厅创建前必须完成切换，保证首帧读取到正确语言。
            await GameEntry.Localization.SwitchLanguageAsync("zh-CN");
            ct.ThrowIfCancellationRequested();

            // 加载大厅场景（单场景模式替换当前启动场景）
            await LoadHallSceneAsync();

            if (ct.IsCancellationRequested || version != launchVersion)
            {
                return;
            }

            DemoErrorPanel.Hide();
            GameEntry.Procedure.ChangeProcedure<DemoMenuProcedure>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 状态已离开或框架关闭，正常结束。
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested || version != launchVersion)
            {
                return;
            }

            Log.Error("[Demo] Launch failed: {0}", ex);
            DemoErrorPanel.Show(ex, RetryLaunch);
        }
    }

    /// <summary>
    /// 重试入口：由错误屏按钮触发，重新执行启动序列。
    /// 仅当本状态仍有效（版本号匹配且未被取消）时才会真正切到菜单。
    /// </summary>
    private void RetryLaunch()
    {
        DemoErrorPanel.Hide();
        int version = ++launchVersion;
        _ = RunLaunchAsync(version, launchCts.Token);
    }

    /// <summary>
    /// 异步加载大厅场景。
    /// </summary>
    private async System.Threading.Tasks.Task LoadHallSceneAsync()
    {
        Log.Info("[Demo] Scene: loading DemoHall...");
        await GameEntry.Scene.LoadSceneAsync("DemoHall");
        Log.Info("[Demo] Scene: DemoHall loaded.");
    }
}
