#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 运行时调试覆盖层。收起时显示可拖动的 FPS 浮窗，展开后显示完整调试面板。
    /// 面板含 Overview / Modules / Log 三个标签页，功能对齐 RFDebugger EditorWindow。
    /// Release 包通过条件编译完全移除。
    /// </summary>
    public class DebuggerOverlay : MonoBehaviour
    {
        public struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public string Time;

            public LogEntry(string message, string stackTrace, LogType type)
            {
                Message = message;
                StackTrace = stackTrace;
                Type = type;
                Time = DateTime.Now.ToString("HH:mm:ss");
            }
        }

        public static bool ActiveWindow { get; set; }

        /// <summary>
        /// 当前 FPS（由 Update 每 0.3 秒帧计数法采样，供 DebuggerWindow 读取以保证一致性）。
        /// </summary>
        public static float CurrentFps { get; private set; }

        /// <summary>
        /// 获取当前缓存日志数量。
        /// </summary>
        public static int LogCount
        {
            get
            {
                lock (logLock)
                {
                    return logCache.Count;
                }
            }
        }

        private static readonly List<LogEntry> logCache = new List<LogEntry>();
        private static readonly object logLock = new object();

        public static List<LogEntry> GetLogCache()
        {
            lock (logLock) { return new List<LogEntry>(logCache); }
        }

        public static void ClearLogCache()
        {
            lock (logLock) { logCache.Clear(); }
        }

        // ---- 组件引用 ----
        private DebuggerComponent debuggerComponent;

        // ---- FPS ----
        private int fpsFrameCount;
        private float fpsAccum;
        private float fpsTimer;
        private float fpsLastRealtime;
        private float avgFps;
        private float moduleRefreshTimer;

        // ---- FPS 浮窗（可拖动） ----
        private Rect fpsBadgeRect = new Rect(10, 10, 130, 40);
        private bool draggingBadge;
        private Vector2 badgeDragOffset;
        private Vector2 badgePointerDownPosition;
        private bool badgeWasDragged;

        // ---- 完整窗口 ----
        private bool showFullWindow;
        private Rect windowRect = new Rect(10, 10, 500, 440);
        private bool dragging;
        private Vector2 dragOffset;
        private bool resizing;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartSize;

        // ---- 标签页 ----
        private int tabIndex;
        private string[] tabNames = { "Overview", "Modules", "Log" };

        // ---- 滚动 ----
        private Vector2 overviewScroll;
        private Vector2 modulesScroll;
        private Vector2 logScroll;

        // ---- Log 选中 ----
        private int selectedLogIndex = -1;

        // ---- Modules 展开 ----
        private Dictionary<string, bool> moduleFoldouts = new Dictionary<string, bool>();

        // ---- 模块信息 ----
        private List<IDebuggerInfo> moduleInfos = new List<IDebuggerInfo>();

        // ---- 样式 ----
        private static Texture2D windowBgTex;
        private GUIStyle badgeBoxStyle;
        private GUIStyle badgeLabelStyle;
        private GUIStyle windowBoxStyle;
        private GUIStyle titleStyle;
        private GUIStyle tabNormalStyle;
        private GUIStyle tabSelectedStyle;
        private GUIStyle labelStyle;
        private GUIStyle miniLabelStyle;
        private GUIStyle logMsgStyle;
        private GUIStyle logLocStyle;
        private GUIStyle stackStyle;
        private GUIStyle buttonStyle;
        private bool stylesInited;
        private float lastStylesScale;

        // ---- 图标 ----
        private static Texture2D iconError;
        private static Texture2D iconWarning;
        private static Texture2D iconInfo;
        private static bool iconsInited;

        // ============================================================
        // 生命周期
        // ============================================================

        internal static void Initialize(DebuggerComponent component)
        {
            GameObject go = new GameObject("[DebuggerOverlay]");
            go.transform.SetParent(component.transform, false);
            DebuggerOverlay overlay = go.AddComponent<DebuggerOverlay>();
            overlay.debuggerComponent = component;
        }

        private void Awake()
        {
            if (windowBgTex == null) windowBgTex = MakeTex(1, 1, new Color(0.13f, 0.13f, 0.15f, 0.94f));
            RefreshModuleInfos();
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if (debuggerComponent == null) return;

            lock (logLock)
            {
                logCache.Add(new LogEntry(condition, stackTrace, type));
                if (logCache.Count > debuggerComponent.MaxLogEntries)
                {
                    logCache.RemoveAt(0);
                }
            }

        }

        private void Update()
        {
            if (!ActiveWindow || debuggerComponent == null) return;

            // FPS 采样：帧计数法（Unity Stats / DebuggerWindow 均以此为准）
            float now = Time.realtimeSinceStartup;
            float delta = now - fpsLastRealtime;
            fpsLastRealtime = now;
            if (delta > 0f)
            {
                fpsFrameCount++;
                fpsAccum += delta;
                fpsTimer += delta;

                if (fpsTimer >= 0.3f)
                {
                    avgFps = fpsFrameCount / Mathf.Max(fpsAccum, 0.0001f);
                    CurrentFps = avgFps;
                    fpsFrameCount = 0;
                    fpsAccum = 0f;
                    fpsTimer = 0f;
                }
            }

            if (showFullWindow)
            {
                moduleRefreshTimer += delta;
                if (moduleRefreshTimer >= 0.5f)
                {
                    moduleRefreshTimer = 0f;
                    RefreshModuleInfos();
                }
            }

            if (Input.GetKeyDown(debuggerComponent.ToggleKey))
            {
                ToggleFullWindow();
            }

            if (showFullWindow && Input.GetKeyDown(debuggerComponent.SwitchTabKey))
            {
                tabIndex = (tabIndex + 1) % 3;
            }
        }

        private void ToggleFullWindow()
        {
            showFullWindow = !showFullWindow;
            if (showFullWindow) RefreshModuleInfos();
        }

        // ============================================================
        // OnGUI 入口
        // ============================================================

        private void OnGUI()
        {
            if (!ActiveWindow || debuggerComponent == null) return;
            float s = EffectiveScale;
            if (!stylesInited || Mathf.Abs(lastStylesScale - s) > 0.01f)
            {
                InitStyles();
                lastStylesScale = s;
            }
            EnsureIcons();

            if (!showFullWindow)
                DrawFpsBadge();
            else
                DrawFullWindow();
        }

        // ============================================================
        // FPS 浮窗（可拖动）
        // ============================================================

        private void DrawFpsBadge()
        {
            float s = EffectiveScale;
            Rect rect = new Rect(fpsBadgeRect.x, fpsBadgeRect.y, 140f * s, 44f * s);

            Color c = avgFps >= 50f ? Color.green : avgFps >= 25f ? Color.yellow : Color.red;
            badgeLabelStyle.normal.textColor = c;

            GUI.Box(rect, "", badgeBoxStyle);
            GUI.Label(rect, string.Format(" FPS: {0:F0}", avgFps), badgeLabelStyle);

            // 拖动
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                draggingBadge = true;
                badgeWasDragged = false;
                badgePointerDownPosition = Event.current.mousePosition;
                badgeDragOffset = Event.current.mousePosition - new Vector2(fpsBadgeRect.x, fpsBadgeRect.y);
                Event.current.Use();
            }

            if (draggingBadge && Event.current.type == EventType.MouseDrag)
            {
                if (Vector2.Distance(
                        Event.current.mousePosition, badgePointerDownPosition) >= 5f * s)
                {
                    badgeWasDragged = true;
                }

                fpsBadgeRect.x = Event.current.mousePosition.x - badgeDragOffset.x;
                fpsBadgeRect.y = Event.current.mousePosition.y - badgeDragOffset.y;
                Event.current.Use();
            }

            if (draggingBadge && Event.current.rawType == EventType.MouseUp)
            {
                draggingBadge = false;
                if (!badgeWasDragged)
                {
                    ToggleFullWindow();
                }
                Event.current.Use();
            }
        }

        // ============================================================
        // 完整窗口
        // ============================================================

        private void DrawFullWindow()
        {
            float s = EffectiveScale;
            Rect sr = ScaledRect();

            // 拖动标题栏
            Rect titleBar = new Rect(sr.x, sr.y, sr.width - 30f * s, 26f * s);
            if (Event.current.type == EventType.MouseDown && titleBar.Contains(Event.current.mousePosition))
            {
                dragging = true;
                dragOffset = Event.current.mousePosition - new Vector2(windowRect.x, windowRect.y);
                Event.current.Use();
            }
            if (dragging && Event.current.rawType == EventType.MouseUp)
            {
                dragging = false; Event.current.Use();
            }
            if (dragging && Event.current.type == EventType.Repaint)
            {
                sr.x = Event.current.mousePosition.x - dragOffset.x;
                sr.y = Event.current.mousePosition.y - dragOffset.y;
            }

            // 缩放
            float hs = 22f * s;
            Rect resizeH = new Rect(sr.xMax - hs, sr.yMax - hs, hs, hs);
            if (Event.current.type == EventType.MouseDown && resizeH.Contains(Event.current.mousePosition))
            {
                resizing = true;
                resizeStartMouse = Event.current.mousePosition;
                resizeStartSize = new Vector2(sr.width, sr.height);
                Event.current.Use();
            }
            if (resizing && Event.current.rawType == EventType.MouseUp)
            {
                resizing = false; Event.current.Use();
            }
            if (resizing && Event.current.type == EventType.Repaint)
            {
                sr.width = Mathf.Max(300f * s, resizeStartSize.x + Event.current.mousePosition.x - resizeStartMouse.x);
                sr.height = Mathf.Max(200f * s, resizeStartSize.y + Event.current.mousePosition.y - resizeStartMouse.y);
            }

            windowRect = new Rect(sr.x, sr.y, sr.width / s, sr.height / s);

            // 绘制窗口背景
            GUI.Box(sr, "", windowBoxStyle);

            // 标题栏
            GUI.Label(new Rect(sr.x + 10f * s, sr.y + 3f * s, sr.width - 50f * s, 22f * s),
                string.Format("Debugger  ({0} toggle, {1} switch)",
                    debuggerComponent.ToggleKey, debuggerComponent.SwitchTabKey), titleStyle);

            // 关闭按钮
            Rect closeRect = new Rect(sr.xMax - 24f * s, sr.y + 2f * s, 20f * s, 20f * s);
            if (GUI.Button(closeRect, "×", buttonStyle))
            {
                showFullWindow = false;
            }

            // 缩放角标
            GUI.Label(resizeH, "↘", miniLabelStyle);

            // 标签页
            float tabY = sr.y + 28f * s;
            float tabH = 26f * s;
            float tabW = (sr.width - 10f * s) / 3f;
            for (int i = 0; i < tabNames.Length; i++)
            {
                Rect tr = new Rect(sr.x + 5f * s + i * tabW, tabY, tabW, tabH);
                GUI.backgroundColor = tabIndex == i ? new Color(0.35f, 0.35f, 0.4f) : new Color(0.2f, 0.2f, 0.25f);
                if (GUI.Button(tr, tabNames[i], tabIndex == i ? tabSelectedStyle : tabNormalStyle))
                    tabIndex = i;
            }
            GUI.backgroundColor = Color.white;

            // 内容区域
            float cy = tabY + tabH + 4f * s;
            Rect cr = new Rect(sr.x + 5f * s, cy, sr.width - 10f * s, sr.yMax - cy - 5f * s);
            GUILayout.BeginArea(cr);

            if (tabIndex == 0) DrawOverview();
            else if (tabIndex == 1) DrawModules();
            else DrawLog();

            GUILayout.EndArea();
        }

        /// <summary>
        /// 获取综合考虑 WindowScale + DPI 的有效缩放比。
        /// 桌面 DPI≈96 → dpiScale≈1.0；手机 DPI≈400 → dpiScale≈4.2。
        /// </summary>
        private float EffectiveScale
        {
            get
            {
                if (debuggerComponent == null) return 1f;
                float s = debuggerComponent.WindowScale;
                if (debuggerComponent.AutoDpiScale && Screen.dpi > 0)
                {
                    s *= Mathf.Max(1f, Screen.dpi / 96f);
                }
                return s;
            }
        }

        private Rect ScaledRect()
        {
            float s = EffectiveScale;
            return new Rect(windowRect.x, windowRect.y, windowRect.width * s, windowRect.height * s);
        }

        // ============================================================
        // Overview
        // ============================================================

        private void DrawOverview()
        {
            overviewScroll = GUILayout.BeginScrollView(overviewScroll);

            GUILayout.Label(string.Format("FPS: {0:F1}    Frame: {1:F1} ms",
                avgFps, Time.unscaledDeltaTime * 1000f), labelStyle);
            GUILayout.Label(string.Format("Memory: {0:F1} MB",
                System.GC.GetTotalMemory(false) / 1048576f), labelStyle);
            GUILayout.Space(6);

            foreach (IDebuggerInfo info in moduleInfos)
            {
                string name = info.GetModuleName();
                string status = info.GetStatus();
                GUILayout.Label(string.Format("{0,-14}  {1}", name, status), labelStyle);
            }

            GUILayout.EndScrollView();
        }

        // ============================================================
        // Modules（展开式详情，对齐 RFDebugger）
        // ============================================================

        private void DrawModules()
        {
            modulesScroll = GUILayout.BeginScrollView(modulesScroll);

            foreach (IDebuggerInfo info in moduleInfos)
            {
                string name = info.GetModuleName();
                if (!moduleFoldouts.ContainsKey(name))
                    moduleFoldouts[name] = false;

                GUILayout.BeginHorizontal();
                moduleFoldouts[name] = GUILayout.Toggle(moduleFoldouts[name],
                    string.Format("{0}{1}  {2}", moduleFoldouts[name] ? "▼" : "▶", name, info.GetStatus()),
                    labelStyle, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                if (moduleFoldouts[name])
                {
                    Dictionary<string, string> details = info.GetDetails();
                    if (details != null && details.Count > 0)
                    {
                        foreach (KeyValuePair<string, string> kv in details)
                        {
                            GUILayout.Label(string.Format("    {0}: {1}", kv.Key, kv.Value), miniLabelStyle);
                        }
                    }
                    else
                    {
                        GUILayout.Label("    (no details)", miniLabelStyle);
                    }
                }
            }

            GUILayout.EndScrollView();
        }

        // ============================================================
        // Log（点击展开，对齐 RFDebugger）
        // ============================================================

        private void DrawLog()
        {
            float s = EffectiveScale;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", buttonStyle, GUILayout.Width(70f * s), GUILayout.Height(24f * s)))
            {
                ClearLogCache();
                selectedLogIndex = -1;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            List<LogEntry> logs = GetLogCache();
            if (logs == null || logs.Count == 0)
            {
                GUILayout.Label("No logs captured.", labelStyle);
                return;
            }

            logScroll = GUILayout.BeginScrollView(logScroll);

            for (int i = logs.Count - 1; i >= 0; i--)
            {
                LogEntry entry = logs[i];
                if (!ShouldShowLog(entry.Type)) continue;

                bool isSelected = (selectedLogIndex == i);
                Color entryColor = GetLogColor(entry.Type);
                Texture2D icon = GetLogIcon(entry.Type);

                GUILayout.BeginHorizontal();
                if (icon != null) GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(16));

                string displayMsg = string.Format("{0}  {1}", entry.Time, entry.Message);
                GUIStyle ms = new GUIStyle(logMsgStyle);
                ms.normal.textColor = entryColor;
                ms.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                GUILayout.Label(displayMsg, ms);
                Rect msgRect = GUILayoutUtility.GetLastRect();

                if (Event.current.type == EventType.MouseDown && msgRect.Contains(Event.current.mousePosition))
                {
                    selectedLogIndex = isSelected ? -1 : i;
                    Event.current.Use();
                }

                GUILayout.EndHorizontal();

                string location = ExtractLocation(entry.StackTrace);
                if (!string.IsNullOrEmpty(location))
                    GUILayout.Label(string.Format("    {0}", location), logLocStyle);

                if (isSelected && !string.IsNullOrEmpty(entry.StackTrace))
                    DrawStack(entry.StackTrace);

                GUILayout.Space(1);
            }

            GUILayout.EndScrollView();
        }

        private void DrawStack(string stackTrace)
        {
            foreach (string line in stackTrace.Split('\n'))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t)) continue;

                if (Regex.IsMatch(t, @"\(at .+?:\d+\)"))
                {
                    GUIStyle s = new GUIStyle(stackStyle);
                    s.normal.textColor = new Color(0.5f, 0.65f, 0.9f);
                    GUILayout.Label(string.Format("    {0}", t), s);
                }
                else
                {
                    GUILayout.Label(string.Format("    {0}", t), stackStyle);
                }
            }
        }

        // ============================================================
        // 辅助
        // ============================================================

        private void RefreshModuleInfos()
        {
            moduleInfos.Clear();

            try
            {
                // Log
                moduleInfos.Add(new ModuleDebugInfo("Log",
                    RFramework.RFrameworkLog.IsInitialized ? "Initialized" : "Not initialized",
                    new Dictionary<string, string>
                    {
                        { "Cached Logs", LogCount.ToString() }
                    }));

                // Base
                BaseComponent baseComponent = GameEntry.Base;
                if (baseComponent != null)
                    moduleInfos.Add(new ModuleDebugInfo("Base", "Running",
                        new Dictionary<string, string>
                        {
                            { "Frame Rate", baseComponent.FrameRate.ToString() },
                            { "Game Speed", string.Format("{0:0.##}", baseComponent.GameSpeed) },
                            { "Game Paused", baseComponent.IsGamePaused.ToString() },
                            { "Run In Background", baseComponent.RunInBackground.ToString() }
                        }));

                // Debugger
                DebuggerComponent debugger = GameEntry.Get<DebuggerComponent>();
                if (debugger != null)
                    moduleInfos.Add(new ModuleDebugInfo("Debugger",
                        debugger.ActiveWindow ? "Active" : "Inactive",
                        new Dictionary<string, string>
                        {
                            { "FPS", string.Format("{0:F1}", CurrentFps) },
                            { "Cached Logs", string.Format("{0} / {1}", debugger.LogCount, debugger.MaxLogEntries) }
                        }));

                // Event
                var eventM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IEventModule>();
                if (eventM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Event", "Active",
                        new Dictionary<string, string>
                        {
                            { "Handlers", eventM.HandlerCount.ToString() },
                            { "Queued Async Events", eventM.AsyncEventCount.ToString() }
                        }));

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
                    string procName = current != null
                        ? current.GetType().Name
                        : "(none)";
                    moduleInfos.Add(new ModuleDebugInfo("Procedure",
                        procName,
                        new Dictionary<string, string>
                        {
                            { "Registered", procM.ProcedureCount.ToString() },
                            { "Running Time", string.Format("{0:F1}s", procM.CurrentProcedureTime) }
                        }));
                }

                // Pool
                var poolM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IPoolModule>();
                if (poolM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Pool",
                        string.Format("Pools: {0}", poolM.PoolCount),
                        new Dictionary<string, string>
                        {
                            { "Object Pools", poolM.PoolCount.ToString() }
                        }));

                // Timer
                var timerM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.ITimerModule>();
                if (timerM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Timer",
                        string.Format("Timers: {0}", timerM.TimerCount), null));

                // Resource
                var resM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IResourceModule>();
                if (resM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Resource",
                        string.Format("Loaded: {0}  Loading: {1}", resM.LoadedAssetCount, resM.LoadingAssetCount), null));

                // Config
                var cfgM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IConfigModule>();
                if (cfgM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Config",
                        string.Format("Tables: {0}", cfgM.ConfigCount), null));

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
                    moduleInfos.Add(new ModuleDebugInfo("UI",
                        string.Format("Forms: {0}", uiM.UIFormCount), null));

                // Entity
                var entM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IEntityModule>();
                if (entM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Entity",
                        string.Format("Entities: {0}  Groups: {1}", entM.EntityCount, entM.EntityGroupCount), null));

                // Audio
                var audioM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IAudioModule>();
                if (audioM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Audio",
                        audioM.Muted ? "Muted" : string.IsNullOrEmpty(audioM.CurrentBgmAssetName)
                            ? "Idle"
                            : audioM.IsBgmPaused ? "Paused" : "Playing",
                        new Dictionary<string, string>
                        {
                            { "Cached Audio Assets", audioM.LoadedAudioAssetCount.ToString() },
                            { "Current BGM", audioM.CurrentBgmAssetName ?? "(none)" },
                            { "BGM Vol", string.Format("{0:F2}", audioM.BgmVolume) },
                            { "SFX Vol", string.Format("{0:F2}", audioM.SfxVolume) },
                            { "UI Vol", string.Format("{0:F2}", audioM.UIVolume) },
                            { "Muted", audioM.Muted.ToString() }
                        }));

                // Network
                var netM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.INetworkModule>();
                if (netM != null)
                {
                    RFramework.INetworkChannel defaultChannel = netM.DefaultChannel;
                    moduleInfos.Add(new ModuleDebugInfo("Network",
                        netM.IsConnected ? "Connected" : "Disconnected",
                        new Dictionary<string, string>
                        {
                            { "Channels", netM.ChannelCount.ToString() },
                            { "Default Channel", defaultChannel != null ? defaultChannel.Name : "(none)" },
                            { "Remote Endpoint", defaultChannel != null && !string.IsNullOrEmpty(defaultChannel.CurrentIP)
                                ? string.Format("{0}:{1}", defaultChannel.CurrentIP, defaultChannel.CurrentPort)
                                : "(none)" },
                            { "Heartbeat", defaultChannel != null
                                ? string.Format("{0:F1}s", defaultChannel.HeartbeatInterval)
                                : "0.0s" },
                            { "Auto Reconnect", (defaultChannel != null && defaultChannel.AutoReconnect).ToString() }
                        }));
                }

                // Localization
                var locM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.ILocalizationModule>();
                if (locM != null)
                    moduleInfos.Add(new ModuleDebugInfo("Localization",
                        locM.CurrentLanguage ?? "(none)",
                        new Dictionary<string, string>
                        {
                            { "Loaded Packs", locM.LoadedLanguageCount.ToString() },
                            { "Supported Languages", locM.SupportedLanguages.Count.ToString() }
                        }));

                // WebRequest
                var webM = RFramework.RFrameworkModuleEntry.GetModule<RFramework.IWebRequestModule>();
                if (webM != null)
                    moduleInfos.Add(new ModuleDebugInfo("WebRequest",
                        string.Format("Active: {0}  Queued: {1}", webM.ActiveRequestCount, webM.QueuedRequestCount), null));
            }
            catch (Exception e)
            {
                Log.Warning("DebuggerOverlay.RefreshModuleInfos: {0}", e.Message);
            }
        }

        private static string ExtractLocation(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "";

            foreach (string line in stackTrace.Split('\n'))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (t.Contains("(at ") && (t.Contains("Assets/") || t.Contains("assets/")))
                {
                    Match m = Regex.Match(t, @"\(at (.+?):(\d+)\)");
                    if (m.Success)
                    {
                        string path = m.Groups[1].Value;
                        int idx = path.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0) path = path.Substring(idx + 1);
                        return string.Format("{0}:{1}", path, m.Groups[2].Value);
                    }
                }
            }
            return "";
        }

        private bool ShouldShowLog(LogType type)
        {
            if (debuggerComponent == null) return true;
            switch (type)
            {
                case LogType.Error: case LogType.Exception: case LogType.Assert:
                    return debuggerComponent.ErrorFilter;
                case LogType.Warning:
                    return debuggerComponent.WarningFilter;
                default:
                    return debuggerComponent.InfoFilter;
            }
        }

        private static Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error: case LogType.Exception: case LogType.Assert:
                    return new Color(1f, 0.35f, 0.3f);
                case LogType.Warning:
                    return new Color(1f, 0.85f, 0.3f);
                default:
                    return new Color(0.9f, 0.9f, 0.9f);
            }
        }

        // ============================================================
        // 图标
        // ============================================================

        private static void EnsureIcons()
        {
            if (iconsInited) return;
            iconError = MakeTex(10, 10, new Color(0.9f, 0.2f, 0.2f));
            iconWarning = MakeTex(10, 10, new Color(0.9f, 0.75f, 0.1f));
            iconInfo = MakeTex(10, 10, new Color(0.6f, 0.6f, 0.6f));
            iconsInited = true;
        }

        private static Texture2D GetLogIcon(LogType type)
        {
            return type switch
            {
                LogType.Error or LogType.Exception or LogType.Assert => iconError,
                LogType.Warning => iconWarning,
                _ => iconInfo
            };
        }

        // ============================================================
        // 样式
        // ============================================================

        private void InitStyles()
        {
            float s = EffectiveScale;

            badgeBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            badgeLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 0, 0, 0)
            };

            windowBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = windowBgTex },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                fontSize = Mathf.RoundToInt(14 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            tabNormalStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(13 * s),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };

            tabSelectedStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(13 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
                fontSize = Mathf.RoundToInt(14 * s),
                wordWrap = true
            };

            miniLabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f) },
                fontSize = Mathf.RoundToInt(12 * s),
                wordWrap = true
            };

            logMsgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * s),
                wordWrap = true
            };

            logLocStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * s),
                normal = { textColor = new Color(0.45f, 0.45f, 0.45f) },
                wordWrap = true
            };

            stackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * s),
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(14 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 0, 2)
            };

            stylesInited = true;
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D r = new Texture2D(w, h);
            r.SetPixels(pix);
            r.Apply();
            return r;
        }

        // ============================================================
        // 内部类
        // ============================================================

        private sealed class ModuleDebugInfo : IDebuggerInfo
        {
            private string name;
            private string status;
            private Dictionary<string, string> details;

            public ModuleDebugInfo(string name, string status, Dictionary<string, string> details)
            {
                this.name = name;
                this.status = status;
                this.details = details;
            }

            public string GetModuleName() => name;
            public string GetStatus() => status;
            public Dictionary<string, string> GetDetails() => details;
        }
    }
}

#endif
