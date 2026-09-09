using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    internal partial class UIBindingGeneratorSelfCheckTarget
    {
    }

    [Serializable]
    internal sealed class UIBindingTwoArgumentEvent : UnityEvent<string, int>
    {
    }

    [Serializable]
    internal sealed class UIBindingThreeArgumentEvent : UnityEvent<string, int, float>
    {
    }

    [Serializable]
    internal sealed class UIBindingFourArgumentEvent : UnityEvent<string, int, float, bool>
    {
    }

    internal sealed class UIBindingEventSource : ScriptableObject
    {
        public UIBindingTwoArgumentEvent OnTwo = new UIBindingTwoArgumentEvent();
        public UIBindingThreeArgumentEvent OnThree = new UIBindingThreeArgumentEvent();
        public UIBindingFourArgumentEvent OnFour = new UIBindingFourArgumentEvent();
    }

    internal sealed class UIBindingEventTarget : ScriptableObject
    {
        public void HandleTwo(string value, int index) { }
        public void HandleThree(string value, int index, float amount) { }
        public void HandleFour(string value, int index, float amount, bool enabled) { }
    }

    internal static class UIBindingGeneratorSelfCheck
    {
        [MenuItem("GameObject/UnityRFramework/UI 自动绑定生成器自检", false, 21)]
        private static void Run(MenuCommand command)
        {
            GameObject root = new GameObject("Root");
            try
            {
                string selfCheckSourcePath = FindSelfCheckSourcePath();
                Button button = new GameObject("SubmitButton")
                    .AddComponent<Button>();
                button.transform.SetParent(root.transform, false);
                List<Component> selectableComponents =
                    UIBindingGeneratorWindow.GetSelectableComponents(button.gameObject);
                Require(selectableComponents.Contains(button),
                    "节点组件列表没有包含显式选择的 Button。");
                Require(selectableComponents.Contains(button.transform),
                    "节点组件列表没有保留可选的 RectTransform。");
                Image sameNodeImage = button.gameObject.AddComponent<Image>();
                List<MonoBehaviour> selectableBehaviours =
                    UIBindingGeneratorWindow.GetSelectableBehaviours(button.gameObject);
                Require(selectableBehaviours.Contains(button)
                    && selectableBehaviours.Contains(sameNodeImage),
                    "目标节点没有列出全部 MonoBehaviour。");
                Image first = new GameObject("IconA").AddComponent<Image>();
                first.transform.SetParent(root.transform, false);
                Image second = new GameObject("IconB").AddComponent<Image>();
                second.transform.SetParent(root.transform, false);

                List<UIBindingDraft> bindings = new List<UIBindingDraft>
                {
                    new UIBindingDraft
                    {
                        FieldName = "submitButton",
                        EventMemberNames = new List<string> { "onClick" },
                        Components = new List<Component> { button }
                    },
                    new UIBindingDraft
                    {
                        FieldName = "icons",
                        IsArray = true,
                        Components = new List<Component> { first, second }
                    }
                };

                UIBindingValidationResult validation =
                    UIBindingCodeGenerator.Validate(
                        typeof(UIBindingGeneratorSelfCheckTarget),
                        root.transform,
                        selfCheckSourcePath,
                        string.Empty,
                        bindings);
                Require(validation.IsValid, string.Join(string.Empty, validation.Errors));

                string firstOutput = UIBindingCodeGenerator.Generate(
                    typeof(UIBindingGeneratorSelfCheckTarget),
                    bindings);
                string secondOutput = UIBindingCodeGenerator.Generate(
                    typeof(UIBindingGeneratorSelfCheckTarget),
                    bindings);
                Require(firstOutput == secondOutput, "连续生成结果不一致。");
                Require(firstOutput.Contains("private global::UnityEngine.UI.Button submitButton;"),
                    "普通字段类型输出错误。");
                Require(firstOutput.Contains("private global::UnityEngine.UI.Image[] icons;"),
                    "数组字段类型输出错误。");
                Require(firstOutput.Contains("public void HandleSubmitButtonClick()"),
                    "Button 事件入口未生成。");
                Require(
                    UIBindingCodeGenerator.GetHandlerMethodName(
                        new UIBindingDraft
                        {
                            FieldName = "input_tmp"
                        },
                        "onValueChanged") == "HandleInputTmpValueChanged",
                    "下划线字段名没有转换为规范事件方法名。");
                Require(
                    UIBindingCodeGenerator.GetSupportedEvents(typeof(InputField))
                        .Exists(item => item.MemberName == "onEndEdit")
                    && UIBindingCodeGenerator.GetSupportedEvents(typeof(InputField))
                        .Exists(item => item.MemberName == "onValueChanged"),
                    "没有发现组件公开的全部 UnityEvent。");

                Button eventTarget = new GameObject("EventTarget")
                    .AddComponent<Button>();
                eventTarget.transform.SetParent(root.transform, false);
                UIBindingReferenceWriter.BindPersistentEvent(
                    button,
                    eventTarget,
                    "onClick",
                    nameof(Selectable.Select));
                Require(button.onClick.GetPersistentEventCount() == 1,
                    "Button 持久化事件未写入。");
                UIBindingReferenceWriter.BindPersistentEvent(
                    button,
                    eventTarget,
                    "onClick",
                    nameof(Selectable.Select));
                Require(button.onClick.GetPersistentEventCount() == 1,
                    "重复绑定产生了重复事件。");

                InputField inputSource = new GameObject("InputSource")
                    .AddComponent<InputField>();
                inputSource.transform.SetParent(root.transform, false);
                InputField inputTarget = new GameObject("InputTarget")
                    .AddComponent<InputField>();
                inputTarget.transform.SetParent(root.transform, false);
                UIBindingReferenceWriter.BindPersistentEvent(
                    inputSource,
                    inputTarget,
                    "onValueChanged",
                    nameof(InputField.SetTextWithoutNotify));
                Require(inputSource.onValueChanged.GetPersistentEventCount() == 1,
                    "string 持久化事件未写入。");

                Dropdown dropdownSource = new GameObject("DropdownSource")
                    .AddComponent<Dropdown>();
                dropdownSource.transform.SetParent(root.transform, false);
                Dropdown dropdownTarget = new GameObject("DropdownTarget")
                    .AddComponent<Dropdown>();
                dropdownTarget.transform.SetParent(root.transform, false);
                UIBindingReferenceWriter.BindPersistentEvent(
                    dropdownSource,
                    dropdownTarget,
                    "onValueChanged",
                    nameof(Dropdown.SetValueWithoutNotify));
                Require(dropdownSource.onValueChanged.GetPersistentEventCount() == 1,
                    "int 持久化事件未写入。");

                UIBindingEventSource multiSource =
                    ScriptableObject.CreateInstance<UIBindingEventSource>();
                UIBindingEventTarget multiTarget =
                    ScriptableObject.CreateInstance<UIBindingEventTarget>();
                try
                {
                    UIBindingReferenceWriter.BindPersistentEvent(
                        multiSource, multiTarget, "OnTwo", nameof(UIBindingEventTarget.HandleTwo));
                    UIBindingReferenceWriter.BindPersistentEvent(
                        multiSource, multiTarget, "OnThree", nameof(UIBindingEventTarget.HandleThree));
                    UIBindingReferenceWriter.BindPersistentEvent(
                        multiSource, multiTarget, "OnFour", nameof(UIBindingEventTarget.HandleFour));
                    Require(multiSource.OnTwo.GetPersistentEventCount() == 1
                        && multiSource.OnThree.GetPersistentEventCount() == 1
                        && multiSource.OnFour.GetPersistentEventCount() == 1,
                        "多参数 UnityEvent 持久化事件未写入。");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(multiSource);
                    UnityEngine.Object.DestroyImmediate(multiTarget);
                }

                Type tmpInputType = FindComponentType("TMPro.TMP_InputField");
                if (tmpInputType != null)
                {
                    Component tmpInput = new GameObject("TmpInput")
                        .AddComponent(tmpInputType);
                    tmpInput.transform.SetParent(root.transform, false);
                    UIBindingValidationResult assemblyValidation =
                        UIBindingCodeGenerator.Validate(
                            typeof(UIBindingGeneratorSelfCheckTarget),
                            root.transform,
                            "Assets/UnityRFramework/Samples/Sample.Download/Scripts/Runtime/"
                            + "DownloadAcceptanceController.cs",
                            string.Empty,
                            new[]
                            {
                                new UIBindingDraft
                                {
                                    FieldName = "tmpInput",
                                    Components = new List<Component> { tmpInput }
                                }
                            });
                    Require(
                        assemblyValidation.Errors.Exists(message =>
                            message.Contains("Unity.TextMeshPro")),
                        "自定义 asmdef 缺少 TMP 引用时没有阻止生成。");
                }

                bindings[1].Components[1] = button;
                validation = UIBindingCodeGenerator.Validate(
                    typeof(UIBindingGeneratorSelfCheckTarget),
                    root.transform,
                    selfCheckSourcePath,
                    string.Empty,
                    bindings);
                Require(!validation.IsValid, "重复组件引用未被拦截。");

                Debug.Log("[UI 自动绑定] 自检通过。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    "[UI 自动绑定] 自检失败：" + message);
            }
        }

        private static Type FindComponentType(string fullName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (type.FullName == fullName)
                {
                    return type;
                }
            }

            return null;
        }

        private static string FindSelfCheckSourcePath()
        {
            string[] guids = AssetDatabase.FindAssets("UIBindingGeneratorSelfCheckTarget t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/UIBindingGeneratorSelfCheckTarget.cs", StringComparison.Ordinal))
                {
                    return path;
                }
            }

            throw new InvalidOperationException("无法定位 UI 自动绑定自检脚本。");
        }
    }
}
