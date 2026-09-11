#if UNITY_EDITOR

using RFramework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>StorageComponent 自定义 Inspector。</summary>
    [CustomEditor(typeof(Runtime.StorageComponent))]
    public sealed class StorageComponentEditor : UnityRFrameworkComponentEditorBase
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty helper = serializedObject.FindProperty("storageHelperTypeName");
            helper.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Storage Helper", helper.stringValue, typeof(Runtime.StorageHelperBase));

            SerializedProperty serializer = serializedObject.FindProperty(
                "storageSerializerTypeName");
            serializer.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Storage Serializer", serializer.stringValue, typeof(IStorageSerializer));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("storageDirectoryName"),
                new GUIContent("存档目录", "Application.persistentDataPath 下的相对目录。"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("defaultVersion"),
                new GUIContent("默认存档版本"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("defaultCompressionMode"),
                new GUIContent("默认压缩"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("defaultProtectionMode"),
                new GUIContent("默认数据保护"));
            SerializedProperty protectionMode = serializedObject.FindProperty(
                "defaultProtectionMode");
            if (protectionMode.enumValueIndex != 0)
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("defaultProtectionKeyId"),
                    new GUIContent(
                        "存档密钥标识",
                        "默认使用 SaveKey；轮换时使用 SaveKey.版本号，并保留旧密钥直到存档迁移完成。"));
                SerializedProperty automaticallyManage = serializedObject.FindProperty(
                    "automaticallyManageSaveKey");
                EditorGUILayout.PropertyField(
                    automaticallyManage,
                    new GUIContent(
                        "自动管理安装级密钥",
                        "仅启用加密时创建密钥；关闭后由项目代码注入 IKeyStore 或 IDataProtector。"));

                EditorGUILayout.HelpBox(
                    automaticallyManage.boolValue
                        ? "自动管理会为本次安装生成随机 SaveKey。Windows 项目导入 "
                          + "Expansion.Security.Windows 后自动使用当前用户 DPAPI；未导入时使用基础文件密钥仓。"
                        : "自动管理已关闭。保存或加载加密存档前，必须调用 "
                          + "SetManagedSaveKeyStore 或 SetDataProtector。",
                    automaticallyManage.boolValue ? MessageType.Info : MessageType.Warning);
            }
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("createBackupByDefault"),
                new GUIContent("默认创建备份"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("recoverFromBackupByDefault"),
                new GUIContent("默认从备份恢复"));

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
