using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 基于 Unity 原生 SpriteAtlas 的最小创建与检查入口。
    /// </summary>
    internal static class SpriteAtlasWorkflow
    {
        [MenuItem("Assets/UnityRFramework/从所选资源创建 SpriteAtlas", false, 31)]
        private static void CreateFromSelection()
        {
            UnityEngine.Object[] packables = GetSelectedPackables();
            string path = EditorUtility.SaveFilePanelInProject(
                "创建 SpriteAtlas",
                "UISpriteAtlas",
                "spriteatlas",
                "选择 SpriteAtlas 保存位置。");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, path);
            atlas.Add(packables);

            SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = Math.Max(4, packing.padding);
            atlas.SetPackingSettings(packing);
            atlas.SetIncludeInBuild(true);

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            Selection.activeObject = atlas;
            EditorGUIUtility.PingObject(atlas);
            Debug.Log(
                $"[SpriteAtlas] 已创建 {path}，收集对象 {packables.Length} 个。",
                atlas);
            ReportDisabledSpritePacker(atlas);
        }

        [MenuItem("Assets/UnityRFramework/从所选资源创建 SpriteAtlas", true)]
        private static bool CanCreateFromSelection()
        {
            return GetSelectedPackables().Length > 0;
        }

        [MenuItem("Assets/UnityRFramework/检查 SpriteAtlas", false, 32)]
        private static void ValidateSelectedAtlas()
        {
            SpriteAtlas atlas = Selection.activeObject as SpriteAtlas;
            List<string> issues = Validate(atlas);
            if (issues.Count == 0)
            {
                Debug.Log($"[SpriteAtlas 检查] {atlas.name}：未发现问题。", atlas);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[SpriteAtlas 检查] {issues[i]}", atlas);
            }

            Debug.LogWarning(
                $"[SpriteAtlas 检查] {atlas.name}：发现 {issues.Count} 个问题或风险项。",
                atlas);
        }

        [MenuItem("Assets/UnityRFramework/检查 SpriteAtlas", true)]
        private static bool CanValidateSelectedAtlas()
        {
            return Selection.activeObject is SpriteAtlas;
        }

        internal static List<string> Validate(SpriteAtlas atlas)
        {
            var issues = new List<string>();
            if (atlas == null)
            {
                issues.Add("未选择 SpriteAtlas。");
                return issues;
            }

            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
            {
                issues.Add(
                    "Project Settings > Editor > Sprite Packer > Mode 当前为 Disabled，"
                    + "图集不会生成可供运行时读取的 Sprite。");
            }

            UnityEngine.Object[] packables = atlas.GetPackables();
            if (packables.Length == 0)
            {
                issues.Add($"{atlas.name} 没有收集任何 Sprite 或文件夹。");
            }

            SerializedProperty includeInBuild =
                new SerializedObject(atlas).FindProperty("m_EditorData.bindAsDefault");
            if (includeInBuild != null && !includeInBuild.boolValue)
            {
                issues.Add($"{atlas.name} 未启用 Include in Build。");
            }

            SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
            if (packing.enableRotation)
            {
                issues.Add($"{atlas.name} 启用了 Allow Rotation；UGUI 图集建议关闭。");
            }

            if (packing.enableTightPacking)
            {
                issues.Add($"{atlas.name} 启用了 Tight Packing；UGUI 图集建议关闭。");
            }

            if (packing.padding < 2)
            {
                issues.Add($"{atlas.name} 的 Padding 小于 2，可能出现边缘采样串色。");
            }

            AddDuplicateSpriteNameIssues(atlas, packables, issues);
            return issues;
        }

        private static void ReportDisabledSpritePacker(SpriteAtlas atlas)
        {
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
            {
                Debug.LogWarning(
                    "[SpriteAtlas] 当前 Sprite Packer Mode 为 Disabled。请在 Project Settings > Editor > "
                    + "Sprite Packer > Mode 中启用 Sprite Atlas 后再运行或构建。",
                    atlas);
            }
        }

        private static UnityEngine.Object[] GetSelectedPackables()
        {
            var result = new List<UnityEngine.Object>();
            UnityEngine.Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                UnityEngine.Object item = selected[i];
                string path = AssetDatabase.GetAssetPath(item);
                if (AssetDatabase.IsValidFolder(path)
                    || item is Sprite
                    || item is Texture2D)
                {
                    result.Add(item);
                }
            }

            return result.ToArray();
        }

        private static void AddDuplicateSpriteNameIssues(
            SpriteAtlas atlas,
            UnityEngine.Object[] packables,
            List<string> issues)
        {
            var spritesByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < packables.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(packables[i]);
                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { path });
                    for (int j = 0; j < guids.Length; j++)
                    {
                        AddSpritesAtPath(AssetDatabase.GUIDToAssetPath(guids[j]), visited, spritesByName);
                    }
                }
                else
                {
                    AddSpritesAtPath(path, visited, spritesByName);
                }
            }

            foreach (KeyValuePair<string, List<string>> pair in spritesByName)
            {
                if (pair.Value.Count > 1)
                {
                    issues.Add(
                        $"{atlas.name} 中 Sprite 名称“{pair.Key}”重复：{string.Join("，", pair.Value)}。");
                }
            }
        }

        private static void AddSpritesAtPath(
            string path,
            HashSet<string> visited,
            Dictionary<string, List<string>> spritesByName)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Sprite sprite)
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        sprite,
                        out string guid,
                        out long localId))
                {
                    continue;
                }

                string key = guid + ":" + localId;
                if (!visited.Add(key))
                {
                    continue;
                }

                if (!spritesByName.TryGetValue(sprite.name, out List<string> paths))
                {
                    paths = new List<string>();
                    spritesByName.Add(sprite.name, paths);
                }

                paths.Add(Path.GetFileName(path) + "#" + localId);
            }
        }
    }
}
