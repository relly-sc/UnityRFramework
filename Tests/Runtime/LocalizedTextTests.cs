using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityRFramework.Runtime;
using Object = UnityEngine.Object;

namespace UnityRFramework.Tests
{
    public sealed class LocalizedTextTests
    {
        [UnityTest]
        public IEnumerator UgUiAndOptionalTmpTextFollowLanguageChanges()
        {
            GameObject framework = new GameObject("LocalizedTextTestFramework");
            GameObject ui = new GameObject("LocalizedTextTestUI");
            GameObject tmpObject = null;
            ui.SetActive(false);
            try
            {
                framework.AddComponent<UnityRFrameworkController>();
                framework.AddComponent<EventComponent>();
                LocalizationComponent localization = framework.AddComponent<LocalizationComponent>();
                typeof(LocalizationComponent).GetField("loadDefaultLanguageOnStart",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(localization, false);

                Text ugui = ui.AddComponent<Text>();
                AddLocalizedText(ui);

                Type tmpType = GetOptionalTmpType();
                Graphic tmp = null;
                if (tmpType != null)
                {
                    tmpObject = new GameObject("LocalizedTextTestTMP");
                    tmpObject.SetActive(false);
                    tmp = (Graphic)tmpObject.AddComponent(tmpType);
                    AddLocalizedText(tmpObject);
                    tmpObject.SetActive(true);
                }

                ui.SetActive(true);
                localization.LoadLanguageFromString("zh-CN",
                    "[{\"Key\":\"title\",\"Value\":\"中文\"}]");
                localization.LoadLanguageFromString("en",
                    "[{\"Key\":\"title\",\"Value\":\"English\"}]");
                localization.SwitchLanguage("zh-CN");
                yield return null;
                Assert.AreEqual("中文", ugui.text);
                if (tmp != null) Assert.AreEqual("中文", ReadText(tmp));

                localization.SwitchLanguage("en");
                Assert.AreEqual("English", ugui.text);
                if (tmp != null) Assert.AreEqual("English", ReadText(tmp));

            }
            finally
            {
                if (tmpObject != null) Object.Destroy(tmpObject);
                Object.Destroy(ui);
                Object.Destroy(framework);
            }
        }

        private static void AddLocalizedText(GameObject gameObject)
        {
            LocalizedText component = gameObject.AddComponent<LocalizedText>();
            typeof(LocalizedText).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(component, "title");
        }

        private static string ReadText(Graphic graphic)
        {
            return (string)graphic.GetType().GetProperty("text")?.GetValue(graphic);
        }

        private static Type GetOptionalTmpType()
        {
            try
            {
                return Assembly.Load("Unity.TextMeshPro")
                    .GetType("TMPro.TextMeshProUGUI", throwOnError: true);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
