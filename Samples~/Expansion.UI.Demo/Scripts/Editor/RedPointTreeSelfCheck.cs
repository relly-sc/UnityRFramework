using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 红点聚合、批量刷新、多视图绑定和删除行为的轻量回归检查。
    /// </summary>
    internal static class RedPointTreeSelfCheck
    {
        [MenuItem("GameObject/UnityRFramework/红点树自检", false, 24)]
        private static void Run()
        {
            GameObject root = new GameObject("RedPointTreeSelfCheck");
            try
            {
                RedPointTree tree = root.AddComponent<RedPointTree>();
                RedPointView first = CreateView(root.transform, tree, "Mail/System");
                RedPointView second = CreateView(root.transform, tree, "Mail/System");

                tree.SetValue("Mail/System", 2);
                tree.SetValue("Mail/Friend", 3);
                tree.FlushPendingChanges();

                Require(tree.GetValue("Mail") == 5, "父节点没有聚合两个叶节点的数值。");
                Require(first.Value == 2 && second.Value == 2, "同一节点的多个视图没有同步刷新。");

                UnityEngine.Object.DestroyImmediate(first.gameObject);
                tree.SetValue("Mail/System", 4);
                tree.FlushPendingChanges();
                Require(second.Value == 4 && tree.GetValue("Mail") == 7,
                    "视图销毁后继续更新节点失败。");

                Require(tree.Remove("Mail/System"), "已有节点删除失败。");
                tree.FlushPendingChanges();
                Require(second.Value == 0 && tree.GetValue("Mail") == 3,
                    "删除节点后视图或父节点数值不正确。");

                tree.SetValue("Mail/System/Notice", 6);
                tree.Remove("Mail/System");
                tree.SetValue("Mail/System/Notice", 8);
                tree.FlushPendingChanges();
                Require(second.Value == 8 && tree.GetValue("Mail") == 11,
                    "同帧删除并重建节点后保留了旧的删除通知。");

                Debug.Log("[红点树自检] 通过。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RedPointView CreateView(Transform parent, RedPointTree tree, string path)
        {
            GameObject viewObject = new GameObject("RedPointView", typeof(RectTransform));
            viewObject.transform.SetParent(parent, false);
            RedPointView view = viewObject.AddComponent<RedPointView>();

            GameObject indicator = new GameObject("Indicator", typeof(RectTransform), typeof(Image));
            indicator.transform.SetParent(viewObject.transform, false);
            GameObject textObject = new GameObject("Count", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(indicator.transform, false);

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("indicatorRoot").objectReferenceValue = indicator;
            serialized.FindProperty("countText").objectReferenceValue = textObject.GetComponent<Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.Configure(tree, path);
            return view;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("[红点树自检] " + message);
            }
        }
    }
}
