using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// ExpansionDemo 运行时按需下载验证面板。
    /// 缺少磁盘缓存时由 YooAsset 在 LoadAssetAsync 内静默下载资源包，再实例化模型。
    /// </summary>
    public sealed class ExpansionDemoOnDemandProbe : MonoBehaviour
    {
        private const string ModelLocation = "ExpansionDemo/OnDemandModel";
        private const int PreviewLayer = 31;

        [SerializeField]
        [Tooltip("按需加载验证面板。")]
        private GameObject panel;

        [SerializeField]
        [Tooltip("缓存状态、下载大小和加载结果。")]
        private Text statusText;

        [SerializeField]
        [Tooltip("触发按需加载的按钮。")]
        private Button loadButton;

        [SerializeField]
        [Tooltip("按需加载按钮文字。")]
        private Text loadButtonText;

        [SerializeField]
        [Tooltip("模型预览图。")]
        private RawImage previewImage;

        [SerializeField]
        [Tooltip("仅渲染按需加载模型的预览相机。")]
        private Camera previewCamera;

        [SerializeField]
        [Tooltip("按需加载模型的实例父节点。")]
        private Transform previewRoot;

        private CancellationTokenSource loadCts;
        private RenderTexture previewTexture;
        private GameObject modelInstance;
        private bool assetLoaded;
        private bool isLoading;

        /// <summary>
        /// 初始化预览输出并绑定按钮。
        /// </summary>
        private void Start()
        {
            loadCts = new CancellationTokenSource();
            if (loadButton != null)
            {
                loadButton.onClick.AddListener(OnLoadButtonClicked);
            }

            CreatePreviewTexture();
            SetStatus("点击按钮验证运行时按需下载。\n首次无缓存时会静默下载资源包。");
        }

        /// <summary>
        /// 取消加载并释放运行时预览对象。
        /// </summary>
        private void OnDestroy()
        {
            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(OnLoadButtonClicked);
            }

            loadCts?.Cancel();
            loadCts?.Dispose();
            loadCts = null;

            if (modelInstance != null)
            {
                Destroy(modelInstance);
                modelInstance = null;
            }

            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }

            if (previewImage != null)
            {
                previewImage.texture = null;
            }

            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
                previewTexture = null;
            }
        }

        private void OnLoadButtonClicked()
        {
            if (!isLoading)
            {
                _ = LoadAndPreviewAsync(loadCts.Token);
            }
        }

        private async Task LoadAndPreviewAsync(CancellationToken ct)
        {
            isLoading = true;
            SetButtonState(false, "加载中...");

            try
            {
                if (GameEntry.Resource == null)
                {
                    throw new RFrameworkException(
                        "ExpansionDemo: ResourceComponent is unavailable.");
                }

                long downloadBytes = GameEntry.Resource.GetDownloadSize(ModelLocation);
                SetStatus(downloadBytes > 0L
                    ? $"本地无缓存，正在静默下载 {FormatBytes(downloadBytes)}..."
                    : "磁盘缓存命中，正在直接加载模型...");

                if (assetLoaded)
                {
                    DestroyModelInstance();
                    GameEntry.Resource.UnloadAsset<GameObject>(ModelLocation);
                    assetLoaded = false;
                }

                GameObject modelPrefab = await GameEntry.Resource.LoadAssetAsync<GameObject>(
                    ModelLocation,
                    0,
                    ct);
                ct.ThrowIfCancellationRequested();

                assetLoaded = true;
                modelInstance = Instantiate(modelPrefab, previewRoot, false);
                modelInstance.name = "OnDemandModel";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                SetLayerRecursively(modelInstance.transform, PreviewLayer);
                FrameModel();

                SetStatus(downloadBytes > 0L
                    ? $"按需下载并实例化成功\n下载量：{FormatBytes(downloadBytes)}"
                    : "缓存资源实例化成功\n本次无需网络下载");
                SetButtonState(true, "重新加载");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 框架重启或应用退出时正常取消。
            }
            catch (Exception exception)
            {
                SetStatus($"按需加载失败\n{exception.Message}");
                SetButtonState(true, "重试");
                if (RFrameworkLog.IsInitialized)
                {
                    Log.Error("[ExpansionDemo] On-demand model load failed: {0}", exception);
                }
            }
            finally
            {
                isLoading = false;
            }
        }

        private void CreatePreviewTexture()
        {
            if (previewCamera == null || previewImage == null)
            {
                return;
            }

            previewTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "ExpansionDemoOnDemandPreview"
            };
            previewTexture.Create();
            previewCamera.targetTexture = previewTexture;
            previewImage.texture = previewTexture;
        }

        private void FrameModel()
        {
            if (modelInstance == null || previewCamera == null)
            {
                return;
            }

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new RFrameworkException(
                    "ExpansionDemo: the on-demand model does not contain a Renderer.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            Vector3 direction = new Vector3(1f, 0.55f, -1f).normalized;
            previewCamera.transform.position = bounds.center + direction * (radius * 3f);
            previewCamera.transform.LookAt(bounds.center);
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = radius * 10f + 10f;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = radius * 1.15f;
        }

        private void DestroyModelInstance()
        {
            if (modelInstance == null)
            {
                return;
            }

            Destroy(modelInstance);
            modelInstance = null;
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        private void SetButtonState(bool interactable, string label)
        {
            if (loadButton != null)
            {
                loadButton.interactable = interactable;
            }

            if (loadButtonText != null)
            {
                loadButtonText.text = label;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024L * 1024L)
            {
                return $"{bytes / 1024f:F1} KB";
            }

            return $"{bytes / (1024f * 1024f):F1} MB";
        }
    }
}
