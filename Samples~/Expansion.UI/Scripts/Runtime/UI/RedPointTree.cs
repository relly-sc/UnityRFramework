using System;
using System.Collections.Generic;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 按斜杠路径维护父子聚合值，并在帧末合并刷新红点视图和事件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedPointTree : MonoBehaviour
    {
        private sealed class Node
        {
            public string Path;
            public string ParentPath;
            public int LocalValue;
            public int Value;
            public readonly HashSet<string> Children = new HashSet<string>();
        }

        private readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>();
        private readonly Dictionary<string, List<RedPointView>> views =
            new Dictionary<string, List<RedPointView>>();
        private readonly HashSet<string> dirtyPaths = new HashSet<string>();
        private readonly HashSet<string> removedPaths = new HashSet<string>();
        private readonly List<string> flushPaths = new List<string>();

        /// <summary>获取当前节点数量。</summary>
        public int NodeCount => nodes.Count;

        private void LateUpdate()
        {
            FlushPendingChanges();
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>设置节点自身值；父节点会聚合自身值与全部直接子节点值。</summary>
        /// <param name="path">以斜杠分隔的节点路径。</param>
        /// <param name="value">非负数值。</param>
        public void SetValue(string path, int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            string normalizedPath = NormalizePath(path);
            Node node = EnsureNode(normalizedPath);
            if (node.LocalValue == value)
            {
                return;
            }

            node.LocalValue = value;
            MarkDirty(normalizedPath);
        }

        /// <summary>获取节点最近一次刷新完成后的聚合值。</summary>
        /// <param name="path">节点路径。</param>
        /// <returns>节点不存在时返回 0。</returns>
        public int GetValue(string path)
        {
            string normalizedPath = NormalizePath(path);
            return nodes.TryGetValue(normalizedPath, out Node node) ? node.Value : 0;
        }

        /// <summary>删除指定节点及其全部后代。</summary>
        /// <param name="path">节点路径。</param>
        /// <returns>是否删除了节点。</returns>
        public bool Remove(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!nodes.TryGetValue(normalizedPath, out Node node))
            {
                return false;
            }

            flushPaths.Clear();
            CollectDescendants(normalizedPath, flushPaths);
            for (int i = 0; i < flushPaths.Count; i++)
            {
                string removedPath = flushPaths[i];
                nodes.Remove(removedPath);
                dirtyPaths.Remove(removedPath);
                removedPaths.Add(removedPath);
            }

            if (!string.IsNullOrEmpty(node.ParentPath)
                && nodes.TryGetValue(node.ParentPath, out Node parent))
            {
                parent.Children.Remove(normalizedPath);
                MarkDirty(node.ParentPath);
            }

            return true;
        }

        /// <summary>立即处理当前累计的变化；通常由 LateUpdate 自动调用。</summary>
        public void FlushPendingChanges()
        {
            if (dirtyPaths.Count == 0 && removedPaths.Count == 0)
            {
                return;
            }

            flushPaths.Clear();
            flushPaths.AddRange(dirtyPaths);
            flushPaths.Sort((left, right) => GetDepth(right).CompareTo(GetDepth(left)));
            for (int i = 0; i < flushPaths.Count; i++)
            {
                string path = flushPaths[i];
                if (!nodes.TryGetValue(path, out Node node))
                {
                    continue;
                }

                long total = node.LocalValue;
                foreach (string childPath in node.Children)
                {
                    if (nodes.TryGetValue(childPath, out Node child))
                    {
                        total += child.Value;
                    }
                }

                int value = (int)Math.Min(int.MaxValue, total);
                if (node.Value != value)
                {
                    node.Value = value;
                    Publish(path, value);
                }
            }

            foreach (string path in removedPaths)
            {
                Publish(path, 0);
            }

            dirtyPaths.Clear();
            removedPaths.Clear();
        }

        /// <summary>移除全部节点并将已绑定视图刷新为隐藏。</summary>
        public void Clear()
        {
            foreach (KeyValuePair<string, List<RedPointView>> pair in views)
            {
                UpdateViews(pair.Value, 0);
            }

            nodes.Clear();
            dirtyPaths.Clear();
            removedPaths.Clear();
            flushPaths.Clear();
        }

        /// <summary>绑定一个红点视图，并立即同步最近一次刷新完成的值。</summary>
        /// <param name="view">目标视图。</param>
        /// <param name="path">节点路径。</param>
        internal void Bind(RedPointView view, string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!views.TryGetValue(normalizedPath, out List<RedPointView> targets))
            {
                targets = new List<RedPointView>();
                views.Add(normalizedPath, targets);
            }

            if (!targets.Contains(view))
            {
                targets.Add(view);
            }

            view.SetValue(nodes.TryGetValue(normalizedPath, out Node node) ? node.Value : 0);
        }

        /// <summary>解除红点视图绑定。</summary>
        /// <param name="view">目标视图。</param>
        /// <param name="path">节点路径。</param>
        internal void Unbind(RedPointView view, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalizedPath = NormalizePath(path);
            if (views.TryGetValue(normalizedPath, out List<RedPointView> targets))
            {
                targets.Remove(view);
                if (targets.Count == 0)
                {
                    views.Remove(normalizedPath);
                }
            }
        }

        private Node EnsureNode(string path)
        {
            string[] parts = path.Split('/');
            string parentPath = null;
            Node node = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string currentPath = parentPath == null ? parts[i] : parentPath + "/" + parts[i];
                if (!nodes.TryGetValue(currentPath, out node))
                {
                    node = new Node { Path = currentPath, ParentPath = parentPath };
                    nodes.Add(currentPath, node);
                    if (parentPath != null)
                    {
                        nodes[parentPath].Children.Add(currentPath);
                    }
                }

                removedPaths.Remove(currentPath);

                parentPath = currentPath;
            }

            return node;
        }

        private void MarkDirty(string path)
        {
            string currentPath = path;
            while (!string.IsNullOrEmpty(currentPath) && nodes.TryGetValue(currentPath, out Node node))
            {
                dirtyPaths.Add(currentPath);
                currentPath = node.ParentPath;
            }
        }

        private void CollectDescendants(string path, List<string> results)
        {
            if (!nodes.TryGetValue(path, out Node node))
            {
                return;
            }

            foreach (string childPath in node.Children)
            {
                CollectDescendants(childPath, results);
            }

            results.Add(path);
        }

        private void Publish(string path, int value)
        {
            if (views.TryGetValue(path, out List<RedPointView> targets))
            {
                UpdateViews(targets, value);
            }

            GameEntry.Event?.Fire(new RedPointChangedEvent(path, value));
        }

        private static void UpdateViews(List<RedPointView> targets, int value)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                RedPointView view = targets[i];
                if (view == null)
                {
                    targets.RemoveAt(i);
                }
                else
                {
                    view.SetValue(value);
                }
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Red point path cannot be empty.", nameof(path));
            }

            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                throw new ArgumentException("Red point path cannot be empty.", nameof(path));
            }

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
                if (parts[i].Length == 0)
                {
                    throw new ArgumentException("Red point path contains an empty segment.", nameof(path));
                }
            }

            return string.Join("/", parts);
        }

        private static int GetDepth(string path)
        {
            int depth = 1;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/')
                {
                    depth++;
                }
            }

            return depth;
        }
    }
}
