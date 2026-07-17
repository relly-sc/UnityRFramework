using UnityRFramework.Runtime;

/// <summary>
/// Demo 启动入口。挂在 DemoBoot 场景的独立 GameObject 上，
/// 负责注册流程状态并启动发射流程。
/// 本脚本不依赖 .asmdef，随宿主编入 Assembly-CSharp。
/// </summary>
public class DemoGameEntry : UnityEngine.MonoBehaviour
{
    /// <summary>
    /// 生命周期：唤醒。初始化 Procedure 模块并启动 Launch 状态。
    /// </summary>
    private void Start()
    {
        // 验证框架已就绪（场景中应放置了 UnityRFramework prefab）
        if (GameEntry.Base == null)
        {
            Log.Error("[Demo] UnityRFramework prefab not found in scene. "
                + "Please add the UnityRFramework prefab to the DemoBoot scene.");
            return;
        }

        // 自动注册 Demo 所在程序集中的全部 Procedure 状态
        GameEntry.Procedure.InitializeFromAssembly<DemoLaunchProcedure>();

        // 同步启动发射流程（OnEnter 内部 fire-and-forget 启动异步初始化序列）
        GameEntry.Procedure.StartProcedure<DemoLaunchProcedure>();
    }
}
