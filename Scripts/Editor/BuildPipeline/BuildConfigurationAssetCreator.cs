using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建步骤配置资产的创建工具：在 Profile 默认目录创建指定类型的配置资产，
    /// 创建后选中并 Ping，供顶层菜单与 Assets/Create 流程共用。
    /// 创建位置与 Profile 资产保持一致，便于统一管理。
    /// </summary>
    public static class BuildConfigurationAssetCreator
    {
        /// <summary>
        /// 创建指定类型的配置资产并选中。
        /// </summary>
        /// <typeparam name="T">配置资产类型。</typeparam>
        /// <param name="defaultName">默认资产名称（不含扩展名）；空白时使用类型名。</param>
        /// <returns>创建成功的资产；创建失败时返回 null。</returns>
        public static T Create<T>(string defaultName)
            where T : ScriptableObject
        {
            string directory = UnityRFrameworkBuildProfile.DefaultAssetDirectory;
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            string name = string.IsNullOrWhiteSpace(defaultName)
                ? typeof(T).Name
                : defaultName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{directory}/{name}.asset");

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        /// <summary>
        /// 按运行时类型创建配置资产并选中；供配置目录等无法静态确定泛型参数的场景使用。
        /// </summary>
        /// <param name="type">配置资产类型；须派生自 ScriptableObject。</param>
        /// <param name="defaultName">默认资产名称（不含扩展名）；空白时使用类型名。</param>
        /// <returns>创建成功的资产；参数无效或创建失败时返回 null。</returns>
        public static ScriptableObject Create(Type type, string defaultName)
        {
            if (type == null
                || !typeof(ScriptableObject).IsAssignableFrom(type)
                || type.IsAbstract)
            {
                return null;
            }

            string directory = UnityRFrameworkBuildProfile.DefaultAssetDirectory;
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            string name = string.IsNullOrWhiteSpace(defaultName)
                ? type.Name
                : defaultName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{directory}/{name}.asset");

            ScriptableObject asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }
    }
}
