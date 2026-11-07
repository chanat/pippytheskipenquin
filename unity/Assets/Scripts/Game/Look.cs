// Shared colour helper for the UI and HUD.
using UnityEngine;

namespace Pip.Game
{
    public static class Look
    {
        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
