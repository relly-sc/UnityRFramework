using Obfuz.Unity;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Obfuz 构建配置 Inspector，仅提供第三方设置入口。
    /// </summary>
    [CustomEditor(typeof(ObfuzBuildConfiguration))]
    public sealed class ObfuzBuildConfigurationEditor : UnityEditor.Editor
    {
        /// <summary>绘制 Obfuz 构建配置。</summary>
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("打开 Obfuz 设置"))
            {
                ObfuzMenu.OpenSettings();
            }
        }
    }
}
