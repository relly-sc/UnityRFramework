using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 通过显式选择组件生成 partial 序列化字段和可选 UI 事件入口。
    /// </summary>
    internal sealed class UIBindingGeneratorWindow : EditorWindow
    {
        [SerializeField]
        private MonoBehaviour target;

        [SerializeField]
        private GameObject targetNode;

        [SerializeField]
        private List<UIBindingDraft> bindings = new List<UIBindingDraft>();

        [SerializeField]
        private Vector2 scrollPosition;

        [SerializeField]
        private bool showCodePreview;

        private string resultMessage = string.Empty;
        private MessageType resultMessageType = MessageType.Info;

        [MenuItem("GameObject/UnityRFramework/UI 自动绑定代码生成器", false, 20)]
        private static void Open(MenuCommand command)
        {
            UIBindingGeneratorWindow window =
                GetWindow<UIBindingGeneratorWindow>("UI 自动绑定");
            GameObject selectedNode = command.context as GameObject
                ?? Selection.activeGameObject;
            if (window.targetNode != selectedNode)
            {
                window.targetNode = selectedNode;
                window.target = null;
                window.resultMessage = string.Empty;
            }

            window.minSize = new Vector2(680f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "选择目标节点及承载字段的 partial MonoBehaviour，再逐项显式指定组件。"
                + "生成文件只包含序列化引用和可选事件入口，不会修改业务代码。",
                MessageType.Info);

            DrawTargetSelector();

            if (target != null
                && !EditorUtility.IsPersistent(target)
                && string.IsNullOrEmpty(target.gameObject.scene.path))
            {
                EditorGUILayout.HelpBox(
                    "请先保存目标场景，再生成并自动回填引用。",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "输出文件",
                    GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(
                        UIBindingCodeGenerator.GetOutputPath(target));
                }
            }

            bool tmpInstalled = IsTypeLoaded("TMPro.TMP_Text")
                || IsTypeLoaded("TMPro.TextMeshProUGUI");
            EditorGUILayout.LabelField(
                "TMP 编辑器适配",
                tmpInstalled ? "已检测到" : "未安装，不显示 TMP 事件选项");

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("绑定项", EditorStyles.boldLabel);
                if (GUILayout.Button("添加字段", GUILayout.Width(90f)))
                {
                    bindings.Add(new UIBindingDraft());
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            int removeIndex = -1;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (DrawBinding(i, bindings[i]))
                {
                    removeIndex = i;
                }
            }

            if (removeIndex >= 0)
            {
                bindings.RemoveAt(removeIndex);
            }

            DrawValidationAndPreview();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            UIBindingValidationResult validation =
                UIBindingCodeGenerator.Validate(target, bindings);
            using (new EditorGUI.DisabledScope(!validation.IsValid))
            {
                if (GUILayout.Button("生成并回填", GUILayout.Height(32f)))
                {
                    bool started = UIBindingReferenceWriter.GenerateAndQueue(
                        target,
                        bindings,
                        out string outputPath,
                        out string message);
                    resultMessage = started
                        ? message + "\n" + outputPath
                        : message;
                    resultMessageType = started
                        ? MessageType.Info
                        : MessageType.Error;
                }
            }

            if (!string.IsNullOrEmpty(resultMessage))
            {
                EditorGUILayout.HelpBox(resultMessage, resultMessageType);
            }
        }

        private bool DrawBinding(int index, UIBindingDraft binding)
        {
            bool remove = false;
            if (binding.EventMemberNames == null)
            {
                binding.EventMemberNames = new List<string>();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"字段 {index + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("删除", GUILayout.Width(56f)))
                    {
                        remove = true;
                    }
                }

                binding.FieldName = EditorGUILayout.TextField(
                    "字段名",
                    binding.FieldName);
                bool nextIsArray = EditorGUILayout.Toggle("数组字段", binding.IsArray);
                if (binding.IsArray != nextIsArray)
                {
                    binding.IsArray = nextIsArray;
                    binding.EventMemberNames.Clear();
                    EnsureComponentSlots(binding);
                }

                EnsureComponentSlots(binding);
                for (int componentIndex = 0;
                     componentIndex < binding.Components.Count;
                     componentIndex++)
                {
                    bool removeComponent = DrawComponentSelector(
                        binding,
                        componentIndex);
                    if (removeComponent)
                    {
                        binding.Nodes.RemoveAt(componentIndex);
                        binding.Components.RemoveAt(componentIndex);
                        componentIndex--;
                    }
                }

                if (binding.IsArray)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth);
                        if (GUILayout.Button("添加数组元素"))
                        {
                            binding.Nodes.Add(null);
                            binding.Components.Add(null);
                        }
                    }
                }

                DrawEventSelector(binding);
                DrawBindingPreview(binding);
            }

            return remove;
        }

        private void DrawTargetSelector()
        {
            if (targetNode == null && target != null)
            {
                targetNode = target.gameObject;
            }

            EditorGUI.BeginChangeCheck();
            GameObject nextNode = (GameObject)EditorGUILayout.ObjectField(
                "目标节点",
                targetNode,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                targetNode = nextNode;
                target = null;
                resultMessage = string.Empty;
            }

            if (targetNode == null)
            {
                target = null;
                EditorGUILayout.Popup("目标脚本", 0, new[] { "请先选择目标节点" });
                return;
            }

            List<MonoBehaviour> choices = GetSelectableBehaviours(targetNode);
            int selected = target == null ? 0 : choices.IndexOf(target) + 1;
            if (selected == 0 && target != null)
            {
                target = null;
            }

            string[] labels = new string[choices.Count + 1];
            labels[0] = "请选择 MonoBehaviour";
            for (int i = 0; i < choices.Count; i++)
            {
                labels[i + 1] = GetComponentLabel(choices, i);
            }

            int nextSelected = EditorGUILayout.Popup(
                "目标脚本",
                selected,
                labels);
            if (nextSelected != selected)
            {
                target = nextSelected == 0 ? null : choices[nextSelected - 1];
                resultMessage = string.Empty;
            }
        }

        private static void EnsureComponentSlots(UIBindingDraft binding)
        {
            if (binding.Components == null)
            {
                binding.Components = new List<Component>();
            }

            if (binding.Nodes == null)
            {
                binding.Nodes = new List<GameObject>();
            }

            while (binding.Nodes.Count < binding.Components.Count)
            {
                Component component = binding.Components[binding.Nodes.Count];
                binding.Nodes.Add(component == null ? null : component.gameObject);
            }

            while (binding.Nodes.Count > binding.Components.Count)
            {
                binding.Nodes.RemoveAt(binding.Nodes.Count - 1);
            }

            if (!binding.IsArray)
            {
                while (binding.Components.Count > 1)
                {
                    binding.Components.RemoveAt(binding.Components.Count - 1);
                    binding.Nodes.RemoveAt(binding.Nodes.Count - 1);
                }
            }

            if (binding.Components.Count == 0)
            {
                binding.Nodes.Add(null);
                binding.Components.Add(null);
            }
        }

        private static bool DrawComponentSelector(
            UIBindingDraft binding,
            int componentIndex)
        {
            bool remove = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                GameObject node = (GameObject)EditorGUILayout.ObjectField(
                    binding.IsArray ? $"节点 {componentIndex + 1}" : "节点",
                    binding.Nodes[componentIndex],
                    typeof(GameObject),
                    true);
                if (EditorGUI.EndChangeCheck())
                {
                    binding.Nodes[componentIndex] = node;
                    binding.Components[componentIndex] = null;
                    binding.EventMemberNames.Clear();
                }

                if (binding.IsArray
                    && GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    remove = true;
                }
            }

            if (remove || binding.Nodes[componentIndex] == null)
            {
                return remove;
            }

            List<Component> choices = GetSelectableComponents(
                binding.Nodes[componentIndex]);
            Component current = binding.Components[componentIndex];
            int selected = current == null ? 0 : choices.IndexOf(current) + 1;
            if (selected == 0 && current != null)
            {
                binding.Components[componentIndex] = null;
                binding.EventMemberNames.Clear();
            }

            string[] labels = new string[choices.Count + 1];
            labels[0] = "请选择组件";
            for (int i = 0; i < choices.Count; i++)
            {
                labels[i + 1] = GetComponentLabel(choices, i);
            }

            int nextSelected = EditorGUILayout.Popup(
                binding.IsArray ? $"组件类型 {componentIndex + 1}" : "组件类型",
                selected,
                labels);
            if (nextSelected != selected)
            {
                binding.Components[componentIndex] = nextSelected == 0
                    ? null
                    : choices[nextSelected - 1];
                binding.EventMemberNames.Clear();
            }

            return false;
        }

        internal static List<Component> GetSelectableComponents(GameObject node)
        {
            List<Component> result = new List<Component>();
            if (node == null)
            {
                return result;
            }

            Component[] components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    result.Add(components[i]);
                }
            }

            return result;
        }

        internal static List<MonoBehaviour> GetSelectableBehaviours(GameObject node)
        {
            List<MonoBehaviour> result = new List<MonoBehaviour>();
            if (node == null)
            {
                return result;
            }

            MonoBehaviour[] behaviours = node.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    result.Add(behaviours[i]);
                }
            }

            return result;
        }

        private static string GetComponentLabel<T>(IReadOnlyList<T> components, int index)
            where T : Component
        {
            Type type = components[index].GetType();
            int occurrence = 0;
            int total = 0;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].GetType() == type)
                {
                    total++;
                    if (i <= index)
                    {
                        occurrence++;
                    }
                }
            }

            return total > 1
                ? $"{type.FullName} #{occurrence}"
                : type.FullName;
        }

        private void DrawEventSelector(UIBindingDraft binding)
        {
            if (binding.EventMemberNames == null)
            {
                binding.EventMemberNames = new List<string>();
            }

            Type componentType = binding.Components.Count > 0
                && binding.Components[0] != null
                    ? binding.Components[0].GetType()
                    : null;
            List<UIBindingEventDescriptor> choices = binding.IsArray
                ? new List<UIBindingEventDescriptor>()
                : UIBindingCodeGenerator.GetSupportedEvents(componentType);
            HashSet<string> available = new HashSet<string>(
                choices.ConvertAll(item => item.MemberName),
                StringComparer.Ordinal);
            binding.EventMemberNames.RemoveAll(name => !available.Contains(name));

            string buttonText = choices.Count == 0
                ? "该组件无公开 UnityEvent"
                : binding.EventMemberNames.Count == 0
                    ? "未选择"
                    : $"已选择 {binding.EventMemberNames.Count} 项";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("事件入口");
                using (new EditorGUI.DisabledScope(
                    binding.IsArray || componentType == null || choices.Count == 0))
                {
                    if (!GUILayout.Button(buttonText, EditorStyles.popup))
                    {
                        return;
                    }

                    GenericMenu menu = new GenericMenu();
                    for (int i = 0; i < choices.Count; i++)
                    {
                        UIBindingEventDescriptor descriptor = choices[i];
                        string eventName = descriptor.MemberName;
                        bool selected = binding.EventMemberNames.Contains(eventName);
                        menu.AddItem(
                            new GUIContent(descriptor.DisplayName),
                            selected,
                            () =>
                            {
                                if (binding.EventMemberNames.Contains(eventName))
                                {
                                    binding.EventMemberNames.Remove(eventName);
                                }
                                else
                                {
                                    binding.EventMemberNames.Add(eventName);
                                    binding.EventMemberNames.Sort(StringComparer.Ordinal);
                                }

                                resultMessage = string.Empty;
                                Repaint();
                            });
                    }

                    menu.ShowAsContext();
                }
            }
        }

        private void DrawBindingPreview(UIBindingDraft binding)
        {
            Component first = binding.Components.Count > 0
                ? binding.Components[0]
                : null;
            string typeName = first == null
                ? "<未选择>"
                : first.GetType().FullName + (binding.IsArray ? "[]" : string.Empty);
            EditorGUILayout.LabelField("字段类型", typeName);
            if (first != null && target != null)
            {
                for (int i = 0; i < binding.Components.Count; i++)
                {
                    Component component = binding.Components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    EditorGUILayout.LabelField(
                        binding.IsArray ? $"目标路径 {i + 1}" : "目标路径",
                        UIBindingCodeGenerator.GetTransformPath(
                            target.transform,
                            component.transform));
                }
            }
        }

        private void DrawValidationAndPreview()
        {
            UIBindingValidationResult validation =
                UIBindingCodeGenerator.Validate(target, bindings);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("生成检查", EditorStyles.boldLabel);
            if (!validation.IsValid)
            {
                EditorGUILayout.HelpBox(
                    string.Join(string.Empty, validation.Errors),
                    MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                $"检查通过：{bindings.Count} 个字段。",
                MessageType.Info);
            showCodePreview = EditorGUILayout.Foldout(
                showCodePreview,
                "代码预览",
                true);
            if (showCodePreview)
            {
                string code = UIBindingCodeGenerator.Generate(target, bindings);
                EditorGUILayout.TextArea(code, GUILayout.MinHeight(180f));
            }
        }

        private static bool IsTypeLoaded(string fullName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (type.FullName == fullName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
