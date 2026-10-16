using System;
using System.Collections;
using System.Reflection;
using RFramework;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Runtime
{
    /// <summary>按本地化键自动更新 UGUI Text 或 TextMeshProUGUI 文本。</summary>
    [AddComponentMenu("UnityRFramework/UI/本地化文本")]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("语言表中的 Key；仅用于不需要业务参数的静态文字。")]
        private string key;

        [SerializeField]
        [Tooltip("目标 UGUI Text 或 TextMeshProUGUI；留空时使用当前物体上的文本组件。")]
        private Graphic target;

        private EventComponent eventSource;
        private Coroutine waitForLanguage;
        private PropertyInfo tmpTextProperty;

        /// <summary>供编辑器检查工具读取的本地化键。</summary>
        public string Key => key;

        private void Awake()
        {
            if (target == null) target = GetComponent<Graphic>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(key)) return;
            if (target == null) target = GetComponent<Graphic>();
            Bind();
            bool refreshed = RefreshIfReady();
            if (eventSource == null || !refreshed)
            {
                waitForLanguage = StartCoroutine(WaitForLanguage());
            }
        }

        private IEnumerator WaitForLanguage()
        {
            while (isActiveAndEnabled)
            {
                Bind();
                if (eventSource != null && RefreshIfReady())
                {
                    waitForLanguage = null;
                    yield break;
                }

                yield return null;
            }
        }

        private void OnDisable()
        {
            if (waitForLanguage != null)
            {
                StopCoroutine(waitForLanguage);
                waitForLanguage = null;
            }

            if (eventSource != null)
            {
                eventSource.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
                eventSource = null;
            }
        }

        private void OnLanguageChanged(LanguageChangedEvent _)
        {
            RefreshIfReady();
        }

        private void Bind()
        {
            if (eventSource != null) return;
            EventComponent events = GameEntry.Event;
            if (events == null) return;
            events.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            eventSource = events;
        }

        /// <summary>以当前语言重新设置文字；语言尚未加载时保持原文。</summary>
        public void Refresh()
        {
            RefreshIfReady();
        }

        private bool RefreshIfReady()
        {
            LocalizationComponent localization = GameEntry.Localization;
            if (localization == null || string.IsNullOrEmpty(localization.CurrentLanguage))
            {
                return false;
            }

            if (target == null) target = GetComponent<Graphic>();
            if (target is Text uguiText)
            {
                uguiText.text = localization.GetString(key);
            }
            else if (target != null && IsTmpText(target.GetType()))
            {
                tmpTextProperty ??= target.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                tmpTextProperty?.SetValue(target, localization.GetString(key));
            }
            else
            {
                Debug.LogWarning($"LocalizedText '{name}' needs a UGUI Text or TextMeshProUGUI target.", this);
            }

            return true;
        }

        private static bool IsTmpText(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.FullName == "TMPro.TMP_Text") return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (target == null) target = GetComponent<Graphic>();
        }
#endif
    }
}
