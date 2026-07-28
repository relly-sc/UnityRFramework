using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将所选对象上的 BoxCollider 适配到全部子 Renderer 的世界包围盒。
    /// </summary>
    internal static class ColliderFitUtility
    {
        private const string MenuPath =
            "GameObject/UnityRFramework/适配 BoxCollider 到 Renderer";

        /// <summary>
        /// 为所有所选对象适配 BoxCollider。
        /// </summary>
        [MenuItem(MenuPath, false, -997)]
        private static void FitSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            int fittedCount = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (TryFit(selected[i]))
                {
                    fittedCount++;
                }
            }

            if (fittedCount == 0)
            {
                EditorUtility.DisplayDialog(
                    "适配 BoxCollider",
                    "所选对象及其子节点中没有可用 Renderer。",
                    "确定");
            }
        }

        /// <summary>
        /// 判断当前选择是否包含 GameObject。
        /// </summary>
        /// <returns>至少选择一个 GameObject 时返回 true。</returns>
        [MenuItem(MenuPath, true)]
        private static bool ValidateFitSelected()
        {
            return Selection.gameObjects.Length > 0;
        }

        private static bool TryFit(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            bool hasBounds = false;
            Bounds localBounds = default;
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 minimum = worldBounds.min;
                Vector3 maximum = worldBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldPoint = new Vector3(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z);
                    Vector3 localPoint =
                        root.transform.InverseTransformPoint(worldPoint);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(root);
            }
            else
            {
                Undo.RecordObject(collider, "Fit BoxCollider");
            }

            collider.center = localBounds.center;
            collider.size = localBounds.size;
            EditorUtility.SetDirty(collider);
            return true;
        }
    }
}
