using System;
using System.Collections.Generic;
using NUnit.Framework;
using RFramework;
using UnityEditor;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    public sealed class SettingModuleTests
    {
        [Test]
        public void PlayerPrefsSettingsSurviveModuleRestart()
        {
            string prefix = "UnityRFramework.SettingTest." + Guid.NewGuid().ToString("N");
            string volume = prefix + ".Volume";
            string language = prefix + ".Language";
            string fullscreen = prefix + ".Fullscreen";
            try
            {
                ISettingModule settings = RFrameworkModuleHost.Get<ISettingModule>();
                settings.SetHelper(new PlayerPrefsSettingHelper());
                Assert.AreEqual(nameof(PlayerPrefsSettingHelper), settings.HelperName);
                Assert.IsFalse(settings.HasPendingChanges);
                Assert.AreEqual(0.5f, settings.GetFloat(volume, 0.5f));
                Assert.AreEqual("zh-CN", settings.GetString(language, "zh-CN"));
                Assert.IsTrue(settings.GetBool(fullscreen, true));

                settings.SetFloat(volume, 0.25f);
                settings.SetString(language, "en");
                settings.SetBool(fullscreen, false);
                Assert.IsTrue(settings.HasPendingChanges);
                RFrameworkModuleHost.StopAll();

                settings = RFrameworkModuleHost.Get<ISettingModule>();
                settings.SetHelper(new PlayerPrefsSettingHelper());
                Assert.IsFalse(settings.HasPendingChanges);
                Assert.AreEqual(0.25f, settings.GetFloat(volume));
                Assert.AreEqual("en", settings.GetString(language));
                Assert.IsFalse(settings.GetBool(fullscreen, true));
                Assert.IsTrue(settings.HasKey(language));
                settings.DeleteKey(language);
                settings.Save();
                Assert.IsFalse(settings.HasPendingChanges);
                Assert.IsFalse(settings.HasKey(language));
            }
            finally
            {
                PlayerPrefs.DeleteKey(volume);
                PlayerPrefs.DeleteKey(language);
                PlayerPrefs.DeleteKey(fullscreen);
                PlayerPrefs.Save();
                RFrameworkModuleHost.StopAll();
            }
        }

        [Test]
        public void FrameworkPrefabsContainSettingChild()
        {
            string[] guids = AssetDatabase.FindAssets(
                "UnityRFramework t:Prefab", new[] { "Assets/UnityRFramework" });
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/UnityRFramework.prefab", StringComparison.Ordinal)) continue;
                paths.Add(path);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.AreEqual(0, prefab.GetComponents<SettingComponent>().Length, path);
                Transform child = prefab.transform.Find("Setting");
                Assert.NotNull(child, path);
                Assert.AreEqual(1, child.GetComponents<SettingComponent>().Length, path);
                Assert.AreEqual(1, prefab.GetComponentsInChildren<SettingComponent>(true).Length, path);
                var serialized = new SerializedObject(child.GetComponent<SettingComponent>());
                Assert.AreEqual("UnityRFramework.Runtime.PlayerPrefsSettingHelper",
                    serialized.FindProperty("settingHelperTypeName").stringValue, path);
            }

            Assert.GreaterOrEqual(paths.Count, 1);
        }
    }
}
