#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Component 自定义 Inspector 通用工具。
    /// 提供 Helper 类型下拉框绘制等共享能力。
    /// </summary>
    public static class ComponentEditorUtility
    {
        /// <summary>
        /// Helper 基类到可选类型名称的缓存。程序集重载时由 Unity 自动重建。
        /// </summary>
        private static readonly Dictionary<Type, string[]> HelperTypeNames =
            new Dictionary<Type, string[]>();

        /// <summary>
        /// 绘制 Helper 类型下拉框。
        /// 自动扫描当前 AppDomain 中继承自 <paramref name="baseHelperType"/> 的所有非抽象类。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="currentTypeName">当前选中的类型全名。</param>
        /// <param name="baseHelperType">Helper 基类类型。</param>
        /// <returns>用户选中的类型全名。</returns>
        public static string HelperTypePopup(string label, string currentTypeName, Type baseHelperType)
        {
            if (baseHelperType == null)
            {
                EditorGUILayout.LabelField(label, "Base helper type is null.");
                return currentTypeName;
            }

            if (!HelperTypeNames.TryGetValue(baseHelperType, out string[] cachedTypeNames))
            {
                // 每个 Helper 基类在一次 Domain 生命周期内只扫描一次。
                List<Type> helperTypes = new List<Type>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.IsClass && !type.IsAbstract && baseHelperType.IsAssignableFrom(type))
                            {
                                helperTypes.Add(type);
                            }
                        }
                    }
                    catch
                    {
                        // 忽略无法加载的程序集
                    }
                }

                cachedTypeNames = helperTypes
                    .OrderBy(type => type.FullName)
                    .Select(type => type.FullName)
                    .ToArray();
                HelperTypeNames.Add(baseHelperType, cachedTypeNames);
            }

            List<string> displayNames = new List<string>(cachedTypeNames);

            // 当前值若不在扫描结果中，保留在首位供用户选择
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(currentTypeName))
            {
                int found = displayNames.FindIndex(n => n == currentTypeName);
                if (found >= 0)
                {
                    selectedIndex = found;
                }
                else
                {
                    displayNames.Insert(0, currentTypeName);
                    selectedIndex = 0;
                }
            }

            if (displayNames.Count < 1)
            {
                EditorGUILayout.LabelField(label, "No available helper type.");
                return currentTypeName;
            }

            int newIndex = EditorGUILayout.Popup(label, selectedIndex, displayNames.ToArray());
            return displayNames[newIndex];
        }

        /// <summary>
        /// 在 Play Mode 下绘制框架组件的只读运行信息。
        /// </summary>
        /// <param name="component">要显示的框架组件。</param>
        public static void DrawRuntimeInformation(Runtime.UnityRFrameworkComponent component)
        {
            if (component == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);

            if (component is Runtime.BaseComponent baseComponent)
            {
                DrawValue("Frame Rate", baseComponent.FrameRate.ToString());
                DrawValue("Game Speed", baseComponent.GameSpeed.ToString("0.##"));
                DrawValue("Game Paused", FormatBoolean(baseComponent.IsGamePaused));
                DrawValue("Run In Background", FormatBoolean(baseComponent.RunInBackground));
            }
            else if (component is Runtime.AudioComponent audioComponent)
            {
                DrawValue("Cached Audio Assets", audioComponent.LoadedAudioAssetCount.ToString());
                DrawValue("Current BGM", EmptyFallback(audioComponent.CurrentBgmAssetName));
                DrawValue("BGM State", string.IsNullOrEmpty(audioComponent.CurrentBgmAssetName)
                    ? "Stopped"
                    : audioComponent.IsBgmPaused ? "Paused" : "Playing");
                DrawValue("Muted", FormatBoolean(audioComponent.Muted));
                DrawValue("BGM / SFX / UI Volume", string.Format("{0:0.##} / {1:0.##} / {2:0.##}",
                    audioComponent.BgmVolume, audioComponent.SfxVolume, audioComponent.UIVolume));
            }
            else if (component is Runtime.ConfigComponent configComponent)
            {
                DrawValue("Loaded Config Tables", configComponent.ConfigCount.ToString());
            }
            else if (component is Runtime.DebuggerComponent debuggerComponent)
            {
                DrawValue("Window Active", FormatBoolean(debuggerComponent.ActiveWindow));
                DrawValue("Current FPS", Runtime.DebuggerOverlay.CurrentFps.ToString("0.0"));
                DrawValue("Cached Logs", string.Format("{0} / {1}",
                    debuggerComponent.LogCount, debuggerComponent.MaxLogEntries));
            }
            else if (component is Runtime.EntityComponent entityComponent)
            {
                DrawValue("Managed Entities", entityComponent.EntityCount.ToString());
                DrawValue("Entity Groups", entityComponent.EntityGroupCount.ToString());
            }
            else if (component is Runtime.EventComponent eventComponent)
            {
                DrawValue("Event Handlers", eventComponent.HandlerCount.ToString());
                DrawValue("Queued Async Events", eventComponent.AsyncEventCount.ToString());
            }
            else if (component is Runtime.FsmComponent fsmComponent)
            {
                DrawValue("FSM Instances", fsmComponent.FsmCount.ToString());
            }
            else if (component is Runtime.LocalizationComponent localizationComponent)
            {
                DrawValue("Current Language", EmptyFallback(localizationComponent.CurrentLanguage));
                DrawValue("Loaded Languages", localizationComponent.LoadedLanguageCount.ToString());
                DrawValue("Supported Languages", localizationComponent.SupportedLanguages.Count.ToString());
            }
            else if (component is Runtime.NetworkComponent networkComponent)
            {
                DrawValue("Network Channels", networkComponent.ChannelCount.ToString());
                DrawValue("Default Connected", FormatBoolean(networkComponent.IsConnected));
                RFramework.INetworkChannel channel = networkComponent.DefaultChannel;
                DrawValue("Default Channel", channel != null ? channel.Name : "None");
                DrawValue("Remote Endpoint", channel != null && !string.IsNullOrEmpty(channel.CurrentIP)
                    ? string.Format("{0}:{1}", channel.CurrentIP, channel.CurrentPort)
                    : "None");
            }
            else if (component is Runtime.PoolComponent poolComponent)
            {
                DrawValue("Object Pools", poolComponent.PoolCount.ToString());
            }
            else if (component is Runtime.ProcedureComponent procedureComponent)
            {
                DrawValue("Registered Procedures", procedureComponent.ProcedureCount.ToString());
                DrawValue("Current Procedure", procedureComponent.CurrentProcedure != null
                    ? procedureComponent.CurrentProcedure.GetType().Name
                    : "None");
                DrawValue("Current State Time", string.Format("{0:0.00} s",
                    procedureComponent.CurrentProcedureTime));
            }
            else if (component is Runtime.ResourceComponent resourceComponent)
            {
                DrawValue("Loaded Assets", resourceComponent.LoadedAssetCount.ToString());
                DrawValue("Loading Assets", resourceComponent.LoadingAssetCount.ToString());
            }
            else if (component is Runtime.SceneComponent sceneComponent)
            {
                DrawValue("Current Scene", EmptyFallback(sceneComponent.CurrentSceneName));
                DrawValue("Loaded Scenes", sceneComponent.LoadedSceneCount.ToString());
                DrawValue("Loading Scenes", sceneComponent.LoadingSceneCount.ToString());
            }
            else if (component is Runtime.TimerComponent timerComponent)
            {
                DrawValue("Active Timers", timerComponent.TimerCount.ToString());
            }
            else if (component is Runtime.UIComponent uiComponent)
            {
                DrawValue("Managed UI Windows", uiComponent.UIFormCount.ToString());
            }
            else if (component is Runtime.WebRequestComponent webRequestComponent)
            {
                DrawValue("Active Requests", webRequestComponent.ActiveRequestCount.ToString());
                DrawValue("Queued Requests", webRequestComponent.QueuedRequestCount.ToString());
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制一行只读名称和值。
        /// </summary>
        private static void DrawValue(string label, string value)
        {
            EditorGUILayout.LabelField(label, value ?? string.Empty);
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrEmpty(value) ? "None" : value;
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "Yes" : "No";
        }
    }
}

#endif
