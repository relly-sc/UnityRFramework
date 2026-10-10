using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>应用基础设置入口，与 Storage 存档模块独立。</summary>
    [AddComponentMenu("UnityRFramework/Setting")]
    [DisallowMultipleComponent]
    public sealed class SettingComponent : UnityRFrameworkComponent
    {
        private const string DefaultHelperTypeName =
            "UnityRFramework.Runtime.PlayerPrefsSettingHelper";

        [SerializeField]
        [Tooltip("设置存储辅助器类型全名；默认使用 Unity PlayerPrefs。")]
        private string settingHelperTypeName = DefaultHelperTypeName;

        private ISettingModule settingModule;

        public string HelperName => settingModule?.HelperName ?? "None";
        public bool HasPendingChanges => settingModule != null && settingModule.HasPendingChanges;

        protected override void Awake()
        {
            base.Awake();
            settingModule = RFrameworkModuleHost.Get<ISettingModule>();
            if (string.IsNullOrWhiteSpace(settingHelperTypeName))
                settingHelperTypeName = DefaultHelperTypeName;

            Type type = Utility.Assembly.GetType(settingHelperTypeName);
            if (type == null || !typeof(ISettingHelper).IsAssignableFrom(type))
                throw new RFrameworkException(
                    $"Setting helper '{settingHelperTypeName}' is missing or invalid.");

            try { SetHelper((ISettingHelper)Activator.CreateInstance(type)); }
            catch (Exception exception)
            {
                throw new RFrameworkException(
                    $"Setting helper '{settingHelperTypeName}' could not be created.", exception);
            }
        }

        /// <summary>在项目需要其他存储方式时替换默认 Helper。</summary>
        public void SetHelper(ISettingHelper helper) => settingModule.SetHelper(helper);

        public bool HasKey(string key) => settingModule.HasKey(key);
        public void DeleteKey(string key) => settingModule.DeleteKey(key);
        public bool GetBool(string key, bool defaultValue = false) =>
            settingModule.GetBool(key, defaultValue);
        public void SetBool(string key, bool value) => settingModule.SetBool(key, value);
        public int GetInt(string key, int defaultValue = 0) =>
            settingModule.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => settingModule.SetInt(key, value);
        public float GetFloat(string key, float defaultValue = 0f) =>
            settingModule.GetFloat(key, defaultValue);
        public void SetFloat(string key, float value) => settingModule.SetFloat(key, value);
        public string GetString(string key, string defaultValue = "") =>
            settingModule.GetString(key, defaultValue);
        public void SetString(string key, string value) => settingModule.SetString(key, value);
        public void Save() => settingModule.Save();

        private void OnApplicationPause(bool paused)
        {
            if (paused) settingModule?.Save();
        }
    }
}
