using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 检查 UI Prefab 中会直接影响引用、点击和 Canvas 排序的常见配置问题。
    /// </summary>
    internal static class UIPrefabValidator
    {
        [MenuItem("Assets/UnityRFramework/检查 UI Prefab", false, 30)]
        private static void ValidateSelectedAsset()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(selected);
            List<string> issues = ValidatePrefabAsset(path);
            ValidateAndReport(selected == null ? path : selected.name, issues, selected);
        }

        [MenuItem("Assets/UnityRFramework/检查 UI Prefab", true)]
        private static bool CanValidateSelectedAsset()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem("GameObject/UnityRFramework/检查 UI Prefab", false, 21)]
        private static void ValidateSelectedObject(MenuCommand command)
        {
            ValidateAndReport(command.context as GameObject ?? Selection.activeGameObject);
        }

        internal static List<string> ValidatePrefabAsset(string path)
        {
            var issues = new List<string>();
            if (string.IsNullOrEmpty(path)
                || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("未选择 Prefab 资产。");
                return issues;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                return Validate(root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static List<string> Validate(GameObject root)
        {
            var issues = new List<string>();
            if (root == null)
            {
                issues.Add("未选择 UI Prefab 或场景 UI 根节点。");
                return issues;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject node = transforms[i].gameObject;
                ValidateMissingReferences(root.transform, node, issues);
                ValidateRaycastTargets(root.transform, node, issues);
                ValidateNestedCanvas(root.transform, node, issues);
            }

            return issues;
        }

        private static void ValidateAndReport(GameObject root)
        {
            if (root == null)
            {
                Debug.LogWarning("[UI Prefab 检查] 未选择 UI Prefab 或场景 UI 根节点。");
                return;
            }

            ValidateAndReport(root.name, Validate(root), root);
        }

        private static void ValidateAndReport(
            string targetName,
            List<string> issues,
            UnityEngine.Object context)
        {
            if (issues.Count == 0)
            {
                Debug.Log($"[UI Prefab 检查] {targetName}：未发现问题。", context);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[UI Prefab 检查] {issues[i]}", context);
            }

            Debug.LogWarning(
                $"[UI Prefab 检查] {targetName}：发现 {issues.Count} 个问题或风险项。",
                context);
        }

        private static void ValidateMissingReferences(
            Transform root,
            GameObject node,
            List<string> issues)
        {
            Component[] components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    issues.Add($"{GetPath(root, node.transform)} 存在丢失的脚本组件。");
                    continue;
                }

                var serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null
                        && property.objectReferenceInstanceIDValue != 0)
                    {
                        issues.Add(
                            $"{GetPath(root, node.transform)} 的 {component.GetType().Name}.{property.propertyPath} 引用已丢失。");
                    }
                }
            }
        }

        private static void ValidateRaycastTargets(
            Transform root,
            GameObject node,
            List<string> issues)
        {
            Graphic[] graphics = node.GetComponents<Graphic>();
            int raycastGraphicCount = 0;
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null && graphic.enabled && graphic.raycastTarget)
                {
                    raycastGraphicCount++;
                }
            }

            if (raycastGraphicCount > 1)
            {
                issues.Add(
                    $"{GetPath(root, node.transform)} 同时存在 {raycastGraphicCount} 个启用 Raycast Target 的 Graphic。");
            }

            if (node.GetComponent<Selectable>() != null)
            {
                return;
            }

            Selectable parentSelectable = node.transform.parent == null
                ? null
                : node.transform.parent.GetComponentInParent<Selectable>();
            if (parentSelectable == null)
            {
                return;
            }

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null && graphic.enabled && graphic.raycastTarget)
                {
                    issues.Add(
                        $"{GetPath(root, node.transform)} 的 {graphic.GetType().Name} 位于交互组件 "
                        + $"{GetPath(root, parentSelectable.transform)} 内，通常应关闭 Raycast Target。");
                }
            }
        }

        private static void ValidateNestedCanvas(
            Transform root,
            GameObject node,
            List<string> issues)
        {
            Canvas canvas = node.GetComponent<Canvas>();
            if (canvas == null || node.transform.parent == null)
            {
                return;
            }

            Canvas parentCanvas = node.transform.parent.GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return;
            }

            CanvasScaler canvasScaler = node.GetComponent<CanvasScaler>();
            if (canvasScaler != null && canvasScaler.enabled)
            {
                issues.Add(
                    $"{GetPath(root, node.transform)} 是嵌套 Canvas，其 CanvasScaler 不会生效；"
                    + "缩放应由根 Canvas 的 CanvasScaler 统一管理。");
            }

            if (!canvas.overrideSorting && canvas.sortingOrder != 0)
            {
                issues.Add(
                    $"{GetPath(root, node.transform)} 是嵌套 Canvas，未启用 Override Sorting，"
                    + $"其 Sorting Order {canvas.sortingOrder} 不会独立生效。");
            }
            else if (canvas.overrideSorting
                     && canvas.sortingLayerID == parentCanvas.sortingLayerID
                     && canvas.sortingOrder == parentCanvas.sortingOrder)
            {
                issues.Add(
                    $"{GetPath(root, node.transform)} 是独立排序的嵌套 Canvas，但排序层和顺序与父 Canvas 相同。");
            }
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root)
            {
                return root.name;
            }

            var names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            string path = root.name;
            while (names.Count > 0)
            {
                path += "/" + names.Pop();
            }

            return path;
        }
    }
}
