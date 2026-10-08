using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
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
        /// 根据 ConfigComponent 当前 Helper 自动读取对应的 JSON 或二进制产物。
        /// </summary>
        public static async Task LoadAllAsync(CancellationToken ct)
        {
            string root;
            string extension;
            Type helperType = GameEntry.Config.HelperType;
            if (helperType == typeof(JsonConfigHelper))
            {
                root = "Config/Json/";
                extension = ".json";
            }
            else if (helperType == typeof(BinaryConfigHelper))
            {
                root = "Config/Binary/";
                extension = ".bytes";
            }
            else
            {
                throw new RFrameworkException(
                    $"DemoDataLoader: unsupported Config Helper '{helperType?.FullName ?? "null"}'.");
            }

            await GameEntry.Config.LoadConfigAsync<Demo_CharacterConfig>(
                root + "Demo_Character" + extension, ct);
            await GameEntry.Config.LoadConfigAsync<Demo_EnemyConfig>(
                root + "Demo_Enemy" + extension, ct);
            await GameEntry.Config.LoadConfigAsync<Demo_QuestConfig>(
                root + "Demo_Quest" + extension, ct);
            await GameEntry.Config.LoadConfigAsync<Demo_ActionConfig>(
                root + "Demo_Action" + extension, ct);
            await GameEntry.Config.LoadConfigAsync<Demo_RewardConfig>(
                root + "Demo_Reward" + extension, ct);
            Log.Info("[Demo] Config: loaded {0} tables.", GameEntry.Config.ConfigCount);
        }
    }
}
