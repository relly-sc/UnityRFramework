#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion.UI.Demo.Editor
{
    /// <summary>
    /// 生成可直接运行的 UI 交互与列表验收场景。
    /// </summary>
    internal static class UIStage5AcceptanceSceneGenerator
    {
        private const string Root = "Assets/UnityRFramework/Samples/Expansion.UI.Demo";
        private const string ScenePath = Root + "/GameAssets/Scenes/UIStage5Acceptance.unity";
        private const string FrameworkPrefabPath =
            "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab";

        [MenuItem("UnityRFramework/Samples/生成 Expansion.UI.Demo 验收场景")]
        private static void Generate()
        {
            EditorSceneManager.SaveOpenScenes();
            EnsureFolder(Root + "/GameAssets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera();
            GameObject frameworkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FrameworkPrefabPath);
            if (frameworkPrefab == null)
            {
                throw new System.InvalidOperationException("未找到框架 Prefab：" + FrameworkPrefabPath);
            }

            GameObject framework = (GameObject)PrefabUtility.InstantiatePrefab(frameworkPrefab);
            framework.name = "UnityRFramework";
            Canvas canvas = framework.GetComponentInChildren<UIComponent>().GetComponentInChildren<Canvas>();
            EnsureEventSystem();

            GameObject root = CreateUI("UIStage5Acceptance", canvas.transform, typeof(Image));
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color32(28, 32, 38, 255);

            Text title = CreateText(root.transform, "Title", "UI 交互与列表验收",
                new Vector2(0f, -32f), new Vector2(900f, 48f), 28, TextAnchor.MiddleCenter);
            SetTopCenter(title.rectTransform);

            GameObject controls = CreateUI("Controls", root.transform, typeof(Image));
            RectTransform controlsRect = controls.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(0f, 1f);
            controlsRect.pivot = new Vector2(0f, 0.5f);
            controlsRect.anchoredPosition = new Vector2(28f, 0f);
            controlsRect.sizeDelta = new Vector2(360f, -120f);
            controls.GetComponent<Image>().color = new Color32(42, 48, 56, 255);

            Button request = CreateButton(controls.transform, "加入确认队列（连续点三次）", -28f);
            Button reject = CreateButton(controls.transform, "拒绝当前确认", -84f);
            Button cancelAll = CreateButton(controls.transform, "取消全部确认", -140f);
            Button toast = CreateButton(controls.transform, "连续显示三条提示", -214f);
            Button thousand = CreateButton(controls.transform, "显示 1000 项", -288f);
            Button ten = CreateButton(controls.transform, "显示 10 项", -344f);
            Button last = CreateButton(controls.transform, "滚动到最后一项", -400f);
            Button restart = CreateButton(controls.transform, "软重启框架", -474f);

            Text status = CreateText(controls.transform, "Status", string.Empty,
                new Vector2(20f, 22f), new Vector2(320f, 118f), 15, TextAnchor.LowerLeft);
            status.rectTransform.anchorMin = new Vector2(0f, 0f);
            status.rectTransform.anchorMax = new Vector2(0f, 0f);
            status.rectTransform.pivot = new Vector2(0f, 0f);

            VirtualizedVerticalList list = CreateList(root.transform);
            ConfirmationDialogQueue confirmation = CreateConfirmation(root.transform);
            ToastQueue toastQueue = CreateToast(root.transform);

            UIStage5AcceptanceController controller = root.AddComponent<UIStage5AcceptanceController>();
            SetObject(controller, "confirmationQueue", confirmation);
            SetObject(controller, "toastQueue", toastQueue);
            SetObject(controller, "virtualList", list);
            SetObject(controller, "requestConfirmationButton", request);
            SetObject(controller, "cancelCurrentButton", reject);
            SetObject(controller, "cancelAllButton", cancelAll);
            SetObject(controller, "enqueueToastsButton", toast);
            SetObject(controller, "showThousandItemsButton", thousand);
            SetObject(controller, "showTenItemsButton", ten);
            SetObject(controller, "scrollToLastButton", last);
            SetObject(controller, "restartButton", restart);
            SetObject(controller, "statusText", status);

            Selection.activeGameObject = root;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Expansion UI 交互与列表验收场景已生成：" + ScenePath);
        }

        private static void CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static ConfirmationDialogQueue CreateConfirmation(Transform parent)
        {
            GameObject host = new GameObject("ConfirmationQueue", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());
            ConfirmationDialogQueue queue = host.AddComponent<ConfirmationDialogQueue>();

            GameObject view = CreateUI("View", host.transform, typeof(Image), typeof(ModalBackdrop));
            Stretch(view.GetComponent<RectTransform>());
            view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            GameObject panel = CreateUI("Dialog", view.transform, typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, 230f);
            panel.GetComponent<Image>().color = new Color32(246, 247, 249, 255);

            Text message = CreateText(panel.transform, "Message", "确认操作",
                new Vector2(0f, 38f), new Vector2(420f, 90f), 22, TextAnchor.MiddleCenter);
            message.color = new Color32(32, 36, 42, 255);
            Button confirm = CreateDialogButton(panel.transform, "确认", new Vector2(-110f, -65f),
                new Color32(43, 113, 83, 255));
            Button cancel = CreateDialogButton(panel.transform, "取消", new Vector2(110f, -65f),
                new Color32(116, 54, 58, 255));

            SetObject(queue, "viewRoot", view);
            SetObject(queue, "messageText", message);
            SetObject(queue, "confirmButton", confirm);
            SetObject(queue, "cancelButton", cancel);
            SetObject(queue, "backdrop", view.GetComponent<ModalBackdrop>());
            view.SetActive(false);
            return queue;
        }

        private static ToastQueue CreateToast(Transform parent)
        {
            GameObject host = new GameObject("ToastQueue", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());
            ToastQueue queue = host.AddComponent<ToastQueue>();
            GameObject view = CreateUI("View", host.transform, typeof(Image));
            RectTransform rect = view.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -92f);
            rect.sizeDelta = new Vector2(420f, 52f);
            view.GetComponent<Image>().color = new Color32(26, 28, 32, 242);
            Text message = CreateText(view.transform, "Message", string.Empty, Vector2.zero,
                new Vector2(390f, 44f), 18, TextAnchor.MiddleCenter);
            SetObject(queue, "viewRoot", view);
            SetObject(queue, "messageText", message);
            view.SetActive(false);
            return queue;
        }

        private static VirtualizedVerticalList CreateList(Transform parent)
        {
            GameObject scroll = CreateUI("VirtualizedList", parent, typeof(Image), typeof(ScrollRect),
                typeof(VirtualizedVerticalList));
            RectTransform scrollRectTransform = scroll.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(420f, 40f);
            scrollRectTransform.offsetMax = new Vector2(-40f, -110f);
            scroll.GetComponent<Image>().color = new Color32(21, 24, 29, 255);

            GameObject viewportObject = CreateUI("Viewport", scroll.transform, typeof(Image), typeof(Mask));
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -12f);
            viewportObject.GetComponent<Image>().color = Color.white;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = CreateUI("Content", viewport, typeof(RectTransform));
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            GameObject templateObject = CreateUI("ItemTemplate", content, typeof(Image));
            RectTransform template = templateObject.GetComponent<RectTransform>();
            template.anchorMin = new Vector2(0f, 1f);
            template.anchorMax = new Vector2(1f, 1f);
            template.pivot = new Vector2(0.5f, 1f);
            template.sizeDelta = new Vector2(0f, 46f);
            template.GetComponent<Image>().color = new Color32(55, 63, 73, 255);
            CreateText(template, "Label", "虚拟列表项", Vector2.zero,
                new Vector2(500f, 40f), 17, TextAnchor.MiddleLeft);

            ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            VirtualizedVerticalList list = scroll.GetComponent<VirtualizedVerticalList>();
            SetObject(list, "scrollRect", scrollRect);
            SetObject(list, "viewport", viewport);
            SetObject(list, "content", content);
            SetObject(list, "itemTemplate", template);
            SetFloat(list, "itemHeight", 46f);
            SetFloat(list, "spacing", 6f);
            SetFloat(list, "paddingTop", 4f);
            SetFloat(list, "paddingBottom", 4f);
            return list;
        }

        private static Button CreateButton(Transform parent, string label, float y)
        {
            GameObject node = CreateUI(label, parent, typeof(Image), typeof(Button));
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, y);
            rect.sizeDelta = new Vector2(320f, 44f);
            node.GetComponent<Image>().color = new Color32(64, 84, 104, 255);
            Text text = CreateText(node.transform, "Label", label, Vector2.zero,
                new Vector2(300f, 40f), 17, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return node.GetComponent<Button>();
        }

        private static Button CreateDialogButton(Transform parent, string label, Vector2 position, Color color)
        {
            GameObject node = CreateUI(label, parent, typeof(Image), typeof(Button));
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(170f, 48f);
            node.GetComponent<Image>().color = color;
            Text text = CreateText(node.transform, "Label", label, Vector2.zero,
                new Vector2(160f, 44f), 18, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return node.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 position,
            Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject node = CreateUI(name, parent, typeof(Text));
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = node.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUI(string name, Transform parent, params System.Type[] components)
        {
            GameObject node = new GameObject(name, components);
            node.transform.SetParent(parent, false);
            return node;
        }

        private static void SetObject(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string name, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetTopCenter(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
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
