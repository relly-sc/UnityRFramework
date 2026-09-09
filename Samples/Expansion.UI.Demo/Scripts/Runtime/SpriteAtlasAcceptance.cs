using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using UnityRFramework.Runtime;

public sealed class SpriteAtlasAcceptance : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private string spriteName = "01_warrior_portrait";

    private const string AtlasLocation = "UI/Common/New Sprite Atlas";
    private SpriteAtlas atlas;

    private async void Start()
    {
        try
        {
            await GameEntry.Resource.InitializeAsync();
            atlas = await GameEntry.Resource.LoadAssetAsync<SpriteAtlas>(
                AtlasLocation);
            Debug.Log("[SpriteAtlas 加载] " + atlas.spriteCount);

            Sprite sprite = atlas.GetSprite(spriteName);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"SpriteAtlas 中不存在 {spriteName}");
            }

            targetImage.sprite = sprite;
            Debug.Log("[SpriteAtlas 验收] Resources 加载成功。");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnDestroy()
    {
        if (atlas == null)
        {
            return;
        }

        targetImage.sprite = null;
        GameEntry.Resource.UnloadAsset<SpriteAtlas>(AtlasLocation);
        atlas = null;
    }
}