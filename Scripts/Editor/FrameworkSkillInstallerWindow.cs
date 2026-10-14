using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityRFramework.Editor
{
    /// <summary>框架 AI 技能的项目、全局安装窗口。</summary>
    internal sealed class FrameworkSkillInstallerWindow : EditorWindow
    {
        private string sourceDirectory;
        private FrameworkSkillAgent[] agents;
        private FrameworkSkillInstallState[] states;
        private string[] stateErrors;
        private string result;
        private MessageType resultType;
        private Vector2 scroll;
        [SerializeField] private string customSkillsRoot = "";

        [MenuItem("UnityRFramework/安装 AI 技能")]
        private static void Open()
        {
            GetWindow<FrameworkSkillInstallerWindow>(FrameworkSkillInstaller.DisplayName).Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(520f, 360f);
            titleContent = new GUIContent(FrameworkSkillInstaller.DisplayName);
            RefreshState();
        }

        private void OnFocus() { RefreshState(); }

        private void RefreshState()
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                PackageInfo package = PackageInfo.FindForAssembly(typeof(FrameworkSkillInstallerWindow).Assembly);
                string start = package != null ? package.resolvedPath
                    : Path.GetDirectoryName(Path.GetFullPath(Path.Combine(projectRoot,
                        AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)))));
                sourceDirectory = FrameworkSkillInstaller.FindSourceDirectory(start);
                agents = FrameworkSkillInstaller.GetAgents(projectRoot,
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetEnvironmentVariable("KIMI_CODE_HOME"));
                states = new FrameworkSkillInstallState[agents.Length * 2];
                stateErrors = new string[states.Length];
                for (int i = 0; i < states.Length; i++)
                {
                    FrameworkSkillAgent agent = agents[i / 2];
                    string path = i % 2 == 0 ? agent.ProjectPath : agent.GlobalPath;
                    try { states[i] = FrameworkSkillInstaller.GetState(path, sourceDirectory); }
                    catch (Exception exception) { stateErrors[i] = exception.Message; }
                }
            }
            catch (Exception exception)
            {
                agents = null;
                result = exception.Message;
                resultType = MessageType.Error;
            }
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(FrameworkSkillInstaller.DisplayName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("Refresh"), "刷新安装状态"),
                    EditorStyles.toolbarButton, GUILayout.Width(30f))) RefreshState();
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, resultType);
            if (agents == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < agents.Length; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(agents[i].Name, EditorStyles.boldLabel);
                    DrawScope(agents[i], i, false);
                    DrawScope(agents[i], i, true);
                }
                GUILayout.Space(4f);
            }
            DrawCustomDirectory();
            EditorGUILayout.EndScrollView();
        }

        private void DrawScope(FrameworkSkillAgent agent, int index, bool global)
        {
            string scope = global ? "全局" : "项目";
            string path = global ? agent.GlobalPath : agent.ProjectPath;
            FrameworkSkillInstallState state = states[index * 2 + (global ? 1 : 0)];
            DrawDestination(agent.Name, scope, path, state, stateErrors[index * 2 + (global ? 1 : 0)]);
            EditorGUILayout.SelectableLabel(Path.Combine(path, FrameworkSkillInstaller.SkillName),
                EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private void DrawCustomDirectory()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("自定义安装目录", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    customSkillsRoot = EditorGUILayout.TextField(new GUIContent("技能父目录",
                        "选择 Agent 的 skills 目录，工具会在其中创建 unityrframework-skills 子目录。"), customSkillsRoot);
                    if (GUILayout.Button("浏览", GUILayout.Width(60f), GUILayout.Height(22f)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("选择技能父目录", customSkillsRoot, "");
                        if (!string.IsNullOrEmpty(selected)) customSkillsRoot = selected;
                    }
                }
                FrameworkSkillInstallState state = FrameworkSkillInstallState.NotInstalled;
                string pathError = string.IsNullOrWhiteSpace(customSkillsRoot) ? "请先选择技能父目录。" : null;
                if (pathError == null)
                {
                    try { state = FrameworkSkillInstaller.GetState(customSkillsRoot, sourceDirectory); }
                    catch (Exception exception) { pathError = exception.Message; }
                }
                DrawDestination("自定义目录", "自定义", customSkillsRoot, state, pathError);
            }
        }

        private void DrawDestination(string agentName, string scope, string path, FrameworkSkillInstallState state,
            string pathError = null)
        {
            string conflict = pathError;
            if (conflict == null)
            {
                try { conflict = FrameworkSkillInstaller.GetScopeConflict(path, sourceDirectory, agents); }
                catch (Exception exception) { conflict = exception.Message; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(scope, GUILayout.Width(50f));
                GUILayout.Label(pathError == null ? GetStatusLabel(state) : "目录未就绪", GUILayout.Width(100f));
                GUILayout.FlexibleSpace();
                bool managed = state == FrameworkSkillInstallState.Installed
                    || state == FrameworkSkillInstallState.UpdateAvailable;
                using (new EditorGUI.DisabledScope(state == FrameworkSkillInstallState.Installed
                    || !string.IsNullOrEmpty(conflict)))
                {
                    bool overwrite = state == FrameworkSkillInstallState.Modified || state == FrameworkSkillInstallState.Unmanaged;
                    string label = overwrite ? "覆盖更新" : state == FrameworkSkillInstallState.UpdateAvailable ? "更新" : "安装";
                    if (GUILayout.Button(label, GUILayout.Width(70f), GUILayout.Height(22f)))
                    {
                        if (!overwrite || EditorUtility.DisplayDialog("覆盖更新 UnityRFramework Skills",
                            "将用当前框架的技能完整替换以下目录，目录内的本地修改也会丢失。请先备份需要保留的内容。\n"
                            + Path.Combine(path, FrameworkSkillInstaller.SkillName), "覆盖更新", "取消"))
                            Execute(() => result = "安装完成：" + FrameworkSkillInstaller.Install(sourceDirectory, path, overwrite, agents));
                    }
                }
                using (new EditorGUI.DisabledScope(!managed))
                {
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("TreeEditor.Trash"), "卸载 UnityRFramework Skills"),
                        GUILayout.Width(30f), GUILayout.Height(22f))
                        && EditorUtility.DisplayDialog("卸载 UnityRFramework Skills",
                            "将卸载 " + agentName + " 的" + scope + "技能：\n"
                            + Path.Combine(path, FrameworkSkillInstaller.SkillName), "卸载", "取消"))
                    {
                        Execute(() =>
                        {
                            FrameworkSkillInstaller.Uninstall(path, sourceDirectory);
                            result = "已卸载 " + agentName + " 的" + scope + "框架技能。";
                        });
                    }
                }
            }
            if (!string.IsNullOrEmpty(conflict))
                EditorGUILayout.HelpBox(conflict, pathError != null && !string.IsNullOrWhiteSpace(path)
                    ? MessageType.Error : MessageType.Info);
        }

        private void Execute(Action action)
        {
            try { action(); resultType = MessageType.Info; }
            catch (Exception exception) { result = exception.Message; resultType = MessageType.Error; }
            RefreshState();
        }

        private static string GetStatusLabel(FrameworkSkillInstallState state)
        {
            switch (state)
            {
                case FrameworkSkillInstallState.Installed: return "已安装";
                case FrameworkSkillInstallState.UpdateAvailable: return "可更新";
                case FrameworkSkillInstallState.Modified: return "有本地修改";
                case FrameworkSkillInstallState.Unmanaged: return "其他方式安装";
                default: return "未安装";
            }
        }
    }
}
