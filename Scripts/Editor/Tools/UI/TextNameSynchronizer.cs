using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将所选层级中的 GameObject 名称同步到 UGUI Text。
    /// </summary>
    internal static class TextNameSynchronizer
    {
        private const string MenuPath =
            "GameObject/UnityRFramework/名称同步到子级 Text";

        /// <summary>
        /// 同步所选对象及其子级中的 UGUI Text 内容。
        /// </summary>
        [MenuItem(MenuPath, false, -996)]
        private static void SynchronizeSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            List<Text> texts = new List<Text>();
            HashSet<int> textIds = new HashSet<int>();
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject root = selected[i];
                if (root == null || HasSelectedAncestor(root.transform))
                {
                    continue;
                }

                Text[] children = root.GetComponentsInChildren<Text>(true);
                for (int textIndex = 0;
                     textIndex < children.Length;
                     textIndex++)
                {
                    Text text = children[textIndex];
                    if (text != null && textIds.Add(text.GetInstanceID()))
                    {
                        texts.Add(text);
                    }
                }
            }

            if (texts.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "名称同步到子级 Text",
                    "所选对象及其子级中没有 UGUI Text。",
                    "确定");
                return;
            }

            Undo.RecordObjects(
                texts.ToArray(),
                "Synchronize Names To UGUI Text");
            for (int i = 0; i < texts.Count; i++)
            {
                Text text = texts[i];
                text.text = ResolveSourceName(text);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                EditorUtility.SetDirty(text);
            }
        }

        /// <summary>
        /// 判断当前选择是否包含场景 GameObject。
        /// </summary>
        /// <returns>至少选择一个场景 GameObject 时返回 true。</returns>
        [MenuItem(MenuPath, true)]
        private static bool ValidateSynchronizeSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] != null && selected[i].scene.IsValid())
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveSourceName(Text text)
        {
            Button button = text.GetComponentInParent<Button>();
            if (button != null)
            {
                return button.gameObject.name;
            }

            if (text.transform.parent != null
                && string.Equals(
                    text.gameObject.name,
                    "Text",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return text.transform.parent.gameObject.name;
            }

            return text.gameObject.name;
        }

        private static bool HasSelectedAncestor(Transform transform)
        {
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (Selection.Contains(parent.gameObject))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }
    }
}
