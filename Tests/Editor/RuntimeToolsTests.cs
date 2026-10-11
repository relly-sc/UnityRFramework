using NUnit.Framework;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    public sealed class RuntimeToolsTests
    {
        [Test]
        public void RectTransformExtensions_SetLayout()
        {
            GameObject gameObject = new GameObject("RuntimeToolsTest", typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();

            rect.SetSize(new Vector2(320f, 180f));
            rect.SetAnchors(new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));
            rect.SetPivot(new Vector2(0.5f, 0f));

            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(320f, 180f)));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.1f, 0.2f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.9f, 0.8f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));

            Object.DestroyImmediate(gameObject);
        }
    }
}
