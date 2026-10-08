#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample.Security.Editor
{
    /// <summary>生成 Protected 数值独立验收场景。</summary>
    public static class SecurityAcceptanceSceneGenerator
    {
        private const string Root = "Assets/UnityRFramework/Samples/Sample.Security";
        private const string ScenePath = Root + "/GameAssets/Scenes/SecurityAcceptance.unity";
        private const string FrameworkPrefabPath = "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab";
        [MenuItem("UnityRFramework/Samples/生成安全数值验收场景")]
        public static void Generate()
        {
            EnsureFolder(Root + "/GameAssets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCameraAndEventSystem();

            GameObject framework = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FrameworkPrefabPath));
            framework.name = "UnityRFramework";
            Canvas canvas = framework.GetComponentInChildren<UIComponent>(true)
                .transform.Find("Canvas").GetComponent<Canvas>();
            SecurityAcceptanceController controller = CreateInterface(canvas.transform);
            Selection.activeGameObject = controller.gameObject;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("安全数值验收场景已生成：" + ScenePath);
        }

        private static SecurityAcceptanceController CreateInterface(Transform parent)
        {
            GameObject root = new GameObject("安全数值验收", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 1f);

            GameObject panel = new GameObject("内容面板", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1000f, 1000f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.19f, 1f);
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 32, 32);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            SecurityAcceptanceController controller = root.AddComponent<SecurityAcceptanceController>();
            CreateLabel(panel.transform, "标题", "安全数值验收", 44, 76, TextAnchor.MiddleCenter);
            CreateLabel(panel.transform, "说明", "普通整数用于对照扫描；ProtectedInt 只保存随机掩码后的编码值。\n点击增加数值后，可分别观察两种值的变化。", 24, 116, TextAnchor.MiddleLeft);
            Text normal = CreateLabel(panel.transform, "普通值", "普通整数：1000", 30, 64, TextAnchor.MiddleLeft);
            Text protectedValue = CreateLabel(panel.transform, "保护值", "ProtectedInt：1000", 30, 64, TextAnchor.MiddleLeft);
            Text tamper = CreateLabel(panel.transform, "篡改事件", "篡改事件：暂无", 24, 64, TextAnchor.MiddleLeft);
            Button increase = CreateButton(panel.transform, "增加 10", 78);
            Button reset = CreateButton(panel.transform, "重置数值", 78);
            Button recreate = CreateButton(panel.transform, "重新生成保护编码", 78);

            SerializedObject serialized = new SerializedObject(controller);
            Assign(serialized, "normalValueText", normal);
            Assign(serialized, "protectedValueText", protectedValue);
            Assign(serialized, "tamperText", tamper);
            Assign(serialized, "increaseButton", increase);
            Assign(serialized, "resetButton", reset);
            Assign(serialized, "recreateButton", recreate);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static Text CreateLabel(Transform parent, string name, string value, int fontSize,
            float height, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            LayoutElement element = textObject.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = BuiltinFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, float height)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.34f, 0.48f, 1f);
            buttonObject.GetComponent<LayoutElement>().preferredHeight = height;
            Text text = CreateLabel(buttonObject.transform, "文字", label, 28, height, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return buttonObject.GetComponent<Button>();
        }

        private static void CreateCameraAndEventSystem()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void Assign(SerializedObject serialized, string property, Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
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
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

#endif
