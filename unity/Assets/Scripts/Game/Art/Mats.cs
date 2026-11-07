// Material factory on the Pip/Unlit shader (particles, placeholder decals, UI-side meshes): alpha or
// additive blending, optional fog keyword, render queue; cached per texture / colour / options.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pip.Game.Art
{
    public enum Blend { Alpha, Additive }

    public static class Mats
    {
        static Shader unlit;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        /// <summary>The template referenced from the scene keeps the shader in player builds.</summary>
        public static void Init(Material unlitTemplate)
        {
            if (unlitTemplate) unlit = unlitTemplate.shader;
        }

        static Shader UnlitS => unlit ? unlit : unlit = Shader.Find("Pip/Unlit");

        public static Material Unlit(Texture2D tex, Color color, Blend blend = Blend.Alpha, bool fog = true, int queue = 3000, bool zwrite = false)
        {
            string key = "u" + (tex ? tex.name : "") + color + blend + fog + queue + zwrite;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(UnlitS) { name = "pip unlit " + (tex ? tex.name : "") };
            m.SetTexture("_BaseMap", tex ? tex : Tex.White);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(blend == Blend.Additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
            m.SetFloat("_Cull", (float)CullMode.Off);
            if (fog) m.EnableKeyword("_PIP_FOG");
            m.renderQueue = queue;
            cache[key] = m;
            return m;
        }

        /// <summary>Per-object unlit copy (for materials whose colour animates).</summary>
        public static Material UnlitInstance(Texture2D tex, Color color, Blend blend = Blend.Alpha, bool fog = true, int queue = 3000) =>
            new Material(Unlit(tex, color, blend, fog, queue));
    }
}
