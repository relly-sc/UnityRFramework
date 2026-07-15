using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;
using UnityRFramework.Tests.Config;

namespace UnityRFramework.Tests
{
    /// <summary>
    /// ConfigPipeline v1 独立运行时验收入口，不依赖 Samples/Demo。
    /// </summary>
    public sealed class ConfigPipelineAcceptance : MonoBehaviour
    {
        public const string PassMarker = "CONFIG_PIPELINE_ACCEPTANCE_PASS";
        public const string FailMarker = "CONFIG_PIPELINE_ACCEPTANCE_FAIL";

        private const string ConfigPath =
            "ConfigPipelineAcceptance/Config/Binary/Acceptance_Action.bytes";
        private const string JsonConfigPath =
            "ConfigPipelineAcceptance/Config/Json/Acceptance_Action.json";
        private const string BinaryBundlePath =
            "ConfigPipelineAcceptance/Config/Binary/AcceptanceBundle.bytes";
        private const string JsonBundlePath =
            "ConfigPipelineAcceptance/Config/Json/AcceptanceBundle.json";
        private const string EnglishPath =
            "ConfigPipelineAcceptance/Localization/Binary/en.bytes";

        private async void Start()
        {
            try
            {
                await RunAsync();
                await ShutdownFrameworkAsync();
                Debug.Log(PassMarker);
                Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(FailMarker + Environment.NewLine + exception);
                try
                {
                    await ShutdownFrameworkAsync();
                }
                catch (Exception shutdownException)
                {
                    Debug.LogException(shutdownException);
                }

                Exit(1);
            }
        }

        private static async Task RunAsync()
        {
            ConfigComponent config = RequireComponent(GameEntry.Config, nameof(GameEntry.Config));
            LocalizationComponent localization = RequireComponent(
                GameEntry.Localization, nameof(GameEntry.Localization));
            ResourceComponent resource = RequireComponent(
                GameEntry.Resource, nameof(GameEntry.Resource));

            Require(
                BinaryConfigCodecRegistry.TryGet(typeof(Acceptance_ActionConfig), out _),
                "Generated Config codec was not registered before scene startup.");
            Require(
                ConfigSchemaRegistry.TryGet(typeof(Acceptance_ActionConfig), out _),
                "Generated Config schema was not registered before scene startup.");

            await VerifyConfigAsync(config, resource);
            await VerifyJsonConfigAsync(resource);
            await VerifyConfigBundlesAsync(config, resource);
            await VerifyLocalizationAsync(localization, resource);
        }

        private static async Task VerifyConfigAsync(
            ConfigComponent config, ResourceComponent resource)
        {
            await config.LoadConfigAsync<Acceptance_ActionConfig>(ConfigPath);
            Require(config.HasConfig<Acceptance_ActionConfig>(), "Config was not cached.");
            Require(
                config.GetAllConfigs<Acceptance_ActionConfig>().Count == 2,
                "Config row count mismatch.");
            Require(
                config.GetConfig<Acceptance_ActionConfig>(1)?.NameKey == "acceptance_attack",
                "Config Id query mismatch.");

            await config.LoadConfigAsync<Acceptance_ActionConfig>(ConfigPath);
            Require(config.ConfigCount == 1, "Repeated Config load created another table.");

            byte[] validBytes = await LoadBytesAsync(resource, ConfigPath);
            byte[] badCrc = CloneAndFlip(validBytes, validBytes.Length - 1);
            ExpectFailure(
                () => config.LoadConfig<Acceptance_ActionConfig>(badCrc), "Config CRC");

            byte[] badSchema = CloneAndFlip(validBytes, 10);
            ExpectFailure(
                () => config.LoadConfig<Acceptance_ActionConfig>(badSchema), "Config schema");

            byte[] badVersion = CloneAndFlip(validBytes, 4);
            ExpectFailure(
                () => config.LoadConfig<Acceptance_ActionConfig>(badVersion), "Config version");
            Require(
                config.GetConfig<Acceptance_ActionConfig>(1)?.NameKey == "acceptance_attack",
                "Rejected Config data replaced the valid cache.");

            config.UnloadConfig<Acceptance_ActionConfig>();
            Require(!config.HasConfig<Acceptance_ActionConfig>(), "Config unload failed.");
            await config.LoadConfigAsync<Acceptance_ActionConfig>(ConfigPath);
            Require(
                config.HasConfigRow<Acceptance_ActionConfig>(2), "Config reload failed.");
        }

        private static async Task VerifyLocalizationAsync(
            LocalizationComponent localization, ResourceComponent resource)
        {
            await localization.SwitchLanguageAsync("zh-CN");
            Require(
                localization.GetString("acceptance_title") == "配置管线验收",
                "Chinese query mismatch.");

            await localization.SwitchLanguageAsync("en");
            Require(
                localization.GetString("acceptance_title") == "Config Pipeline Acceptance",
                "English query mismatch.");
            Require(
                localization.GetString("missing_acceptance_key") == "missing_acceptance_key",
                "Missing Localization key did not fall back to the key.");

            ILocalizationModule module = RFrameworkModuleEntry.GetModule<ILocalizationModule>();
            byte[] validBytes = await LoadBytesAsync(resource, EnglishPath);
            byte[] badCrc = CloneAndFlip(validBytes, validBytes.Length - 1);
            ExpectFailure(() => module.LoadLanguage("en", badCrc), "Localization CRC");

            byte[] badVersion = CloneAndFlip(validBytes, 4);
            ExpectFailure(() => module.LoadLanguage("en", badVersion), "Localization version");

            byte[] duplicate = BuildDuplicateLocalization();
            ExpectFailure(
                () => module.LoadLanguage("duplicate", duplicate),
                "Localization duplicate key");
            Require(
                localization.GetString("acceptance_title") == "Config Pipeline Acceptance",
                "Rejected Localization data replaced the valid cache.");

            localization.UnloadLanguage("en");
            Require(!localization.HasLanguage("en"), "Localization unload failed.");
            await localization.SwitchLanguageAsync("en");
            Require(
                localization.GetString("acceptance_title") == "Config Pipeline Acceptance",
                "Language reload failed.");
        }

        private static async Task VerifyJsonConfigAsync(ResourceComponent resource)
        {
            byte[] bytes = await LoadBytesAsync(resource, JsonConfigPath);
            GameObject owner = new GameObject("JSON Config Acceptance Helper");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                object table = helper.ParseConfig(typeof(Acceptance_ActionConfig), bytes);
                Acceptance_ActionConfig row =
                    helper.GetConfig<Acceptance_ActionConfig>(table, 1);
                Require(row?.NameKey == "acceptance_attack", "JSON Config query mismatch.");
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
            }
        }

        private static async Task VerifyConfigBundlesAsync(
            ConfigComponent config, ResourceComponent resource)
        {
            byte[] binaryBytes = await LoadBytesAsync(resource, BinaryBundlePath);
            config.LoadConfigBundle(binaryBytes);
            Require(
                config.GetConfig<Acceptance_PartitionConfig>(1000)?.NameKey
                    == "acceptance_partition_low",
                "Binary bundle did not load the low partition.");
            Require(
                config.GetConfig<Acceptance_PartitionConfig>(2000)?.NameKey
                    == "acceptance_partition_high",
                "Binary bundle did not load the high partition.");
            Require(config.ConfigCount == 2, "Bundle cache must be counted by row type.");

            byte[] badBundle = CloneAndFlip(binaryBytes, binaryBytes.Length - 1);
            ExpectFailure(() => config.LoadConfigBundle(badBundle), "Config bundle CRC");
            Require(
                config.GetConfig<Acceptance_PartitionConfig>(2000)?.NameKey
                    == "acceptance_partition_high",
                "Rejected bundle replaced the valid cache.");

            byte[] jsonBytes = await LoadBytesAsync(resource, JsonBundlePath);
            GameObject owner = new GameObject("JSON Config Bundle Acceptance Helper");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                IReadOnlyDictionary<Type, object> tables =
                    helper.ParseConfigBundle(jsonBytes);
                object table = tables[typeof(Acceptance_PartitionConfig)];
                Require(
                    helper.GetConfig<Acceptance_PartitionConfig>(table, 1000)?.NameKey
                        == "acceptance_partition_low",
                    "JSON bundle did not load the low partition.");
                Require(
                    helper.GetConfig<Acceptance_PartitionConfig>(table, 2000)?.NameKey
                        == "acceptance_partition_high",
                    "JSON bundle did not load the high partition.");
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
            }
        }

        private static async Task<byte[]> LoadBytesAsync(
            ResourceComponent resource, string assetPath)
        {
            await resource.InitializeAsync();
            TextAsset asset = await resource.LoadAssetAsync<TextAsset>(assetPath);
            if (asset == null)
            {
                throw new RFrameworkException($"Acceptance asset '{assetPath}' was not found.");
            }

            try
            {
                return asset.bytes;
            }
            finally
            {
                resource.UnloadAsset<TextAsset>(assetPath);
            }
        }

        private static byte[] BuildDuplicateLocalization()
        {
            byte[] body;
            using (MemoryStream bodyStream = new MemoryStream())
            using (BinaryWriter bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8, true))
            {
                BinaryFormatUtility.WriteUtf8String(bodyWriter, "duplicate", false);
                BinaryFormatUtility.WriteUtf8String(bodyWriter, "first", false);
                BinaryFormatUtility.WriteUtf8String(bodyWriter, "duplicate", false);
                BinaryFormatUtility.WriteUtf8String(bodyWriter, "second", false);
                bodyWriter.Flush();
                body = bodyStream.ToArray();
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("URFL"));
                writer.Write(BinaryFormatUtility.LocalizationVersion);
                writer.Write(2);
                writer.Write(body.Length);
                writer.Write(BinaryFormatUtility.ComputeCrc32(body));
                writer.Write(body);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] CloneAndFlip(byte[] source, int index)
        {
            byte[] clone = (byte[])source.Clone();
            clone[index] ^= 0x7F;
            return clone;
        }

        private static void ExpectFailure(Action action, string operation)
        {
            try
            {
                action();
            }
            catch (RFrameworkException)
            {
                return;
            }

            throw new RFrameworkException(operation + " accepted invalid data.");
        }

        private static T RequireComponent<T>(T component, string componentName) where T : class
        {
            return component
                ?? throw new RFrameworkException(componentName + " is not available.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new RFrameworkException(message);
            }
        }

        private static async Task ShutdownFrameworkAsync()
        {
            UnityRFrameworkComponentEntry.Shutdown(ShutdownType.None);
            await Task.Yield();
            await Task.Yield();
            Require(GameEntry.Base == null, "Framework component registry was not cleared.");
        }

        private static void Exit(int exitCode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
