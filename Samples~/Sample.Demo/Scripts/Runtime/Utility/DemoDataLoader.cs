using System.Threading;
using System.Threading.Tasks;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample
{
    /// <summary>
    /// Demo 配置数据加载器。集中加载全部配置表，供大厅/远征/结算共用。
    /// 必须在 Resource 模块初始化完成后调用（DemoLaunchProcedure 中已保证）。
    /// </summary>
    public static class DemoDataLoader
    {
        /// <summary>
        /// 异步加载全部配置表。
        /// 配置文件位于 StreamingAssets/Config/Binary/，经 LocalFileResourceHelper 加载为原始字节。
        /// </summary>
        public static async Task LoadAllAsync(CancellationToken ct)
        {
            await GameEntry.Config.LoadConfigAsync<Demo_CharacterConfig>("Config/Binary/Demo_Character.bytes", ct);
            await GameEntry.Config.LoadConfigAsync<Demo_EnemyConfig>("Config/Binary/Demo_Enemy.bytes", ct);
            await GameEntry.Config.LoadConfigAsync<Demo_QuestConfig>("Config/Binary/Demo_Quest.bytes", ct);
            await GameEntry.Config.LoadConfigAsync<Demo_ActionConfig>("Config/Binary/Demo_Action.bytes", ct);
            await GameEntry.Config.LoadConfigAsync<Demo_RewardConfig>("Config/Binary/Demo_Reward.bytes", ct);
            Log.Info("[Demo] Config: loaded {0} tables.", GameEntry.Config.ConfigCount);
        }
    }
}
