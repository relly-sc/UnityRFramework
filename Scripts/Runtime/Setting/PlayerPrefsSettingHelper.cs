using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>使用 Unity PlayerPrefs 保存非敏感应用偏好。</summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class PlayerPrefsSettingHelper : ISettingHelper
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
