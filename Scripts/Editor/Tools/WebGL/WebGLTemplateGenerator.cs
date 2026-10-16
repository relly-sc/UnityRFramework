using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Creates a project-local WebGL template compatible with Unity 2022.3.
    /// </summary>
    internal static class WebGLTemplateGenerator
    {
        private const string TemplateAssetPath =
            "Assets/WebGLTemplates/UnityRFramework";
        private const string IndexAssetPath =
            TemplateAssetPath + "/index.html";
        private const string LoadingBackgroundFileName = "load.png";
        private const string FullscreenIconFileName = "fullscreen.png";

        private static readonly UTF8Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        [MenuItem("UnityRFramework/生成 WebGL 模板", false, 20)]
        private static void Generate()
        {
            string absoluteFolder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                TemplateAssetPath.Replace('/', Path.DirectorySeparatorChar));
            string absoluteIndex = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                IndexAssetPath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(absoluteIndex)
                && !EditorUtility.DisplayDialog(
                    "生成 WebGL 模板",
                    "模板已存在，是否覆盖？\n" + IndexAssetPath,
                    "覆盖",
                    "取消"))
            {
                return;
            }

            Directory.CreateDirectory(absoluteFolder);
            CopyDefaultTemplateAsset(
                absoluteFolder,
                string.Empty,
                LoadingBackgroundFileName);
            CopyDefaultTemplateAsset(
                absoluteFolder,
                string.Empty,
                FullscreenIconFileName);
            File.WriteAllText(absoluteIndex, BuildIndexHtml(), Utf8WithoutBom);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "生成 WebGL 模板",
                "已生成：" + IndexAssetPath
                    + "\n请在 Player Settings > WebGL > Resolution and Presentation > WebGL Template 中选择 UnityRFramework。",
                "确定");
        }

        private static void CopyDefaultTemplateAsset(
            string targetFolder,
            string relativeFolder,
            string fileName)
        {
            string[] guids = AssetDatabase.FindAssets(
                "WebGLTemplateGenerator t:MonoScript");
            if (guids.Length == 0)
            {
                return;
            }

            string sourceScriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            string sourceFolder = Path.GetDirectoryName(
                Path.GetFullPath(sourceScriptPath));
            string sourcePath = Path.Combine(
                sourceFolder,
                relativeFolder,
                fileName);
            string destinationFolder = Path.Combine(targetFolder, relativeFolder);
            string destinationPath = Path.Combine(destinationFolder, fileName);

            if (!File.Exists(sourcePath) || File.Exists(destinationPath))
            {
                return;
            }

            Directory.CreateDirectory(destinationFolder);
            File.Copy(sourcePath, destinationPath);
        }

        internal static string BuildIndexHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
  <meta charset=""utf-8"">
  <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=no""></meta>
  <title>{{{ PRODUCT_NAME }}}</title>
  <style>
    html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: {{{ BACKGROUND_COLOR }}}; }
    #unity-container { position: relative; width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; background: url(""load.png"") center / cover no-repeat; }
    #unity-canvas { width: {{{ WIDTH }}}px; height: {{{ HEIGHT }}}px; max-width: 100vw; max-height: 100vh; background: transparent; }
    #unity-container:fullscreen, #unity-container:-webkit-full-screen { background: {{{ BACKGROUND_COLOR }}}; }
    #unity-container:fullscreen #unity-canvas, #unity-container:-webkit-full-screen #unity-canvas { width: 100vw; height: 100vh; }
    #unity-loading-bar { position: absolute; width: min(80vw, 420px); text-align: center; color: white; font-family: sans-serif; }
    #unity-progress-bar-empty { width: 100%; height: 8px; margin-top: 12px; background: rgba(255,255,255,.25); }
    #unity-progress-bar-full { width: 0%; height: 100%; background: white; }
    #unity-load-text { margin-top: 8px; }
    #unity-warning { position: absolute; top: 0; left: 0; right: 0; z-index: 2; color: #111; font-family: sans-serif; }
    #unity-warning div { padding: 10px; text-align: center; }
    #unity-browser-hint { position: absolute; bottom: 8px; left: 0; right: 0; z-index: 2; color: white; text-align: center; font: 12px sans-serif; }
    #unity-fullscreen-button { position: absolute; right: 12px; bottom: 12px; z-index: 3; width: 38px; height: 38px; padding: 0; border: 0; border-radius: 3px; background: rgba(0,0,0,.6) url(""fullscreen.png"") center / 24px 24px no-repeat; cursor: pointer; }
    #unity-container:fullscreen #unity-fullscreen-button, #unity-container:-webkit-full-screen #unity-fullscreen-button { display: none; }
    #unity-url-window { display: none; position: absolute; inset: 5%; z-index: 4; border: 0; background: white; }
    #unity-url-window iframe { width: 100%; height: 100%; border: 0; }
  </style>
</head>
<body>
  <div id=""unity-container"">
    <canvas id=""unity-canvas"" tabindex=""-1""></canvas>
    <div id=""unity-loading-bar"">
      <div>{{{ PRODUCT_NAME }}}</div>
      <div id=""unity-progress-bar-empty""><div id=""unity-progress-bar-full""></div></div>
      <div id=""unity-load-text"">加载中... 0%</div>
    </div>
    <div id=""unity-warning""></div>
    <div id=""unity-browser-hint"">推荐使用 Edge、Firefox 或 Chrome 浏览器</div>
    <button id=""unity-fullscreen-button"" type=""button"" aria-label=""全屏"" title=""全屏""></button>
  </div>
  <input type=""file"" id=""unity-file-input"" hidden>
  <div id=""unity-url-window""><iframe id=""unity-url-frame"" title=""网页内容""></iframe></div>
  <script>
    const buildUrl = ""Build"";
    const config = {
      dataUrl: buildUrl + ""/{{{ DATA_FILENAME }}}"",
      frameworkUrl: buildUrl + ""/{{{ FRAMEWORK_FILENAME }}}"",
      codeUrl: buildUrl + ""/{{{ CODE_FILENAME }}}"",
      streamingAssetsUrl: ""StreamingAssets"",
      companyName: ""{{{ COMPANY_NAME }}}"",
      productName: ""{{{ PRODUCT_NAME }}}"",
      productVersion: ""{{{ PRODUCT_VERSION }}}""
    };
    const container = document.querySelector(""#unity-container"");
    const canvas = document.querySelector(""#unity-canvas"");
    const loadingBar = document.querySelector(""#unity-loading-bar"");
    const progressBar = document.querySelector(""#unity-progress-bar-full"");
    const loadText = document.querySelector(""#unity-load-text"");
    const warningBanner = document.querySelector(""#unity-warning"");
    const browserHint = document.querySelector(""#unity-browser-hint"");
    const fullscreenButton = document.querySelector(""#unity-fullscreen-button"");
    const fileInput = document.querySelector(""#unity-file-input"");
    const urlWindow = document.querySelector(""#unity-url-window"");
    const urlFrame = document.querySelector(""#unity-url-frame"");
    let gameInstance = null;

    function unityShowBanner(message, type) {
      const item = document.createElement(""div"");
      item.textContent = message;
      item.style.background = type === ""error"" ? ""#f44336"" : ""#ffeb3b"";
      warningBanner.appendChild(item);
      if (type !== ""error"") {
        window.setTimeout(() => item.remove(), 5000);
      }
    }

    function isFullscreen() {
      return document.fullscreenElement === container
        || document.webkitFullscreenElement === container;
    }

    function updateFullscreenButton() {
      const fullscreen = isFullscreen();
      fullscreenButton.hidden = fullscreen;
      fullscreenButton.setAttribute(""aria-label"", fullscreen ? ""退出全屏"" : ""全屏"");
      fullscreenButton.title = fullscreen ? ""退出全屏"" : ""全屏"";
    }

    function enterFullscreen() {
      if (container.requestFullscreen) return container.requestFullscreen();
      if (container.webkitRequestFullscreen) return container.webkitRequestFullscreen();
      return Promise.resolve();
    }

    function exitFullscreen() {
      if (document.exitFullscreen) return document.exitFullscreen();
      if (document.webkitExitFullscreen) return document.webkitExitFullscreen();
      return Promise.resolve();
    }

    function toggleFullscreen() {
      return isFullscreen() ? exitFullscreen() : enterFullscreen();
    }

    function sendToUnity(method, value) {
      if (gameInstance && gameInstance.SendMessage) {
        gameInstance.SendMessage(""ExternalCall"", method, value || """");
      }
    }

    function GetCurrentURL() {
      const url = window.location.href;
      sendToUnity(""ReceiveFileData"", JSON.stringify({ fileType: ""txt"", receiveContent: url }));
      return url;
    }
    function ChineseInput() { canvas.style.width = ""100vw""; canvas.style.height = ""100vh""; }
    function OnBeforeunload(event) { sendToUnity(""OnBeforeunload""); }
    function OpenUrl_Window(url) { urlFrame.src = url; urlWindow.style.display = ""block""; }
    function CloseUrl_Window() { urlFrame.src = """"; urlWindow.style.display = ""none""; }
    function OpenURL(url) { window.open(url, ""_blank""); }
    function OpenURL1(url) { window.open(url, ""_blank"", ""scrollbars=yes,resizable=1""); }
    function AlertInfo(message) { window.alert(message); }
    function ConfirmInfo(message) { return window.confirm(message); }
    function PromptInfo(message) { return window.prompt(message); }
    function Exit() { window.open(""about:blank"", ""_self"").close(); }

    function SyncDB() {
      if (typeof FS !== ""undefined"" && FS.syncfs) {
        FS.syncfs(false, (error) => { if (error) console.warn(""syncfs error: "", error); });
      }
    }

    function SetCookie(name, value, days) {
      const expires = new Date(Date.now() + days * 86400000).toUTCString();
      document.cookie = name + ""="" + encodeURIComponent(value) + ""; expires="" + expires + ""; path=/"";
    }

    function GetCookie(name) {
      const prefix = name + ""="";
      const value = document.cookie.split(""; "").find(item => item.indexOf(prefix) === 0);
      const result = value ? decodeURIComponent(value.substring(prefix.length)) : """";
      sendToUnity(""ReceiveFileData"", JSON.stringify({ fileType: ""txt"", receiveContent: result }));
      return result;
    }

    function SelectFile() { fileInput.click(); }

    function Download(base64, filename) {
      const link = document.createElement(""a"");
      link.href = ""data:application/octet-stream;base64,"" + base64;
      link.download = filename;
      link.click();
    }

    function FileImport(file) {
      file = file || fileInput.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = () => sendToUnity(""ReceiveFileData"", JSON.stringify({
        fileType: file.name.substring(file.name.lastIndexOf(""."") + 1),
        receiveContent: reader.result.split("","")[1]
      }));
      reader.readAsDataURL(file);
    }

    fullscreenButton.addEventListener(""click"", (event) => { event.stopPropagation(); toggleFullscreen(); });
    document.addEventListener(""fullscreenchange"", updateFullscreenButton);
    document.addEventListener(""webkitfullscreenchange"", updateFullscreenButton);
    fileInput.addEventListener(""change"", () => FileImport(fileInput.files[0]));
    updateFullscreenButton();
    const script = document.createElement(""script"");
    script.src = buildUrl + ""/{{{ LOADER_FILENAME }}}"";
    script.onload = () => {
      createUnityInstance(canvas, { ...config, showBanner: unityShowBanner }, (progress) => {
        progressBar.style.width = (100 * progress) + ""%"";
        loadText.textContent = ""加载中... "" + Math.round(100 * progress) + ""%"";
      }).then((unityInstance) => {
        loadingBar.style.display = ""none"";
        if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
          browserHint.textContent = ""移动设备建议使用横屏浏览"";
        }
        else {
          browserHint.style.display = ""none"";
        }
        gameInstance = unityInstance;
      }).catch((message) => {
        unityShowBanner(message, ""error"");
      });
    };
    document.body.appendChild(script);
  </script>
</body>
</html>
";
        }
    }
}
