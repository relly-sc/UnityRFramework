using System.Threading;
using System.Threading.Tasks;
using Game.Config;
using UnityRFramework.Runtime;

/// <summary>
/// Demo 配置数据加载器。集中加载全部配置表，供大厅/远征/结算共用。
/// 必须在 Resource 模块初始化完成后调用（DemoLaunchProcedure 中已保证）。
/// </summary>
public static class DemoDataLoader
{
    /// <summary>
    /// 异步加载全部配置表。
    /// 配置文件位于 StreamingAssets/Config/Json/，经 LocalFileResourceHelper 加载为原始字节。
    /// </summary>
    public static async Task LoadAllAsync(CancellationToken ct)
    {
        await GameEntry.Config.LoadConfigAsync<Demo_CharacterConfig>("Config/Json/Demo_Character.json", ct);
        await GameEntry.Config.LoadConfigAsync<Demo_EnemyConfig>("Config/Json/Demo_Enemy.json", ct);
        await GameEntry.Config.LoadConfigAsync<Demo_QuestConfig>("Config/Json/Demo_Quest.json", ct);
        await GameEntry.Config.LoadConfigAsync<Demo_ActionConfig>("Config/Json/Demo_Action.json", ct);
        await GameEntry.Config.LoadConfigAsync<Demo_RewardConfig>("Config/Json/Demo_Reward.json", ct);
        Log.Info("[Demo] Config: loaded {0} tables.", GameEntry.Config.ConfigCount);
    }
}
