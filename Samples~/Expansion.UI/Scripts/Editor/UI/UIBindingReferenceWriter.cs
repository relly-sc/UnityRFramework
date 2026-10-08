using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 写入生成代码，并在脚本编译完成后回填 Prefab 或场景对象引用。
    /// </summary>
    [InitializeOnLoad]
    internal static class UIBindingReferenceWriter
    {
        private const string PendingKey =
            "UnityRFramework.Expansion.UIBinding.Pending";

        static UIBindingReferenceWriter()
        {
            EditorApplication.delayCall += TryApplyPending;
        }

        public static bool GenerateAndQueue(
            MonoBehaviour target,
            IReadOnlyList<UIBindingDraft> bindings,
            out string outputPath,
            out string message)
        {
            UIBindingValidationResult validation =
                UIBindingCodeGenerator.Validate(target, bindings);
            if (!validation.IsValid)
            {
                outputPath = string.Empty;
                message = string.Join(string.Empty, validation.Errors);
                return false;
            }

            outputPath = UIBindingCodeGenerator.GetOutputPath(target);
            string code = UIBindingCodeGenerator.Generate(target, bindings);
            string absolutePath = Path.GetFullPath(outputPath);
            string current = File.Exists(absolutePath)
                ? File.ReadAllText(absolutePath).Replace("\r\n", "\n")
                : string.Empty;
            bool changed = !string.Equals(current, code, StringComparison.Ordinal);

            PendingAssignment pending = CreatePending(target, bindings, outputPath);
            SessionState.SetString(PendingKey, JsonUtility.ToJson(pending));
            if (changed)
            {
                File.WriteAllText(absolutePath, code, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(
                    outputPath,
                    ImportAssetOptions.ForceSynchronousImport);
                message = "代码已生成，脚本编译后将自动回填引用。";
            }
            else
            {
                EditorApplication.delayCall += TryApplyPending;
                message = "生成代码没有变化，正在回填引用。";
            }

            return true;
        }

        internal static void TryApplyPending()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            PendingAssignment pending = JsonUtility.FromJson<PendingAssignment>(json);
            MonoBehaviour target = ResolveObject<MonoBehaviour>(pending.TargetId);
            if (target == null)
            {
                SessionState.EraseString(PendingKey);
                Debug.LogError("[UI 自动绑定] 目标对象已不存在，无法回填引用。");
                return;
            }

            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.Update();
            for (int i = 0; i < pending.Bindings.Count; i++)
            {
                if (serializedTarget.FindProperty(pending.Bindings[i].FieldName) == null)
                {
                    Debug.LogWarning(
                        $"[UI 自动绑定] 字段 '{pending.Bindings[i].FieldName}' 尚不可用，"
                        + "请先修复脚本编译错误，再重新生成。");
                    return;
                }
            }

            Undo.RecordObject(target, "回填 UI 自动绑定引用");
            HashSet<UnityEngine.Object> changedObjects =
                new HashSet<UnityEngine.Object> { target };
            for (int i = 0; i < pending.Bindings.Count; i++)
            {
                PendingBinding binding = pending.Bindings[i];
                SerializedProperty property =
                    serializedTarget.FindProperty(binding.FieldName);
                Component[] components = binding.ComponentIds
                    .Select(ResolveObject<Component>)
                    .ToArray();

                if (components.Any(component => component == null))
                {
                    SessionState.EraseString(PendingKey);
                    Debug.LogError(
                        $"[UI 自动绑定] 字段 '{binding.FieldName}' 的组件已不存在。");
                    return;
                }

                if (binding.IsArray)
                {
                    property.arraySize = components.Length;
                    for (int componentIndex = 0;
                         componentIndex < components.Length;
                         componentIndex++)
                    {
                        property.GetArrayElementAtIndex(componentIndex)
                            .objectReferenceValue = components[componentIndex];
                    }
                }
                else
                {
                    property.objectReferenceValue = components[0];
                }

                for (int eventIndex = 0;
                     eventIndex < binding.Events.Count;
                     eventIndex++)
                {
                    Undo.RecordObject(components[0], "绑定 UI 事件");
                    PendingEvent pendingEvent = binding.Events[eventIndex];
                    BindPersistentEvent(
                        components[0],
                        target,
                        pendingEvent.MemberName,
                        pendingEvent.HandlerMethodName);
                    changedObjects.Add(components[0]);
                }
            }

            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            foreach (UnityEngine.Object changedObject in changedObjects)
            {
                SaveObjectChange(changedObject);
            }

            SessionState.EraseString(PendingKey);
            Debug.Log(
                $"[UI 自动绑定] 已生成并回填 {pending.Bindings.Count} 个字段："
                + pending.OutputPath);
        }

        private static PendingAssignment CreatePending(
            MonoBehaviour target,
            IReadOnlyList<UIBindingDraft> bindings,
            string outputPath)
        {
            PendingAssignment pending = new PendingAssignment
            {
                TargetId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(),
                OutputPath = outputPath
            };
            for (int i = 0; i < bindings.Count; i++)
            {
                UIBindingDraft source = bindings[i];
                PendingBinding binding = new PendingBinding
                {
                    FieldName = source.FieldName,
                    IsArray = source.IsArray
                };
                List<UIBindingEventDescriptor> events =
                    UIBindingCodeGenerator.GetSelectedEvents(source);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    binding.Events.Add(new PendingEvent
                    {
                        MemberName = events[eventIndex].MemberName,
                        HandlerMethodName = UIBindingCodeGenerator.GetHandlerMethodName(
                            source,
                            events[eventIndex].MemberName)
                    });
                }

                for (int componentIndex = 0;
                     componentIndex < source.Components.Count;
                     componentIndex++)
                {
                    binding.ComponentIds.Add(
                        GlobalObjectId.GetGlobalObjectIdSlow(
                            source.Components[componentIndex]).ToString());
                }

                pending.Bindings.Add(binding);
            }

            return pending;
        }

        internal static void BindPersistentEvent(
            UnityEngine.Object source,
            UnityEngine.Object target,
            string eventMemberName,
            string methodName)
        {
            UIBindingEventDescriptor descriptor = UIBindingCodeGenerator
                .GetSupportedEvents(source.GetType())
                .FirstOrDefault(item => item.MemberName == eventMemberName);
            UnityEventBase unityEvent = GetUnityEvent(source, eventMemberName);
            if (unityEvent == null)
            {
                throw new InvalidOperationException(
                    $"组件 {source.GetType().FullName} 没有可绑定的 {eventMemberName} 事件。");
            }

            if (descriptor == null)
            {
                throw new InvalidOperationException(
                    $"事件 {eventMemberName} 不是受支持的 UnityEvent。");
            }

            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (unityEvent.GetPersistentTarget(i) == target
                    && unityEvent.GetPersistentMethodName(i) == methodName)
                {
                    UnityEventTools.RemovePersistentListener(unityEvent, i);
                }
            }

            MethodInfo handler = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                descriptor.ArgumentTypes,
                null);
            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"目标 {target.GetType().FullName} 没有公开方法 {methodName}。\n");
            }

            if (descriptor.ArgumentTypes.Length == 0)
            {
                UnityAction action = (UnityAction)Delegate.CreateDelegate(
                    typeof(UnityAction),
                    target,
                    handler);
                UnityEventTools.AddPersistentListener((UnityEvent)unityEvent, action);
                return;
            }

            Type actionType = GetUnityActionType(descriptor.ArgumentTypes);
            Delegate callback = Delegate.CreateDelegate(
                actionType,
                target,
                handler);
            MethodInfo addMethod = typeof(UnityEventTools)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method =>
                    method.Name == "AddPersistentListener"
                    && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == descriptor.ArgumentTypes.Length
                    && method.GetParameters().Length == 2)
                .MakeGenericMethod(descriptor.ArgumentTypes);
            addMethod.Invoke(null, new object[] { unityEvent, callback });
        }

        private static UnityEventBase GetUnityEvent(
            UnityEngine.Object source,
            string memberName)
        {
            PropertyInfo property = source.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(source, null) as UnityEventBase;
            }

            FieldInfo field = source.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public);
            return field?.GetValue(source) as UnityEventBase;
        }

        private static Type GetUnityActionType(Type[] argumentTypes)
        {
            switch (argumentTypes.Length)
            {
                case 1:
                    return typeof(UnityAction<>).MakeGenericType(argumentTypes);
                case 2:
                    return typeof(UnityAction<,>).MakeGenericType(argumentTypes);
                case 3:
                    return typeof(UnityAction<,,>).MakeGenericType(argumentTypes);
                case 4:
                    return typeof(UnityAction<,,,>).MakeGenericType(argumentTypes);
                default:
                    throw new NotSupportedException(
                        "仅支持 0 到 4 个参数的 UnityEvent。");
            }
        }

        private static T ResolveObject<T>(string globalId)
            where T : UnityEngine.Object
        {
            if (!GlobalObjectId.TryParse(globalId, out GlobalObjectId id))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
        }

        private static void SaveObjectChange(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            Component component = target as Component;
            if (component == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(component))
            {
                GameObject root = component.transform.root.gameObject;
                PrefabUtility.SavePrefabAsset(root);
            }
            else if (component.gameObject.scene.IsValid())
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
        }

        [Serializable]
        private sealed class PendingAssignment
        {
            public string TargetId;
            public string OutputPath;
            public List<PendingBinding> Bindings = new List<PendingBinding>();
        }

        [Serializable]
        private sealed class PendingBinding
        {
            public string FieldName;
            public bool IsArray;
            public List<PendingEvent> Events = new List<PendingEvent>();
            public List<string> ComponentIds = new List<string>();
        }

        [Serializable]
        private sealed class PendingEvent
        {
            public string MemberName;
            public string HandlerMethodName;
        }
    }
}
