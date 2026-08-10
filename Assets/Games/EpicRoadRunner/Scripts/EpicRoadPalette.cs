using UnityEngine;

namespace RunnerPac.EpicRoadRunner
{
    // Picks a random colour palette each run so no two races look alike.
    //
    // Deliberately touches only sky, light and road - never the barrels, gates or
    // enemies. Those have to stay instantly readable, and recolouring them would
    // make a red gate stop reading as dangerous.
    //
    // Nothing here writes to an asset: the skybox is cloned at runtime and the
    // road is tinted through a MaterialPropertyBlock, so the project files are
    // untouched and play mode leaves no trace.
    public class EpicRoadPalette : MonoBehaviour
    {
        [System.Serializable]
        public class Palette
        {
            public string Name = "Palette";
            [Header("Sky")]
            [ColorUsage(false, true)] public Color SkyTint = Color.cyan;
            public Color GroundColor = new Color(0.25f, 0.22f, 0.20f);
            [Header("Light")]
            public Color SunColor = Color.white;
            public Color Ambient = new Color(0.75f, 0.75f, 0.75f);
            [Header("Road")]
            public Color RoadBlocks = Color.white;
            public Color RoadLines = Color.white;
        }

        [Tooltip("A random one of these is applied when the level starts.")]
        public Palette[] Palettes = new Palette[0];

        // Road tinting is OFF by default, and should stay that way.
        // A coloured road makes barrels, gates and enemies much harder to pick
        // out, and the lane blockers share these materials with the road surface,
        // so they cannot be recoloured independently. The sky carries the theme
        // instead. Fill these in only if you want to experiment.
        [Tooltip("Leave EMPTY to keep the road grey (recommended - coloured road hurts readability).")]
        public string RoadBlocksMaterial = "";

        [Tooltip("Leave EMPTY to keep the lane markings grey.")]
        public string RoadLinesMaterial = "";

        [Tooltip("Log which palette was chosen.")]
        public bool LogChoice = false;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Start()
        {
            if (Palettes == null || Palettes.Length == 0) return;
            Apply(Palettes[Random.Range(0, Palettes.Length)]);
        }

        public void Apply(Palette palette)
        {
            if (RenderSettings.skybox != null)
            {
                // Clone so the shared Default-Skybox asset is never modified.
                var sky = new Material(RenderSettings.skybox);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", palette.SkyTint);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", palette.GroundColor);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientLight = palette.Ambient;

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.color = palette.SunColor;

            Tint(RoadBlocksMaterial, palette.RoadBlocks);
            Tint(RoadLinesMaterial, palette.RoadLines);

            DynamicGI.UpdateEnvironment();

            if (LogChoice) Debug.Log($"[EpicRoadPalette] {palette.Name}");
        }

        void Tint(string materialName, Color color)
        {
            if (string.IsNullOrEmpty(materialName)) return;

            var block = new MaterialPropertyBlock();
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var shared = renderer.sharedMaterial;
                if (shared == null || !shared.name.StartsWith(materialName)) continue;

                renderer.GetPropertyBlock(block);
                // Set both, so this works whether the shader is URP (_BaseColor)
                // or built-in/Standard (_Color). Unknown ids are ignored.
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
