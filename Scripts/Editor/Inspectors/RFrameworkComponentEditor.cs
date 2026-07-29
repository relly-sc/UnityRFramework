#if UNITY_EDITOR

using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 框架组件 Inspector 基类。
    /// 在编辑器运行状态下统一绘制模块只读运行信息并持续刷新。
    /// </summary>
    public abstract class RFrameworkComponentEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 绘制当前组件的运行信息。
        /// </summary>
        protected void DrawRuntimeInformation()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            ComponentEditorUtility.DrawRuntimeInformation(
                target as Runtime.UnityRFrameworkComponent);
        }

        /// <summary>
        /// 运行时持续刷新 Inspector 中的动态统计值。
        /// </summary>
        public override bool RequiresConstantRepaint()
        {
            return EditorApplication.isPlaying;
        }
    }

    /// <summary>
    /// 未提供专用 Inspector 的框架组件通用 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.UnityRFrameworkComponent), true)]
    public sealed class UnityRFrameworkComponentEditor : RFrameworkComponentEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawRuntimeInformation();
        }
    }
}

#endif
