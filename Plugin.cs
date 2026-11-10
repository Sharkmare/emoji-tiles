using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EmojiTiles
{
    [BepInPlugin("local.emojitiles", "EmojiTiles", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private const string ImageFileName = "tile.png";
        private const float ScanIntervalSeconds = 2f;
        private const float PivotCenter = 0.5f;

        private Texture2D emojiTexture;
        private readonly Dictionary<int, Sprite> spriteBySizeKey = new Dictionary<int, Sprite>();
        private readonly HashSet<int> replacedTileIds = new HashSet<int>();
        private readonly HashSet<string> loggedUnhandledTypes = new HashSet<string>();
        private float secondsUntilScan;

        private void Awake()
        {
            string imagePath = Path.Combine(Path.GetDirectoryName(Info.Location), ImageFileName);
            if (!File.Exists(imagePath))
            {
                Logger.LogError("missing " + imagePath);
                enabled = false;
                return;
            }
            emojiTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!emojiTexture.LoadImage(File.ReadAllBytes(imagePath)))
            {
                Logger.LogError("could not decode " + imagePath);
                enabled = false;
                return;
            }
            emojiTexture.filterMode = FilterMode.Bilinear;
            Logger.LogInfo("loaded " + ImageFileName + " " + emojiTexture.width + "x" + emojiTexture.height);
        }

        private void Update()
        {
            secondsUntilScan -= Time.unscaledDeltaTime;
            if (secondsUntilScan > 0f)
            {
                return;
            }
            secondsUntilScan = ScanIntervalSeconds;
            int replaced = ReplaceAllTileSprites();
            if (replaced > 0)
            {
                foreach (Tilemap tilemap in Object.FindObjectsOfType<Tilemap>())
                {
                    tilemap.RefreshAllTiles();
                }
                Logger.LogInfo("replaced sprites on " + replaced + " new tile assets");
            }
        }

        private int ReplaceAllTileSprites()
        {
            int replaced = 0;
            foreach (TileBase tileBase in Resources.FindObjectsOfTypeAll<TileBase>())
            {
                if (!replacedTileIds.Add(tileBase.GetInstanceID()))
                {
                    continue;
                }
                if (ReplaceOnTile(tileBase))
                {
                    replaced++;
                }
            }
            return replaced;
        }

        private bool ReplaceOnTile(TileBase tileBase)
        {
            Tile tile = tileBase as Tile;
            if (tile != null)
            {
                tile.sprite = EmojiSpriteLike(tile.sprite);
                return true;
            }
            RuleTile ruleTile = tileBase as RuleTile;
            if (ruleTile != null)
            {
                ruleTile.m_DefaultSprite = EmojiSpriteLike(ruleTile.m_DefaultSprite);
                foreach (RuleTile.TilingRule rule in ruleTile.m_TilingRules)
                {
                    for (int index = 0; index < rule.m_Sprites.Length; index++)
                    {
                        rule.m_Sprites[index] = EmojiSpriteLike(rule.m_Sprites[index]);
                    }
                }
                return true;
            }
            AnimatedTile animatedTile = tileBase as AnimatedTile;
            if (animatedTile != null)
            {
                for (int index = 0; index < animatedTile.m_AnimatedSprites.Length; index++)
                {
                    animatedTile.m_AnimatedSprites[index] = EmojiSpriteLike(animatedTile.m_AnimatedSprites[index]);
                }
                return true;
            }
            string typeName = tileBase.GetType().FullName;
            if (loggedUnhandledTypes.Add(typeName))
            {
                Logger.LogWarning("tile type not handled: " + typeName);
            }
            return false;
        }

        private Sprite EmojiSpriteLike(Sprite original)
        {
            float worldWidth = original != null ? original.rect.width / original.pixelsPerUnit : 1f;
            int sizeKey = Mathf.RoundToInt(worldWidth * 1000f);
            if (!spriteBySizeKey.TryGetValue(sizeKey, out Sprite sprite))
            {
                float pixelsPerUnit = emojiTexture.width / worldWidth;
                sprite = Sprite.Create(emojiTexture, new Rect(0, 0, emojiTexture.width, emojiTexture.height), new Vector2(PivotCenter, PivotCenter), pixelsPerUnit);
                spriteBySizeKey[sizeKey] = sprite;
            }
            return sprite;
        }
    }
}
