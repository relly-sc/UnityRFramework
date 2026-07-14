#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 框架调试器 EditorWindow。PlayMode 下实时显示 FPS、帧时间、
    /// 各模块状态和 GM 命令输入。菜单：UnityRFramework → Debugger。
    /// </summary>
    public class DebuggerWindow : EditorWindow
    {
        /// <summary>
        /// 当前选中的标签页索引。
        /// </summary>
        private int selectedTab;

        /// <summary>
        /// 标签页名称。
        /// </summary>
        private readonly string[] tabNames = { "Overview", "Modules", "Console", "GM" };

        /// <summary>
        /// FPS 滚动缓冲区。
        /// </summary>
        private Queue<float> fpsHistory = new Queue<float>();

        /// <summary>
        /// GM 命令输入。
        /// </summary>
        private string gmCommand = "";

        /// <summary>
        /// GM 命令执行结果。
        /// </summary>
        private string gmResult = "";

        /// <summary>
        /// 模块信息列表。
        /// </summary>
        private List<Runtime.IDebuggerInfo> moduleInfos = new List<Runtime.IDebuggerInfo>();

        /// <summary>
        /// 模块信息折叠状态。
        /// </summary>
        private Dictionary<string, bool> moduleFoldouts = new Dictionary<string, bool>();

        /// <summary>
        /// 模块详情滚动位置。
        /// </summary>
        private Vector2 moduleScrollPos;

        /// <summary>
        /// GM 滚动位置。
        /// </summary>
        private Vector2 gmScrollPos;

        /// <summary>
        /// Console 滚动位置。
        /// </summary>
        private Vector2 consoleScroll;

        /// <summary>
        /// Console 当前选中的日志索引，-1 表示未选中。
        /// </summary>
        private int selectedLogIndex = -1;

        /// <summary>
        /// 打开调试器窗口。
        /// </summary>
        [MenuItem("UnityRFramework/Debugger")]
        public static void Open()
        {
            DebuggerWindow window = GetWindow<DebuggerWindow>("RF Debugger");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        /// <summary>
        /// FPS 图表采样定时器（使用 Time.unscaledDeltaTime 保证与游戏 FPS 一致）。
        /// </summary>
        private float fpsChartTimer;
        private const float FpsChartSampleInterval = 0.3f;

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;

            // 基于 Time.unscaledDeltaTime 采样，与 Unity Stats 窗口同源
            fpsChartTimer += Time.unscaledDeltaTime;
            if (fpsChartTimer >= FpsChartSampleInterval)
            {
                float fps = 1.0f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                fpsHistory.Enqueue(fps);
                if (fpsHistory.Count > 100) fpsHistory.Dequeue();

                fpsChartTimer -= FpsChartSampleInterval;
            }

            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                fpsHistory.Clear();
                fpsChartTimer = 0f;
                moduleInfos.Clear();
            }

            Repaint();
        }

        private void Update()
        {
            // 不再采样 FPS，已由 OnEditorUpdate 处理
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter PlayMode to view debug data.", MessageType.Info);
                return;
            }

            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

            switch (selectedTab)
            {
                case 0:
                    DrawOverview();
                    break;
                case 1:
                    DrawModules();
                    break;
                case 2:
                    DrawConsole();
                    break;
                case 3:
                    DrawGM();
                    break;
            }
        }

        // ==============================
        // Overview
        // ==============================

        private void DrawOverview()
        {
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);

            // 直接读取 Overlay 的帧计数 FPS，保证与 Unity Stats 一致
            float currentFps = Runtime.DebuggerOverlay.CurrentFps;

            EditorGUILayout.LabelField("FPS", string.Format("{0:F1}", currentFps));
            EditorGUILayout.LabelField("Frame Time", string.Format("{0:F2} ms", Time.unscaledDeltaTime * 1000f));
            EditorGUILayout.LabelField("GC Memory", string.Format("{0:F1} MB", System.GC.GetTotalMemory(false) / 1048576f));

            EditorGUILayout.Space();

            // FPS 趋势图
            EditorGUILayout.LabelField("FPS History", EditorStyles.boldLabel);
            Rect chartRect = GUILayoutUtility.GetRect(400, 100);
            if (fpsHistory.Count > 1)
            {
                DrawFpsChart(chartRect);
            }

            EditorGUILayout.Space();

            // 模块快速概览
            EditorGUILayout.LabelField("Modules Quick View", EditorStyles.boldLabel);
            RefreshModuleInfos();

            foreach (Runtime.IDebuggerInfo info in moduleInfos)
            {
                EditorGUILayout.LabelField(info.GetModuleName(), info.GetStatus());
            }
        }

        private void DrawFpsChart(Rect rect)
        {
            float[] fpsData = fpsHistory.ToArray();
            if (fpsData.Length < 2)
            {
                return;
            }

            Handles.BeginGUI();
            Handles.color = Color.grey;
            Handles.DrawLine(
                new Vector3(rect.x, rect.y + rect.height / 2),
                new Vector3(rect.x + rect.width, rect.y + rect.height / 2));

            Handles.color = Color.green;
            float stepX = rect.width / (fpsData.Length - 1);
            float maxFps = 120f;
            float scaleY = rect.height / maxFps;

            for (int i = 0; i < fpsData.Length - 1; i++)
            {
                float x1 = rect.x + i * stepX;
                float y1 = rect.y + rect.height - Mathf.Clamp(fpsData[i], 0, maxFps) * scaleY;
                float x2 = rect.x + (i + 1) * stepX;
                float y2 = rect.y + rect.height - Mathf.Clamp(fpsData[i + 1], 0, maxFps) * scaleY;
                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }

            Handles.EndGUI();
        }

        // ==============================
        // Modules
        // ==============================

        private void DrawModules()
        {
            RefreshModuleInfos();

            moduleScrollPos = EditorGUILayout.BeginScrollView(moduleScrollPos);

            foreach (Runtime.IDebuggerInfo info in moduleInfos)
            {
                string name = info.GetModuleName();
                if (!moduleFoldouts.ContainsKey(name))
                {
                    moduleFoldouts[name] = false;
                }

                moduleFoldouts[name] = EditorGUILayout.Foldout(moduleFoldouts[name], string.Format("{0} — {1}", name, info.GetStatus()));

                if (moduleFoldouts[name])
                {
                    EditorGUI.indentLevel++;
                    var details = info.GetDetails();
                    if (details != null)
                    {
                        foreach (var kv in details)
                        {
                            EditorGUILayout.LabelField(kv.Key, kv.Value);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No details available.");
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ==============================
        // Console
        // ==============================

        /// <summary>
        /// Console 面板。每条日志显示图标+消息+源文件位置行（对齐 Unity 原生 Console 格式）。
        /// 点击条目展开完整堆栈，点击堆栈中的文件路径可跳转到源码行。
        /// </summary>
        private void DrawConsole()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                Runtime.DebuggerOverlay.ClearLogCache();
                selectedLogIndex = -1;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 读取 DebuggerOverlay 的日志缓存
            var logs = Runtime.DebuggerOverlay.GetLogCache();
            if (logs == null || logs.Count == 0)
            {
                EditorGUILayout.HelpBox("No logs captured. Enable the overlay (F3) to start logging.", MessageType.Info);
                return;
            }

            consoleScroll = EditorGUILayout.BeginScrollView(consoleScroll);

            for (int i = logs.Count - 1; i >= 0; i--)
            {
                var entry = logs[i];
                bool isSelected = (selectedLogIndex == i);
                Color entryColor = GetLogColor(entry.Type);

                // --- 图标 + 消息行 ---
                EditorGUILayout.BeginHorizontal();

                Texture2D icon = GetLogIcon(entry.Type);
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(14));
                }

                // 消息文本，包含时间前缀
                string displayMsg = string.Format("{0} {1}", entry.Time, entry.Message);

                GUIStyle msgStyle = new GUIStyle(EditorStyles.label);
                msgStyle.normal.textColor = entryColor;
                msgStyle.wordWrap = true;
                msgStyle.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                msgStyle.padding = new RectOffset(0, 0, 1, 0);
                msgStyle.margin = new RectOffset(0, 0, 0, 0);

                // 用 Label 绘制以便用 GetLastRect 检测点击
                EditorGUILayout.LabelField(displayMsg, msgStyle);
                Rect msgRect = GUILayoutUtility.GetLastRect();

                // 点击消息行切换选中
                if (Event.current.type == EventType.MouseDown && msgRect.Contains(Event.current.mousePosition))
                {
                    selectedLogIndex = isSelected ? -1 : i;
                    Event.current.Use();
                    Repaint();
                }

                EditorGUILayout.EndHorizontal();

                // --- 源文件位置行（灰色，类似 Unity Console 的第二行） ---
                string location = ExtractLocation(entry.StackTrace);
                if (!string.IsNullOrEmpty(location))
                {
                    EditorGUI.indentLevel++;
                    GUIStyle locStyle = new GUIStyle(EditorStyles.miniLabel);
                    locStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
                    locStyle.richText = false;
                    EditorGUILayout.LabelField(location, locStyle);
                    EditorGUI.indentLevel--;
                }

                // --- 选中时展开完整堆栈 ---
                if (isSelected && !string.IsNullOrEmpty(entry.StackTrace))
                {
                    EditorGUI.indentLevel++;
                    DrawStackTrace(entry.StackTrace);
                    EditorGUI.indentLevel--;
                }

                // 条目间距
                EditorGUILayout.Space(1);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 从堆栈中提取第一条用户代码路径（过滤 Unity 内部行）。
        /// 返回 "Assets/Scripts/Example.cs:42" 格式。
        /// </summary>
        private string ExtractLocation(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return "";
            }

            string[] lines = stackTrace.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                // 找到第一个包含用户 Assets 路径的堆栈行
                if (trimmed.Contains("(at ") && (trimmed.Contains("Assets/") || trimmed.Contains("assets/")))
                {
                    Match match = Regex.Match(trimmed, @"\(at (.+?):(\d+)\)");
                    if (match.Success)
                    {
                        string fullPath = match.Groups[1].Value;
                        // 绝对路径截取为相对路径
                        int assetsIdx = fullPath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                        if (assetsIdx >= 0)
                        {
                            fullPath = fullPath.Substring(assetsIdx + 1);
                        }
                        return string.Format("{0}:{1}", fullPath, match.Groups[2].Value);
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// 绘制堆栈跟踪。每行独立绘制，包含文件路径的行可点击跳转源码。
        /// </summary>
        private void DrawStackTrace(string stackTrace)
        {
            string[] lines = stackTrace.Split('\n');

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                // 尝试匹配 "(at path:line)" 格式，做成可点击链接
                Match match = Regex.Match(trimmed, @"\(at (.+?):(\d+)\)");
                if (match.Success)
                {
                    string filePath = match.Groups[1].Value;
                    int lineNum = int.Parse(match.Groups[2].Value);

                    // 蓝色链接样式
                    GUIStyle linkStyle = new GUIStyle(EditorStyles.miniLabel);
                    linkStyle.normal.textColor = new Color(0.35f, 0.55f, 0.85f);
                    linkStyle.hover.textColor = new Color(0.5f, 0.7f, 1f);
                    linkStyle.wordWrap = true;

                    if (GUILayout.Button(trimmed, linkStyle))
                    {
                        OpenFileAtLine(filePath, lineNum);
                    }
                }
                else
                {
                    // 普通堆栈行
                    GUIStyle stackStyle = new GUIStyle(EditorStyles.miniLabel);
                    stackStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                    stackStyle.wordWrap = true;
                    EditorGUILayout.LabelField(trimmed, stackStyle);
                }
            }
        }

        /// <summary>
        /// 尝试在 IDE 中打开指定文件并定位到行号。
        /// 支持相对路径和绝对路径（自动截取 Assets/ 前缀）。
        /// </summary>
        private void OpenFileAtLine(string filePath, int line)
        {
            // 绝对路径截取为相对路径
            string relativePath = filePath;
            int assetsIdx = filePath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx >= 0)
            {
                relativePath = filePath.Substring(assetsIdx + 1);
            }

            // 尝试通过 AssetDatabase 打开
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
            if (asset != null)
            {
                // AssetDatabase.OpenAsset 可定位到行号
                if (!AssetDatabase.OpenAsset(asset, line))
                {
                    Debug.LogWarning(string.Format("Unable to open asset at line: {0}:{1}", relativePath, line));
                }
            }
            else
            {
                Debug.LogWarning(string.Format("File not found in project: {0}", relativePath));
            }
        }

        /// <summary>
        /// 根据日志类型获取对应的内置图标。
        /// </summary>
        private Texture2D GetLogIcon(LogType type)
        {
            string iconName;
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    iconName = "console.erroricon";
                    break;
                case LogType.Warning:
                    iconName = "console.warnicon";
                    break;
                case LogType.Assert:
                    iconName = "console.erroricon";
                    break;
                default:
                    iconName = "console.infoicon";
                    break;
            }
            return EditorGUIUtility.IconContent(iconName).image as Texture2D;
        }

        /// <summary>
        /// 根据日志类型返回对应的显示颜色。
        /// </summary>
        private Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    return new Color(1f, 0.35f, 0.35f);
                case LogType.Warning:
                    return new Color(1f, 0.82f, 0.25f);
                case LogType.Assert:
                    return new Color(1f, 0.35f, 0.35f);
                default:
                    return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        // ==============================
        // GM Commands
        // ==============================

        private void DrawGM()
        {
            EditorGUILayout.LabelField("GM Command", EditorStyles.boldLabel);
            gmCommand = EditorGUILayout.TextField("Command:", gmCommand);

            if (GUILayout.Button("Execute"))
            {
                if (!string.IsNullOrWhiteSpace(gmCommand))
                {
                    gmResult = ExecuteGMCommand(gmCommand);
                }
            }

            EditorGUILayout.Space();
            gmScrollPos = EditorGUILayout.BeginScrollView(gmScrollPos, GUILayout.Height(150));
            EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(gmResult, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private string ExecuteGMCommand(string command)
        {
            string[] parts = command.Split(' ');
            if (parts.Length == 0)
            {
                return "Empty command.";
            }

            string cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "fps":
                    {
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int target))
                        {
                            Application.targetFrameRate = target;
                            return string.Format("Target FPS set to {0}.", target);
                        }
                        return string.Format("Current target FPS: {0}. Usage: fps <target>", Application.targetFrameRate);
                    }

                case "timescale":
                    {
                        if (parts.Length >= 2 && float.TryParse(parts[1], out float scale))
                        {
                            Time.timeScale = scale;
                            return string.Format("TimeScale set to {0}.", scale);
                        }
                        return string.Format("Current TimeScale: {0}. Usage: timescale <value>", Time.timeScale);
                    }

                case "gc":
                    {
                        long before = System.GC.GetTotalMemory(false);
                        System.GC.Collect();
                        long after = System.GC.GetTotalMemory(false);
                        long freed = before - after;
                        return string.Format("GC collected {0:F1} MB.", freed / 1048576f);
                    }

                case "help":
                    return "Commands: fps <target>, timescale <value>, gc, help";

                default:
                    return string.Format("Unknown command: '{0}'. Type 'help' for available commands.", cmd);
            }
        }

        /// <summary>
        /// 从 Module 层收集模块调试信息（对齐 DebuggerOverlay）。
        /// </summary>
        private void RefreshModuleInfos()
        {
            moduleInfos.Clear();

            try
            {
                // Log
                moduleInfos.Add(new ModuleDebugInfo("Log",
                    RFramework.RFrameworkLog.IsInitialized ? "Initialized" : "Not initialized", null));

                // Event
                var eventM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IEventModule>();
                if (eventM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Event", "Active", null));

                // Fsm
                var fsmM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IFsmModule>();
                if (fsmM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Fsm",
                        string.Format("FSMs: {0}", fsmM.FsmCount), null));

                // Procedure
                var procM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IProcedureModule>();
                if (procM != null)
                {
                    var current = procM.CurrentProcedure;
                    string procName = current != null ? current.GetType().Name : "(none)";
                    var procDetails = new Dictionary<string, string>
                    {
                        { "Running Time", string.Format("{0:F1}s", procM.CurrentProcedureTime) }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Procedure", procName, procDetails));
                }

                // Pool
                var poolM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IPoolModule>();
                if (poolM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Active Objects", poolM.PoolCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Pool",
                        string.Format("Objects: {0}", poolM.PoolCount), details));
                }

                // Timer
                var timerM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.ITimerModule>();
                if (timerM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Active Timers", timerM.TimerCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Timer",
                        string.Format("Timers: {0}", timerM.TimerCount), details));
                }

                // Resource
                var resM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IResourceModule>();
                if (resM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Loaded Assets", resM.LoadedAssetCount.ToString() },
                        { "Loading Assets", resM.LoadingAssetCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Resource",
                        string.Format("Loaded: {0}  Loading: {1}", resM.LoadedAssetCount, resM.LoadingAssetCount), details));
                }

                // Config
                var cfgM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IConfigModule>();
                if (cfgM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Loaded Tables", cfgM.ConfigCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Config",
                        string.Format("Tables: {0}", cfgM.ConfigCount), details));
                }

                // Scene
                var sceneM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.ISceneModule>();
                if (sceneM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Loaded", sceneM.LoadedSceneCount.ToString() },
                        { "Loading", sceneM.LoadingSceneCount.ToString() }
                    };
                    string[] loaded = sceneM.GetLoadedSceneNames();
                    if (loaded != null && loaded.Length > 0)
                    {
                        details["Scenes"] = string.Join(", ", loaded);
                    }
                    moduleInfos.Add(new ModuleDebugInfo("Scene",
                        sceneM.CurrentSceneName ?? "(none)", details));
                }

                // UI
                var uiM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IUIModule>();
                if (uiM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Open Forms", uiM.UIFormCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("UI",
                        string.Format("Forms: {0}", uiM.UIFormCount), details));
                }

                // Entity
                var entM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IEntityModule>();
                if (entM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Entities", entM.EntityCount.ToString() },
                        { "Groups", entM.EntityGroupCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Entity",
                        string.Format("Entities: {0}  Groups: {1}", entM.EntityCount, entM.EntityGroupCount), details));
                }

                // Audio
                var audioM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IAudioModule>();
                if (audioM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "BGM Vol", string.Format("{0:F2}", audioM.BgmVolume) },
                        { "SFX Vol", string.Format("{0:F2}", audioM.SfxVolume) },
                        { "UI Vol", string.Format("{0:F2}", audioM.UIVolume) },
                        { "Muted", audioM.Muted.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Audio",
                        audioM.Muted ? "Muted" : "Playing", details));
                }

                // Network
                var netM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.INetworkModule>();
                if (netM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Heartbeat", string.Format("{0:F1}s", netM.HeartbeatInterval) },
                        { "Auto Reconnect", netM.AutoReconnect.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Network",
                        netM.IsConnected ? "Connected" : "Disconnected", details));
                }

                // Localization
                var locM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.ILocalizationModule>();
                if (locM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Loaded Packs", locM.LoadedLanguageCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("Localization",
                        locM.CurrentLanguage ?? "(none)", details));
                }

                // WebRequest
                var webM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IWebRequestModule>();
                if (webM != null)
                {
                    var details = new Dictionary<string, string>
                    {
                        { "Active", webM.ActiveRequestCount.ToString() },
                        { "Queued", webM.QueuedRequestCount.ToString() }
                    };
                    moduleInfos.Add(new ModuleDebugInfo("WebRequest",
                        string.Format("Active: {0}  Queued: {1}", webM.ActiveRequestCount, webM.QueuedRequestCount), details));
                }
            }
            catch (Exception)
            {
                // 模块未初始化时忽略
            }
        }

        /// <summary>
        /// 模块调试信息包装。
        /// </summary>
        private sealed class ModuleDebugInfo : Runtime.IDebuggerInfo
        {
            private string moduleName;
            private string status;
            private Dictionary<string, string> details;

            public ModuleDebugInfo(string moduleName, string status, Dictionary<string, string> details = null)
            {
                this.moduleName = moduleName;
                this.status = status;
                this.details = details;
            }

            public string GetModuleName()
            {
                return moduleName;
            }

            public string GetStatus()
            {
                return status;
            }

            public Dictionary<string, string> GetDetails()
            {
                return details;
            }
        }
    }
}

#endif
