// Pip waving above the next level on the map (PS.mapPip3d in the web): the s02 idle-wave sprite in a
// UI Image, bobbing and hopping.
using UnityEngine;
using UnityEngine.UI;

namespace Pip.Game.Ui
{
    public static class MapPip
    {
        /// <summary>A child Image of the 60×74 map window showing the wave pose, bobbing.</summary>
        public static MapPip2D Create2D(RectTransform window)
        {
            var go = new GameObject("pip wave", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(window, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-4, 0);
            rt.offsetMax = new Vector2(4, 4);
            var img = go.AddComponent<Image>();
            img.sprite = Art2D.SpriteTable.Get("pip.wave").S;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return go.AddComponent<MapPip2D>();
        }
    }

    /// <summary>The map's 2D Pip: a gentle bob with a little hop and sway.</summary>
    public sealed class MapPip2D : MonoBehaviour
    {
        void LateUpdate()
        {
            float t = Time.unscaledTime;
            var rt = (RectTransform)transform;
            rt.anchoredPosition = new Vector2(0, Mathf.Max(0, Mathf.Sin(t * 3)) * 5);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.8f) * 4);
            float b = Mathf.Sin(t * 6) * 0.02f;
            rt.localScale = new Vector3(1 - b, 1 + b, 1);
        }
    }
}
