using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;

/// <summary>
/// 返回大厅流程。负责等待大厅场景加载完成后再进入菜单流程。
/// </summary>
public sealed class DemoReturnProcedure : ProcedureStateBase
{
    private CancellationTokenSource returnCts;
    private int returnVersion;

    public override void OnEnter()
    {
        returnCts = new CancellationTokenSource();
        int version = ++returnVersion;
        _ = ReturnAsync(version, returnCts.Token);
    }

    public override void OnLeave(bool isShutdown)
    {
        returnCts?.Cancel();
        returnCts?.Dispose();
        returnCts = null;
    }

    private async Task ReturnAsync(int version, CancellationToken ct)
    {
        try
        {
            Log.Info("[Demo] Return: loading hall scene...");
            await GameEntry.Scene.LoadSceneAsync("DemoHall", ct: ct);
            if (ct.IsCancellationRequested || version != returnVersion)
            {
                return;
            }

            GameEntry.Procedure.ChangeProcedure<DemoMenuProcedure>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested && version == returnVersion)
            {
                Log.Error("[Demo] Return: load hall failed: {0}", ex);
            }
        }
    }
}
