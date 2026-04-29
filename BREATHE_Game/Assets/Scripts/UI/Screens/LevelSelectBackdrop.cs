using Breathe.Data;
using UnityEngine;

namespace Breathe.UI
{
    /// <summary>
    /// Level-select card art: uses <see cref="MinigameDefinition.Thumbnail"/> when set, otherwise
    /// <c>Resources/LevelSelectBackdrops/&lt;minigameId&gt;</c> (e.g. sailboat.png).
    /// </summary>
    public static class LevelSelectBackdrop
    {
        const string ResourcePathPrefix = "LevelSelectBackdrops/";

        public static Sprite ResolveThumbnail(MinigameDefinition def)
        {
            if (def == null) return null;
            if (def.Thumbnail != null) return def.Thumbnail;
            if (string.IsNullOrEmpty(def.MinigameId)) return null;
            return Resources.Load<Sprite>(ResourcePathPrefix + def.MinigameId);
        }

        public static float SpriteAspect(Sprite s)
        {
            if (s == null) return 1f;
            Rect r = s.rect;
            return r.width / Mathf.Max(0.0001f, r.height);
        }
    }
}
