using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Demo 配置与本地化数据的一键导出入口。
    /// </summary>
    public static class DemoConfigPipelineExporter
    {
        private const string MenuPath = "UnityRFramework/Demo/Export Config and Localization";

        [MenuItem(MenuPath)]
        public static void Export()
        {
            ConfigPipelineOptions options = new ConfigPipelineOptions
            {
                ConfigSourceDirectory =
                    "Assets/UnityRFramework/Samples/Demo/ConfigSource/Config",
                LocalizationSourceDirectory =
                    "Assets/UnityRFramework/Samples/Demo/ConfigSource/Localization",
                GeneratedCodeDirectory =
                    "Assets/UnityRFramework/Samples/Demo/Generated/UnityRFramework/Config",
                ConfigOutputDirectory =
                    "Assets/UnityRFramework/Samples/Demo/GameAssets/Resources/Config",
                LocalizationOutputDirectory =
                    "Assets/UnityRFramework/Samples/Demo/GameAssets/Resources/Localization",
                GeneratedNamespace = "Game.Config"
            };

            ConfigPipelineReport report = ConfigPipelineService.ExportAll(options);
            Debug.Log(report.ToString());
        }
    }
}
