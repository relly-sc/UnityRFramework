using NUnit.Framework;

namespace UnityRFramework.Editor.Tests
{
    public sealed class WebGLTemplateGeneratorTests
    {
        [Test]
        public void BuildIndexHtml_ContainsUnityWebGLPlaceholders()
        {
            string html = UnityRFramework.Editor.WebGLTemplateGenerator.BuildIndexHtml();

            StringAssert.Contains("{{{ LOADER_FILENAME }}}", html);
            StringAssert.Contains("{{{ DATA_FILENAME }}}", html);
            StringAssert.Contains("{{{ FRAMEWORK_FILENAME }}}", html);
            StringAssert.Contains("{{{ CODE_FILENAME }}}", html);
            StringAssert.Contains("{{{ PRODUCT_NAME }}}", html);
            StringAssert.Contains("unity-fullscreen-button", html);
            StringAssert.Contains("url(\"load.png\")", html);
            StringAssert.Contains("fullscreen.png", html);
            StringAssert.Contains("display: none", html);
            StringAssert.Contains("requestFullscreen", html);
            StringAssert.Contains("unityShowBanner", html);
            StringAssert.Contains("OpenUrl_Window", html);
            StringAssert.Contains("SelectFile", html);
            StringAssert.Contains("SetCookie", html);
        }
    }
}
