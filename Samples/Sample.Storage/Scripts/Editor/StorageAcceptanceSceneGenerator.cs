#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample.Storage.Editor
{
    /// <summary>生成 Storage 独立验收场景。</summary>
    public static class StorageAcceptanceSceneGenerator
    {
        private const string Root = "Assets/UnityRFramework/Samples/Sample.Storage";
        private const string ScenePath = Root + "/GameAssets/Scenes/StorageAcceptance.unity";
        private const string FrameworkPrefabPath = "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab";

        [MenuItem("UnityRFramework/Samples/生成存档验收场景")]
        public static void Generate()
        {
            EnsureFolder(Root + "/GameAssets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCameraAndEventSystem();

            GameObject framework = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FrameworkPrefabPath));
            framework.name = "UnityRFramework";
            StorageComponent storage = framework.GetComponentInChildren<StorageComponent>(true);
            ConfigureStorage(storage);

            Canvas canvas = framework.GetComponentInChildren<UIComponent>(true)
                .transform.Find("Canvas").GetComponent<Canvas>();
            StorageAcceptanceController controller = CreateInterface(canvas.transform);
            Selection.activeGameObject = controller.gameObject;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("存档验收场景已生成：" + ScenePath);
        }

        private static void ConfigureStorage(StorageComponent storage)
        {
            SerializedObject serialized = new SerializedObject(storage);
            serialized.FindProperty("storageDirectoryName").stringValue = "SampleStorage";
            serialized.FindProperty("defaultProtectionMode").enumValueIndex = 1;
            serialized.FindProperty("defaultProtectionKeyId").stringValue = "SaveKey.SampleStorage";
            serialized.FindProperty("automaticallyManageSaveKey").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static StorageAcceptanceController CreateInterface(Transform parent)
        {
            GameObject root = new GameObject("存档验收", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 1f);
            StorageAcceptanceController controller = root.AddComponent<StorageAcceptanceController>();

            CreateText(root.transform, "标题", "存档功能验收", new Vector2(0, -34), new Vector2(900, 52), 30, TextAnchor.MiddleCenter);
            Text environment = CreateText(root.transform, "运行环境", "", new Vector2(0, -88), new Vector2(1040, 58), 16, TextAnchor.MiddleLeft);

            Dropdown slot = CreateDropdown(root.transform, "存档槽位", new Vector2(-390, -174), new[] { "存档槽 1", "存档槽 2", "存档槽 3" });
            InputField playerName = CreateInput(root.transform, "玩家名称", "测试玩家", new Vector2(-390, -228));
            InputField level = CreateInput(root.transform, "等级", "1", new Vector2(-390, -282));
            InputField coins = CreateInput(root.transform, "金币", "100", new Vector2(-390, -336));
            Toggle encryption = CreateToggle(root.transform, "加密并认证", new Vector2(-390, -400));
            Toggle compression = CreateToggle(root.transform, "GZip 压缩", new Vector2(-140, -400));

            Button save = CreateButton(root.transform, "保存", new Vector2(150, -174));
            Button load = CreateButton(root.transform, "读取", new Vector2(390, -174));
            Button delete = CreateButton(root.transform, "删除当前槽位", new Vector2(150, -228));
            Button list = CreateButton(root.transform, "列出全部槽位", new Vector2(390, -228));
            Button corrupt = CreateButton(root.transform, "验证损坏后恢复", new Vector2(150, -282));
            Button deleteKey = CreateButton(root.transform, "删除测试密钥", new Vector2(390, -282));
            Button recreateKey = CreateButton(root.transform, "重建测试密钥", new Vector2(150, -336));
            Button restart = CreateButton(root.transform, "软重启框架", new Vector2(390, -336));
            Button quit = CreateButton(root.transform, "退出程序", new Vector2(270, -400));

            Text status = CreateText(root.transform, "状态", "准备中", new Vector2(0, -476), new Vector2(1040, 44), 22, TextAnchor.MiddleCenter);
            Text log = CreateText(root.transform, "日志", "", new Vector2(0, -548), new Vector2(1040, 130), 16, TextAnchor.UpperLeft);

            SerializedObject serialized = new SerializedObject(controller);
            Assign(serialized, "slotDropdown", slot);
            Assign(serialized, "playerNameInput", playerName);
            Assign(serialized, "levelInput", level);
            Assign(serialized, "coinsInput", coins);
            Assign(serialized, "encryptionToggle", encryption);
            Assign(serialized, "compressionToggle", compression);
            Assign(serialized, "saveButton", save);
            Assign(serialized, "loadButton", load);
            Assign(serialized, "deleteButton", delete);
            Assign(serialized, "listButton", list);
            Assign(serialized, "corruptButton", corrupt);
            Assign(serialized, "deleteKeyButton", deleteKey);
            Assign(serialized, "recreateKeyButton", recreateKey);
            Assign(serialized, "restartButton", restart);
            Assign(serialized, "quitButton", quit);
            Assign(serialized, "environmentText", environment);
            Assign(serialized, "statusText", status);
            Assign(serialized, "logText", log);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static InputField CreateInput(Transform parent, string label, string value, Vector2 position)
        {
            CreateText(parent, label + "标签", label, position, new Vector2(130, 42), 17, TextAnchor.MiddleRight);
            GameObject inputObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            SetRect(inputObject.GetComponent<RectTransform>(), position + new Vector2(165, 0), new Vector2(190, 40));
            inputObject.GetComponent<Image>().color = new Color(0.18f, 0.21f, 0.25f, 1f);
            Text text = CreateText(inputObject.transform, "文本", value, Vector2.zero, new Vector2(170, 38), 17, TextAnchor.MiddleLeft);
            Text placeholder = CreateText(inputObject.transform, "提示", "请输入" + label, Vector2.zero, new Vector2(170, 38), 17, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.65f, 0.67f, 0.70f, 1f);
            InputField input = inputObject.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = value;
            return input;
        }

        private static Dropdown CreateDropdown(Transform parent, string label, Vector2 position, string[] values)
        {
            CreateText(parent, label + "标签", label, position, new Vector2(130, 42), 17, TextAnchor.MiddleRight);
            GameObject dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = label;
            dropdownObject.transform.SetParent(parent, false);
            SetRect(dropdownObject.GetComponent<RectTransform>(), position + new Vector2(165, 0), new Vector2(190, 40));
            Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(values));
            SetFont(dropdownObject);
            return dropdown;
        }

        private static Toggle CreateToggle(Transform parent, string label, Vector2 position)
        {
            GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleObject.name = label;
            toggleObject.transform.SetParent(parent, false);
            SetRect(toggleObject.GetComponent<RectTransform>(), position, new Vector2(210, 40));
            Text text = toggleObject.GetComponentInChildren<Text>();
            text.text = label;
            text.font = BuiltinFont;
            text.fontSize = 17;
            text.color = Color.white;
            return toggleObject.GetComponent<Toggle>();
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            SetRect(buttonObject.GetComponent<RectTransform>(), position, new Vector2(210, 42));
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.34f, 0.48f, 1f);
            Text text = CreateText(buttonObject.transform, "文字", label, Vector2.zero, new Vector2(200, 40), 17, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return buttonObject.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            SetRect(textObject.GetComponent<RectTransform>(), position, size);
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

        private static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void SetFont(GameObject root)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = BuiltinFont;
                texts[i].fontSize = 16;
            }
        }

        private static void CreateCameraAndEventSystem()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void Assign(SerializedObject serialized, string property, Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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
