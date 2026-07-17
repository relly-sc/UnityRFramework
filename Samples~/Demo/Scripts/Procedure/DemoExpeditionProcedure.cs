using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;

/// <summary>
/// 远征流程状态。同步生命周期只负责启动或取消远征场景加载任务。
/// </summary>
public sealed class DemoExpeditionProcedure : ProcedureStateBase
{
    private CancellationTokenSource expeditionCts;
    private int expeditionVersion;

    public override void OnEnter()
    {
        expeditionCts = new CancellationTokenSource();
        int version = ++expeditionVersion;
        _ = EnterAsync(version, expeditionCts.Token);
    }

    public override void OnLeave(bool isShutdown)
    {
        expeditionCts?.Cancel();
        expeditionCts?.Dispose();
        expeditionCts = null;
    }

    private async Task EnterAsync(int version, CancellationToken ct)
    {
        try
        {
            Log.Info("[Demo] Expedition: loading expedition scene...");
            await GameEntry.Scene.LoadSceneAsync("DemoExpedition", ct: ct);
            if (ct.IsCancellationRequested || version != expeditionVersion)
            {
                return;
            }

            _ = TryPlayBgmAsync();
            Log.Info("[Demo] Expedition: scene and battle HUD ready.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested && version == expeditionVersion)
            {
                Log.Error("[Demo] Expedition: load failed: {0}", ex);
            }
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
                    "Audio/music_background.wav", 0.35f, true, 0.25f);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[Demo] Optional expedition BGM failed: {0}", ex.Message);
        }
    }
}
