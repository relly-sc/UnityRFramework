using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion.R3Demo.Editor
{
    /// <summary>在当前导入目录生成示例，不依赖 Samples 的固定版本号。</summary>
    public static class R3DemoSceneGenerator
    {
        private static readonly Color Ink = new Color(0.12f, 0.15f, 0.17f);
        private static readonly Color Accent = new Color(0.12f, 0.42f, 0.38f);

        /// <summary>当前导入目录中的示例场景。</summary>
        public static string ScenePath => RootFor(typeof(R3DemoSceneGenerator), "/Scripts/Editor/") +
            "/GameAssets/Scenes/R3Demo.unity";

        /// <summary>确认后生成或覆盖示例场景。</summary>
        [MenuItem("UnityRFramework/Expansion/R3/生成响应式交互示例")]
        public static void Generate()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("重新生成 R3 示例",
                    "将覆盖现有 R3Demo 场景。是否继续？", "生成", "取消")) return;
            GenerateScene();
            Debug.Log("R3 示例已生成：" + ScenePath);
        }

        /// <summary>打开示例，先确认用户未保存的场景。</summary>
        [MenuItem("UnityRFramework/Expansion/R3/打开响应式交互示例")]
        public static void Open()
        {
            if (!File.Exists(ScenePath)) Generate();
            if (File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>在独立附加场景中生成资源，保留用户当前场景。调用方负责确认覆盖。</summary>
        public static void GenerateScene()
        {
            string path = ScenePath;
            string prefabPath = RootFor(typeof(GameEntry), "/Scripts/Runtime/") + "/Prefabs/UnityRFramework.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("未找到框架入口预制体：" + prefabPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PrefabUtility.InstantiatePrefab(prefab, scene);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.94f, 0.95f, 0.96f);
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

                GameObject canvasObject = new GameObject("R3 示例", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 900);
                scaler.matchWidthOrHeight = 0;
                RectTransform viewport = Stretch(new GameObject("交互区域", typeof(RectTransform), typeof(RectMask2D), typeof(Image)), canvasObject.transform);
                viewport.GetComponent<Image>().color = Color.clear;
                viewport.offsetMin = new Vector2(32, 32);
                viewport.offsetMax = new Vector2(-32, -32);
                RectTransform root = Stretch(new GameObject("内容", typeof(RectTransform)), viewport);
                root.anchorMin = new Vector2(0, 1);
                root.pivot = new Vector2(0.5f, 1);
                var fit = root.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = root;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 36;
                VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                R3DemoController controller = root.gameObject.AddComponent<R3DemoController>();
                Text title = Text(root, "R3 响应式交互", 34, 42);
                title.fontStyle = FontStyle.Bold;
                Text status = Text(root, "准备运行", 26, 72);
                GameObject commands = new GameObject("操作", typeof(RectTransform), typeof(GridLayoutGroup));
                commands.transform.SetParent(root, false);
                GridLayoutGroup grid = commands.GetComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                grid.cellSize = new Vector2(590, 60);
                grid.spacing = new Vector2(16, 8);
                commands.AddComponent<LayoutElement>().preferredHeight = 128;
                Button add = Button(commands.transform, "积分 +1");
                Button send = Button(commands.transform, "发送框架事件");
                Button toggle = Button(commands.transform, "打开 / 关闭界面");
                Button reset = Button(commands.transform, "重置数据");

                GameObject panel = new GameObject("响应式窗口", typeof(RectTransform), typeof(VerticalLayoutGroup));
                panel.transform.SetParent(root, false);
                var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
                panelLayout.spacing = 8;
                panelLayout.childControlWidth = panelLayout.childControlHeight = true;
                panelLayout.childForceExpandWidth = true;
                panelLayout.childForceExpandHeight = false;
                R3DemoForm form = panel.AddComponent<R3DemoForm>();
                Text score = Text(panel.transform, "积分：0", 32, 48);
                Text eventText = Text(panel.transform, "最近事件：无", 26, 40);
                DefaultControls.Resources resources = Resources();
                GameObject inputObject = DefaultControls.CreateInputField(resources);
                inputObject.name = "搜索关键词";
                inputObject.transform.SetParent(panel.transform, false);
                inputObject.AddComponent<LayoutElement>().preferredHeight = 60;
                InputField input = inputObject.GetComponent<InputField>();
                input.characterLimit = 80;
                SetText(input.textComponent, 28);
                Text placeholder = (Text)input.placeholder;
                SetText(placeholder, 28);
                placeholder.text = "搜索关键词";
                placeholder.color = new Color(0.4f, 0.44f, 0.48f);
                Text result = Text(panel.transform, "搜索结果：无关键词", 26, 64);
                GameObject allowObject = DefaultControls.CreateToggle(resources);
                allowObject.name = "允许领取";
                allowObject.transform.SetParent(panel.transform, false);
                allowObject.AddComponent<LayoutElement>().preferredHeight = 48;
                Toggle allow = allowObject.GetComponent<Toggle>();
                allow.isOn = true;
                Text toggleLabel = allowObject.GetComponentInChildren<Text>();
                SetText(toggleLabel, 28);
                toggleLabel.text = "允许领取";
                RectTransform background = (RectTransform)allow.targetGraphic.transform;
                background.anchorMin = background.anchorMax = new Vector2(0, 0.5f);
                background.anchoredPosition = new Vector2(24, 0);
                background.sizeDelta = new Vector2(40, 40);
                toggleLabel.rectTransform.offsetMin = new Vector2(58, 0);
                Button claim = Button(panel.transform, "领取奖励（消耗 3 积分）");
                Set(form, "scoreText", score, "eventText", eventText, "searchResultText", result,
                    "searchInput", input, "allowClaimToggle", allow, "claimButton", claim);
                Set(controller, "form", form, "statusText", status, "addScoreButton", add,
                    "sendEventButton", send, "togglePanelButton", toggle, "resetButton", reset);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("无法保存 R3 示例场景。");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static string RootFor(Type type, string suffix)
        {
            foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script.GetClass() != type) continue;
                int index = path.LastIndexOf(suffix, StringComparison.Ordinal);
                if (index >= 0) return path.Substring(0, index);
            }
            throw new InvalidOperationException("未找到 " + type.Name + " 的安装目录。");
        }

        private static RectTransform Stretch(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text Text(Transform parent, string value, int size, float height)
        {
            GameObject go = new GameObject(value, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            SetText(text, size);
            text.text = value;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static void SetText(Text text, int size)
        {
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Button Button(Transform parent, string label)
        {
            GameObject go = DefaultControls.CreateButton(Resources());
            go.name = label;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 60;
            go.GetComponent<Image>().color = Accent;
            Text text = go.GetComponentInChildren<Text>();
            SetText(text, 28);
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            return go.GetComponent<Button>();
        }

        private static DefaultControls.Resources Resources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd")
        };

        private static void Set(UnityEngine.Object target, params object[] pairs)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2)
                serialized.FindProperty((string)pairs[i]).objectReferenceValue = (UnityEngine.Object)pairs[i + 1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
