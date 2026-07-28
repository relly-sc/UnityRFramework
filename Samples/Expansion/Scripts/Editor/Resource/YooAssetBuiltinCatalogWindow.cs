using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 为 YooAsset Host 模式生成内置资源目录文件。
    /// Package 列表来自 BundleCollectorSetting，输出目录使用 YooAsset 当前的
    /// StreamingAssets 根目录。
    /// </summary>
    public sealed class YooAssetBuiltinCatalogWindow : EditorWindow
    {
        private string[] packageNames = Array.Empty<string>();
        private int selectedPackageIndex;

        /// <summary>
        /// 打开 YooAsset 内置目录工具。
        /// </summary>
        [MenuItem("UnityRFramework/Expansion/YooAsset Builtin Catalog")]
        public static void Open()
        {
            YooAssetBuiltinCatalogWindow window =
                GetWindow<YooAssetBuiltinCatalogWindow>("YooAsset 内置目录");
            window.minSize = new Vector2(520f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPackages();
        }

        private void OnFocus()
        {
            RefreshPackages();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("YooAsset Host 内置目录", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Host 模式仍会初始化 BuiltinFileSystem。纯远程包需要空 "
                + "BuiltinCatalog；包含首包资源时，需要根据 StreamingAssets "
                + "中的实际文件重新生成 Catalog。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Package", GUILayout.Width(80f));
                if (packageNames.Length > 0)
                {
                    selectedPackageIndex = EditorGUILayout.Popup(
                        selectedPackageIndex,
                        packageNames);
                }
                else
                {
                    EditorGUILayout.LabelField("未找到 BundleCollector Package");
                }

                if (GUILayout.Button("刷新", GUILayout.Width(64f)))
                {
                    RefreshPackages();
                }
            }

            string packageRoot = GetSelectedPackageRoot();
            EditorGUILayout.LabelField("输出目录");
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(packageRoot) ? "-" : packageRoot,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(packageNames.Length == 0))
            {
                if (GUILayout.Button("生成空 Catalog（全部资源远程）", GUILayout.Height(30f)))
                {
                    GenerateEmptyCatalog();
                }

                if (GUILayout.Button(
                        "根据内置目录生成 Catalog（包含首包资源）",
                        GUILayout.Height(30f)))
                {
                    GenerateCatalogFromBuiltinFiles();
                }
            }
        }

        private void RefreshPackages()
        {
            string previousPackage = GetSelectedPackageName();
            BundleCollectorSetting setting = BundleCollectorSettingData.Setting;
            List<string> names = setting == null
                ? new List<string>()
                : setting.Packages
                    .Where(package => package != null
                                      && !string.IsNullOrWhiteSpace(package.PackageName))
                    .Select(package => package.PackageName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

            packageNames = names.ToArray();
            selectedPackageIndex = string.IsNullOrEmpty(previousPackage)
                ? 0
                : Math.Max(0, Array.IndexOf(packageNames, previousPackage));
            Repaint();
        }

        private void GenerateEmptyCatalog()
        {
            string packageName = RequireSelectedPackageName();
            string packageRoot = GetPackageRoot(packageName);
            Directory.CreateDirectory(packageRoot);

            if (!BuiltinCatalogHelper.CreateEmptyFile(
                    packageName,
                    string.Empty,
                    packageRoot))
            {
                throw new InvalidOperationException(
                    $"创建空 BuiltinCatalog 失败：{packageRoot}");
            }

            AssetDatabase.Refresh();
            Debug.Log(
                $"[UnityRFramework.Expansion] 已为空远程包 '{packageName}' 生成 "
                + $"BuiltinCatalog：{packageRoot}");
        }

        private void GenerateCatalogFromBuiltinFiles()
        {
            string packageName = RequireSelectedPackageName();
            string packageRoot = GetPackageRoot(packageName);
            if (!Directory.Exists(packageRoot))
            {
                throw new DirectoryNotFoundException(
                    $"内置包目录不存在：{packageRoot}");
            }

            if (!BuiltinCatalogHelper.CreateFile(null, packageName, packageRoot))
            {
                throw new InvalidOperationException(
                    $"根据内置目录生成 BuiltinCatalog 失败：{packageRoot}");
            }

            AssetDatabase.Refresh();
            Debug.Log(
                $"[UnityRFramework.Expansion] 已根据内置文件为包 '{packageName}' "
                + $"生成 BuiltinCatalog：{packageRoot}");
        }

        private string GetSelectedPackageRoot()
        {
            string packageName = GetSelectedPackageName();
            return string.IsNullOrEmpty(packageName)
                ? string.Empty
                : GetPackageRoot(packageName);
        }

        private string GetSelectedPackageName()
        {
            return selectedPackageIndex >= 0
                   && selectedPackageIndex < packageNames.Length
                ? packageNames[selectedPackageIndex]
                : string.Empty;
        }

        private string RequireSelectedPackageName()
        {
            string packageName = GetSelectedPackageName();
            if (string.IsNullOrEmpty(packageName))
            {
                throw new InvalidOperationException(
                    "BundleCollectorSetting 中没有可用的 Package。");
            }

            return packageName;
        }

        private static string GetPackageRoot(string packageName)
        {
            return Path.GetFullPath(
                Path.Combine(BundleBuilderHelper.GetStreamingAssetsRoot(), packageName));
        }
    }
}
