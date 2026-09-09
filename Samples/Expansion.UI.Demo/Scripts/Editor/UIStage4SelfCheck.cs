using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 阶段 4 编辑器与运行时规则的轻量回归检查；临时 Prefab 会在检查后删除。
    /// </summary>
    internal static class UIStage4SelfCheck
    {
        [MenuItem("GameObject/UnityRFramework/UI 工具与资源自检", false, 22)]
        private static void Run()
        {
            CheckPrefabRules();
            CheckAtlasRules();
            CheckSafeArea();
            Debug.Log("[UI 工具与资源自检] 通过。");
        }

        private static void CheckPrefabRules()
        {
            var root = new GameObject("Root", typeof(RectTransform), typeof(Canvas));
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/UIStage4SelfCheck.prefab");
            try
            {
                var button = new GameObject(
                    "Button",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                button.transform.SetParent(root.transform, false);

                var decoration = new GameObject(
                    "Decoration",
                    typeof(RectTransform),
                    typeof(Image));
                decoration.transform.SetParent(button.transform, false);

                var nested = new GameObject(
                    "NestedCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
                nested.transform.SetParent(root.transform, false);
                nested.GetComponent<Canvas>().overrideSorting = true;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                List<string> issues = UIPrefabValidator.ValidatePrefabAsset(prefabPath);
                Require(Contains(issues, "Raycast Target"), "未识别重复 Raycast 风险。");
                Require(Contains(issues, "嵌套 Canvas"), "未识别异常嵌套 Canvas。");
                Require(Contains(issues, "CanvasScaler"), "未识别嵌套 Canvas 上无效的 CanvasScaler。");
            }
            finally
            {
                AssetDatabase.DeleteAsset(prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckAtlasRules()
        {
            var atlas = new SpriteAtlas();
            try
            {
                SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
                packing.enableRotation = true;
                packing.enableTightPacking = true;
                packing.padding = 0;
                atlas.SetPackingSettings(packing);

                List<string> issues = SpriteAtlasWorkflow.Validate(atlas);
                Require(Contains(issues, "没有收集"), "未识别空 SpriteAtlas。");
                Require(Contains(issues, "Allow Rotation"), "未识别 Rotation 设置。");
                Require(Contains(issues, "Tight Packing"), "未识别 Tight Packing 设置。");
                Require(Contains(issues, "Padding"), "未识别过小 Padding。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        private static void CheckSafeArea()
        {
            var node = new GameObject("SafeArea", typeof(RectTransform));
            try
            {
                SafeAreaFitter fitter = node.AddComponent<SafeAreaFitter>();
                fitter.ApplySafeArea();

                Rect safeArea = Screen.safeArea;
                var expectedMin = new Vector2(
                    safeArea.xMin / Screen.width,
                    safeArea.yMin / Screen.height);
                var expectedMax = new Vector2(
                    safeArea.xMax / Screen.width,
                    safeArea.yMax / Screen.height);
                RectTransform rect = node.GetComponent<RectTransform>();
                Require(Vector2.Distance(rect.anchorMin, expectedMin) < 0.0001f,
                    "Safe Area 最小锚点不正确。");
                Require(Vector2.Distance(rect.anchorMax, expectedMax) < 0.0001f,
                    "Safe Area 最大锚点不正确。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);
            }
        }

        private static bool Contains(List<string> issues, string value)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Contains(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("[UI 工具与资源自检] " + message);
            }
        }
    }
}
