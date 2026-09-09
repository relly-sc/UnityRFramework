#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample.UI.Editor
{
    /// <summary>
    /// 生成 Sample.UI 的独立验收场景和 Resources Prefab。
    /// </summary>
    public static class UIAcceptanceSceneGenerator
    {
        private const string Root = "Assets/UnityRFramework/Samples/Sample.UI";
        private const string ResourcesRoot = Root + "/GameAssets/Resources/UIAcceptance";
        private const string ScenePath = Root + "/GameAssets/Scenes/UIAcceptance.unity";
        private const string FrameworkPrefabPath = "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab";

        [MenuItem("UnityRFramework/Samples/生成 Sample.UI 验收场景")]
        public static void Generate()
        {
            EnsureFolder(ResourcesRoot);
            EnsureFolder(Root + "/GameAssets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera();
            GameObject framework = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FrameworkPrefabPath));
            framework.name = "UnityRFramework";

            UIComponent uiComponent = framework.GetComponentInChildren<UIComponent>();
            Canvas canvas = uiComponent.transform.Find("Canvas").GetComponent<Canvas>();

            CreateFormPrefab("PanelA", "普通面板 A", new Color(0.16f, 0.28f, 0.48f, 0.96f));
            CreateFormPrefab("PanelB", "普通面板 B", new Color(0.18f, 0.44f, 0.30f, 0.96f));
            CreateFormPrefab("FullScreen", "全屏弹窗", new Color(0.48f, 0.20f, 0.18f, 0.98f));
            CreateCanvasFormPrefab();

            UIAcceptanceController controller = CreateController(canvas.transform);
            Selection.activeGameObject = controller.gameObject;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Sample.UI 验收场景已生成：" + ScenePath);
        }

        private static void CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static UIAcceptanceController CreateController(Transform parent)
        {
            GameObject root = new GameObject("UIAcceptance", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            UIAcceptanceController controller = root.AddComponent<UIAcceptanceController>();

            Button panelA = CreateButton(root.transform, "打开普通面板 A", new Vector2(180, -80));
            Button panelB = CreateButton(root.transform, "打开普通面板 B", new Vector2(180, -140));
            Button popup = CreateButton(root.transform, "打开全屏弹窗", new Vector2(180, -200));
            Button independentCanvas = CreateButton(root.transform, "打开独立画布界面",
                new Vector2(180, -260));
            Button closeTop = CreateButton(root.transform, "关闭顶部界面 / 返回", new Vector2(180, -320));
            Button closeAll = CreateButton(root.transform, "关闭全部界面", new Vector2(180, -380));
            Button restart = CreateButton(root.transform, "软重启框架", new Vector2(180, -440));
            Text status = CreateText(root.transform, "Status", "准备完成", new Vector2(570, -95), 22);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("openPanelAButton").objectReferenceValue = panelA;
            serialized.FindProperty("openPanelBButton").objectReferenceValue = panelB;
            serialized.FindProperty("openFullScreenButton").objectReferenceValue = popup;
            serialized.FindProperty("openIndependentCanvasButton").objectReferenceValue = independentCanvas;
            serialized.FindProperty("closeTopButton").objectReferenceValue = closeTop;
            serialized.FindProperty("closeAllButton").objectReferenceValue = closeAll;
            serialized.FindProperty("restartButton").objectReferenceValue = restart;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static void CreateFormPrefab(string name, string title, Color color)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500, 240);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.55f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            root.GetComponent<Image>().color = color;
            UIAcceptanceFormLogic logic = root.AddComponent<UIAcceptanceFormLogic>();
            Text titleText = CreateText(root.transform, "Title", title, Vector2.zero, 30);
            Text lifecycleText = CreateText(root.transform, "Lifecycle", "等待打开", new Vector2(0, -60), 20);
            SerializedObject serialized = new SerializedObject(logic);
            serialized.FindProperty("titleText").objectReferenceValue = titleText;
            serialized.FindProperty("lifecycleText").objectReferenceValue = lifecycleText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ResourcesRoot + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreateCanvasFormPrefab()
        {
            GameObject root = new GameObject("IndependentCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 250;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            UIAcceptanceFormLogic logic = root.AddComponent<UIAcceptanceFormLogic>();
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.55f);
            panelRect.sizeDelta = new Vector2(560, 260);
            panel.GetComponent<Image>().color = new Color(0.38f, 0.22f, 0.50f, 0.98f);

            Text titleText = CreateText(panel.transform, "Title", "独立画布界面", Vector2.zero, 30);
            Text lifecycleText = CreateText(panel.transform, "Lifecycle", "等待打开", new Vector2(0, -60), 20);
            SerializedObject serialized = new SerializedObject(logic);
            serialized.FindProperty("titleText").objectReferenceValue = titleText;
            serialized.FindProperty("lifecycleText").objectReferenceValue = lifecycleText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ResourcesRoot + "/IndependentCanvas.prefab");
            Object.DestroyImmediate(root);
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(360, 44);
            buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.35f, 1);
            Text text = CreateText(buttonObject.transform, "Label", label, Vector2.zero, 18);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            return buttonObject.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 position, int size)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(720, 48);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}

#endif
