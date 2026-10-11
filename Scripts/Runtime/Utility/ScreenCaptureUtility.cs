using System;
using System.IO;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>提供相机截图和 PNG 写入，调用者负责决定文件位置。</summary>
    public static class ScreenCaptureUtility
    {
        public static Texture2D CaptureCamera(Camera camera, int width, int height)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            RenderTexture previous = camera.targetTexture;
            RenderTexture buffer = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            camera.targetTexture = buffer;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = buffer;
                camera.Render();
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply();
            }
            catch
            {
                UnityEngine.Object.Destroy(result);
                throw;
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                buffer.Release();
                UnityEngine.Object.Destroy(buffer);
            }

            return result;
        }

        public static void SavePng(Texture2D texture, string path)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty.", nameof(path));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
    }
}
