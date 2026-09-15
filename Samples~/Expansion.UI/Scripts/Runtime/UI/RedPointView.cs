using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将红点节点值映射到一个 UGUI 标记和可选数量文本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedPointView : MonoBehaviour
    {
        [SerializeField] private RedPointTree tree;
        [SerializeField] private string path;
        [SerializeField] private GameObject indicatorRoot;
        [SerializeField] private Text countText;

        private bool bound;

        /// <summary>获取当前显示的节点值。</summary>
        public int Value { get; private set; }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>在运行时设置红点树与节点路径。</summary>
        /// <param name="targetTree">目标红点树。</param>
        /// <param name="targetPath">目标节点路径。</param>
        public void Configure(RedPointTree targetTree, string targetPath)
        {
            Unbind();
            tree = targetTree;
            path = targetPath;
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        /// <summary>由红点树刷新当前显示值。</summary>
        /// <param name="value">最新聚合值。</param>
        internal void SetValue(int value)
        {
            Value = value;
            if (indicatorRoot != null && indicatorRoot != gameObject)
            {
                indicatorRoot.SetActive(value > 0);
            }

            if (countText != null)
            {
                countText.text = value > 0 ? value.ToString() : string.Empty;
            }
        }

        private void Bind()
        {
            if (!bound && tree != null && !string.IsNullOrWhiteSpace(path))
            {
                tree.Bind(this, path);
                bound = true;
            }
        }

        private void Unbind()
        {
            if (bound && tree != null)
            {
                tree.Unbind(this, path);
            }

            bound = false;
        }
    }
}
