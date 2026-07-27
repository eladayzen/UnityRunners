#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Solo.MOST_IN_ONE
{
    public enum BlockCornerMode
    {
        Strips,
        DiagonalCorners
    }

    [HideScriptField]
    [CreateAssetMenu(
        menuName = "MOST/Block Sprite Generator Preset",
        fileName = "BlockSpriteGeneratorPreset"
    )]
    public class BlockSpriteGeneratorPreset : ScriptableObject
    {
        [BigHeader("Block Properties", 16)]
        [HelpBox("Controls the single block sprite: colors, texture size, rim, rounded corners, and corner partition mode.", HelpBoxKind.Info)]
        [Tooltip("Main center color of the generated block.")]
        public Color blockFace = new Color(0.80f, 0.80f, 0.80f, 1f);

        [Tooltip("Top rim color.")]
        public Color blockTop = new Color(1.00f, 1.00f, 1.00f, 1f);

        [Tooltip("Bottom rim color.")]
        public Color blockBottom = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Tooltip("Left rim color.")]
        public Color blockLeft = new Color(0.90f, 0.90f, 0.90f, 1f);

        [Tooltip("Right rim color.")]
        public Color blockRight = new Color(0.65f, 0.65f, 0.65f, 1f);

        [Line]
        [Tooltip("Generated block texture width and height in pixels.")]
        public int blockSize = 256;

        [Tooltip("Rim thickness in pixels. It is clamped to half of the block size.")]
        public int rimThickness = 32;

        [Tooltip("Rounded corner radius in pixels.")]
        public int blockCornerRadius = 24;

        [Tooltip("Alpha-only anti-aliasing width for rounded corners.")]
        public int blockCornerAA = 2;

        [Tooltip("How rim colors are assigned around block corners.")]
        public BlockCornerMode blockCornerMode = BlockCornerMode.DiagonalCorners;

        [BigHeader("Background Properties", 16)]
        [HelpBox("Controls the generated grid background. The final texture size is Columns × Rows × Cell Pixel Size plus outline thickness.", HelpBoxKind.Info)]
        [Tooltip("Grid columns.")]
        public int bgColumns = 8;

        [Tooltip("Grid rows.")]
        public int bgRows = 8;

        [Tooltip("Pixel size of each background grid cell.")]
        public int bgCellPixelSize = 64;

        [Line]
        [Tooltip("Fill color between grid lines.")]
        public Color bgFill = new Color(0.12f, 0.12f, 0.12f, 1f);

        [Tooltip("Horizontal grid line color.")]
        public Color bgLineH = new Color(1f, 1f, 1f, 0.12f);

        [Tooltip("Vertical grid line color.")]
        public Color bgLineV = new Color(1f, 1f, 1f, 0.12f);

        [Tooltip("Outer outline and grid intersection color.")]
        public Color bgOutline = new Color(1f, 1f, 1f, 0.20f);

        [Line]
        [Tooltip("Grid line thickness in pixels.")]
        public int bgGridLineThickness = 2;

        [Tooltip("Outer outline thickness in pixels.")]
        public int bgOutlineThickness = 4;

        [Tooltip("Rounded corner radius in pixels.")]
        public int bgCornerRadius = 32;

        [Tooltip("Alpha-only anti-aliasing width for rounded background corners.")]
        public int bgCornerAA = 2;

        [BigHeader("Block Export Settings", 16)]
        [HelpBox("Block output settings are separated from background output settings, so both generated assets can use different import values.", HelpBoxKind.Info)]
        [Tooltip("Optional Assets/ folder. Empty or invalid folders fall back to Assets/.")]
        public DefaultAsset blockOutputFolder;

        [Tooltip("PNG file name for the block. Existing .png extensions are removed automatically.")]
        public string blockFileName = "Block";

        [Tooltip("Sprite pixels per unit used after importing the PNG.")]
        public float blockPixelsPerUnit = 100f;

        [Tooltip("Texture filter mode used after importing the block PNG.")]
        public FilterMode blockFilterMode = FilterMode.Bilinear;

        [Tooltip("Texture compression used after importing the block PNG.")]
        public TextureImporterCompression blockCompression = TextureImporterCompression.Uncompressed;

        [Tooltip("Sprite pivot, normalized from 0 to 1.")]
        public Vector2 blockPivot = new Vector2(0.5f, 0.5f);

        [Tooltip("Sprite border as Left, Bottom, Right, Top pixels. Useful for sliced UI sprites.")]
        public Vector4 blockSpriteBorder = Vector4.zero;

        [BigHeader("Background Export Settings", 16)]
        [HelpBox("Background output/import settings are independent from the block asset.", HelpBoxKind.Info)]
        [Tooltip("Optional Assets/ folder. Empty or invalid folders fall back to Assets/.")]
        public DefaultAsset backgroundOutputFolder;

        [Tooltip("PNG file name for the background. Existing .png extensions are removed automatically.")]
        public string backgroundFileName = "BackgroundGrid";

        [Tooltip("Sprite pixels per unit used after importing the PNG.")]
        public float backgroundPixelsPerUnit = 100f;

        [Tooltip("Texture filter mode used after importing the background PNG.")]
        public FilterMode backgroundFilterMode = FilterMode.Bilinear;

        [Tooltip("Texture compression used after importing the background PNG.")]
        public TextureImporterCompression backgroundCompression = TextureImporterCompression.Uncompressed;

        [Tooltip("Sprite pivot, normalized from 0 to 1.")]
        public Vector2 backgroundPivot = new Vector2(0.5f, 0.5f);

        [Tooltip("Sprite border as Left, Bottom, Right, Top pixels. Useful for sliced UI sprites.")]
        public Vector4 backgroundSpriteBorder = Vector4.zero;

        [BigHeader("Preview Settings", 16)]
        [Tooltip("Automatically rebuild the preview when generator settings change.")]
        public bool autoPreview = true;
    }

    public class BlockSpriteGeneratorWindow : EditorWindow
    {
        // -----------------------------
        // Menu
        // -----------------------------
        [MenuItem("Tools/MOST/Block Sprite Generator")]
        public static void Open()
        {
            var w = GetWindow<BlockSpriteGeneratorWindow>();
            w.titleContent = new GUIContent("Block Sprite Generator");
            w.minSize = new Vector2(500, 640);
            w.Show();
        }

        private const int PreviewMaxDim = 1024;
        private const int MaxBlockSize = 4096;
        private const int MaxGridCells = 512;
        private const int MaxCellPixelSize = 2048;
        private const long LargeTextureWarningBytes = 256L * 1024L * 1024L;

        // -----------------------------
        // PRESETS
        // -----------------------------
        [SerializeField]
        [BigHeader("Preset Assets", 17)]
        [HelpBox("Load, save, and share generator settings with ScriptableObject preset assets.", HelpBoxKind.Info)]
        [InnerHint("optional")]
        [Tooltip("Optional ScriptableObject preset. Use the buttons below to load or save settings.")]
        private BlockSpriteGeneratorPreset activePreset;

        [SerializeField]
        [InspectorButton(nameof(ButtonLoadActivePreset), Label = "Load Preset", Tooltip = "Apply the active preset to the generator window.", Height = 26f)]
        private InspectorButton loadPresetButton;

        [SerializeField]
        [InspectorButton(nameof(ButtonSaveToActivePreset), Label = "Save To Preset", Tooltip = "Write the current generator settings into the active preset asset.", Confirm = "Overwrite the active preset with the current generator settings?", Height = 26f)]
        private InspectorButton saveToPresetButton;

        [SerializeField]
        [InspectorButton(nameof(ButtonSaveAsNewPreset), Label = "Save As New Preset", Tooltip = "Create a new preset asset from the current generator settings.", Height = 26f)]
        private InspectorButton saveAsNewPresetButton;

        // -----------------------------
        // BLOCK SETTINGS (no gradients)
        // -----------------------------
        [SerializeField]
        [BigHeader("Block Properties", 17)]
        [HelpBox("The block sprite is generated as one square PNG. Shape values are clamped before preview/export.", HelpBoxKind.Info)]
        [Tooltip("Main center color of the generated block.")]
        private Color blockFace = new Color(0.80f, 0.80f, 0.80f, 1f);

        [SerializeField]
        [Tooltip("Top rim color.")]
        private Color blockTop = new Color(1.00f, 1.00f, 1.00f, 1f);

        [SerializeField]
        [Tooltip("Bottom rim color.")]
        private Color blockBottom = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        [Tooltip("Left rim color.")]
        private Color blockLeft = new Color(0.90f, 0.90f, 0.90f, 1f);

        [SerializeField]
        [Tooltip("Right rim color.")]
        private Color blockRight = new Color(0.65f, 0.65f, 0.65f, 1f);

        [SerializeField]
        [Line]
        [Tooltip("Generated block texture width and height in pixels. Clamped to 8–4096.")]
        private int blockSize = 256;

        [SerializeField]
        [Tooltip("Rim thickness in pixels. Clamped to half of the block size.")]
        private int rimThickness = 32;

        [SerializeField]
        [Tooltip("Rounded corner radius in pixels. Clamped to half of the block size.")]
        private int blockCornerRadius = 24;

        [SerializeField]
        [Tooltip("Alpha-only anti-aliasing width for rounded corners. Clamped to 0–16.")]
        private int blockCornerAA = 2;

        [SerializeField]
        [Tooltip("How rim colors are assigned around block corners.")]
        private BlockCornerMode blockCornerMode = BlockCornerMode.DiagonalCorners;

        // -----------------------------
        // BACKGROUND SETTINGS
        // -----------------------------
        [SerializeField]
        [BigHeader("Background Properties", 17)]
        [HelpBox("The background grid has independent dimensions and cell size, separate from the block sprite.", HelpBoxKind.Info)]
        [Tooltip("Grid columns. Clamped to 1–512.")]
        private int bgColumns = 8;

        [SerializeField]
        [Tooltip("Grid rows. Clamped to 1–512.")]
        private int bgRows = 8;

        [SerializeField]
        [Tooltip("Pixel size of each background grid cell. Clamped to 4–2048.")]
        private int bgCellPixelSize = 64;

        [SerializeField]
        [Line]
        [Tooltip("Fill color between grid lines.")]
        private Color bgFill = new Color(0.12f, 0.12f, 0.12f, 1f);

        [SerializeField]
        [Tooltip("Horizontal grid line color.")]
        private Color bgLineH = new Color(1f, 1f, 1f, 0.12f);

        [SerializeField]
        [Tooltip("Vertical grid line color.")]
        private Color bgLineV = new Color(1f, 1f, 1f, 0.12f);

        [SerializeField]
        [Tooltip("Outer outline and grid intersection color.")]
        private Color bgOutline = new Color(1f, 1f, 1f, 0.20f);

        [SerializeField]
        [Line]
        [Tooltip("Grid line thickness in pixels. Clamped to 1–64.")]
        private int bgGridLineThickness = 2;

        [SerializeField]
        [Tooltip("Outer outline thickness in pixels. Clamped to 0–256.")]
        private int bgOutlineThickness = 4;

        [SerializeField]
        [Tooltip("Rounded corner radius in pixels. Clamped to the texture dimensions during generation.")]
        private int bgCornerRadius = 32;

        [SerializeField]
        [Tooltip("Alpha-only anti-aliasing width for rounded background corners. Clamped to 0–16.")]
        private int bgCornerAA = 2;

        // -----------------------------
        // OUTPUT SETTINGS (separated per generated asset)
        // -----------------------------
        [SerializeField]
        [BigHeader("Block Export Settings", 17)]
        [HelpBox("These import/output settings only affect the generated block sprite.", HelpBoxKind.Info)]
        [Tooltip("Optional Assets/ folder. Empty or invalid folders fall back to Assets/.")]
        private DefaultAsset blockOutputFolder;

        [SerializeField]
        [Tooltip("PNG file name for the block. Existing .png extensions are removed automatically.")]
        private string blockFileName = "Block";

        [SerializeField]
        [Tooltip("Sprite pixels per unit used after importing the block PNG.")]
        private float blockPixelsPerUnit = 100f;

        [SerializeField]
        [Tooltip("Texture filter mode used after importing the block PNG.")]
        private FilterMode blockFilterMode = FilterMode.Bilinear;

        [SerializeField]
        [Tooltip("Texture compression used after importing the block PNG.")]
        private TextureImporterCompression blockCompression = TextureImporterCompression.Uncompressed;

        [SerializeField]
        [Tooltip("Sprite pivot, normalized from 0 to 1.")]
        private Vector2 blockPivot = new Vector2(0.5f, 0.5f);

        [SerializeField]
        [Tooltip("Sprite border as Left, Bottom, Right, Top pixels. Useful for sliced UI sprites.")]
        private Vector4 blockSpriteBorder = Vector4.zero;

        [SerializeField]
        [BigHeader("Background Export Settings", 17)]
        [HelpBox("These import/output settings only affect the generated background sprite.", HelpBoxKind.Info)]
        [Tooltip("Optional Assets/ folder. Empty or invalid folders fall back to Assets/.")]
        private DefaultAsset backgroundOutputFolder;

        [SerializeField]
        [Tooltip("PNG file name for the background. Existing .png extensions are removed automatically.")]
        private string backgroundFileName = "BackgroundGrid";

        [SerializeField]
        [Tooltip("Sprite pixels per unit used after importing the background PNG.")]
        private float backgroundPixelsPerUnit = 100f;

        [SerializeField]
        [Tooltip("Texture filter mode used after importing the background PNG.")]
        private FilterMode backgroundFilterMode = FilterMode.Bilinear;

        [SerializeField]
        [Tooltip("Texture compression used after importing the background PNG.")]
        private TextureImporterCompression backgroundCompression = TextureImporterCompression.Uncompressed;

        [SerializeField]
        [Tooltip("Sprite pivot, normalized from 0 to 1.")]
        private Vector2 backgroundPivot = new Vector2(0.5f, 0.5f);

        [SerializeField]
        [Tooltip("Sprite border as Left, Bottom, Right, Top pixels. Useful for sliced UI sprites.")]
        private Vector4 backgroundSpriteBorder = Vector4.zero;

        // -----------------------------
        // PREVIEW + ACTIONS
        // -----------------------------
        [SerializeField]
        [BigHeader("Preview Settings", 17)]
        [HelpBox("Auto Preview rebuilds the preview after settings change. Use Refresh Preview for a manual rebuild.", HelpBoxKind.Info)]
        [Tooltip("Automatically rebuild the preview when generator settings change.")]
        private bool autoPreview = true;

        [SerializeField]
        [BigHeader("Actions", 17)]
        [HelpBox("Export buttons still perform overwrite checks. Generate Both validates both outputs before writing files.", HelpBoxKind.Info)]
        [InspectorButton(nameof(ButtonRefreshPreview), Label = "Refresh Preview", Tooltip = "Rebuild both preview textures.", Height = 28f)]
        private InspectorButton refreshPreviewButton;

        [SerializeField]
        [InspectorButton(nameof(ButtonApplyPixelArtDefaults), Label = "Pixel Art Defaults", Tooltip = "Set point filtering, uncompressed import, zero AA, and centered pivots.", Height = 28f)]
        private InspectorButton pixelArtDefaultsButton;

        [SerializeField]
        [InspectorButton(nameof(ButtonResetDefaults), Label = "Reset Defaults", Tooltip = "Restore the built-in generator defaults.", Confirm = "Reset all generator values to their built-in defaults?", Height = 28f)]
        private InspectorButton resetDefaultsButton;

        [SerializeField]
        [Line]
        [InspectorButton(nameof(GenerateBlock), Label = "Generate Block PNG", Tooltip = "Generate and import the block PNG as a Sprite.", Height = 30f)]
        private InspectorButton generateBlockButton;

        [SerializeField]
        [InspectorButton(nameof(GenerateBackground), Label = "Generate Background PNG", Tooltip = "Generate and import the background PNG as a Sprite.", Height = 30f)]
        private InspectorButton generateBackgroundButton;

        [SerializeField]
        [InspectorButton(nameof(GenerateBoth), Label = "Generate Both", Tooltip = "Validate, generate, and import both PNGs.", Height = 30f)]
        private InspectorButton generateBothButton;

        [SerializeField]
        [BigHeader("Generated Assets", 17)]
        [ReadOnly]
        [Tooltip("Last generated block Sprite.")]
        private Sprite lastBlockSprite;

        [SerializeField]
        [ReadOnly]
        [Tooltip("Last generated background Sprite.")]
        private Sprite lastBackgroundSprite;

        private Vector2 scroll;
        private Texture2D blockPreviewTex;
        private Texture2D bgPreviewTex;
        private SerializedObject serializedWindow;

        private struct ExportPlan
        {
            public string label;
            public string assetPath;
            public string fullPath;

            public ExportPlan(string label, string assetPath, string fullPath)
            {
                this.label = label;
                this.assetPath = assetPath;
                this.fullPath = fullPath;
            }
        }

        private void OnEnable()
        {
            LoadPrefs();
            ClampAllSettings();
            RebuildPreviews();
        }

        private void OnDisable()
        {
            SavePrefs();
            CleanupPreviewTextures();
            serializedWindow = null;
        }

        private void OnGUI()
        {
            EnsureSerializedWindow();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawWindowBanner();
            serializedWindow.Update();

            bool presetChanged = DrawSerializedProperties(nameof(activePreset));
            DrawSerializedProperties(nameof(loadPresetButton), nameof(saveToPresetButton), nameof(saveAsNewPresetButton));

            bool blockChanged = DrawSerializedProperties(
                nameof(blockFace), nameof(blockTop), nameof(blockBottom), nameof(blockLeft), nameof(blockRight),
                nameof(blockSize), nameof(rimThickness), nameof(blockCornerRadius), nameof(blockCornerAA), nameof(blockCornerMode)
            );

            bool backgroundChanged = DrawSerializedProperties(
                nameof(bgColumns), nameof(bgRows), nameof(bgCellPixelSize),
                nameof(bgFill), nameof(bgLineH), nameof(bgLineV), nameof(bgOutline),
                nameof(bgGridLineThickness), nameof(bgOutlineThickness), nameof(bgCornerRadius), nameof(bgCornerAA)
            );

            bool blockExportChanged = DrawSerializedProperties(
                nameof(blockOutputFolder), nameof(blockFileName), nameof(blockPixelsPerUnit), nameof(blockFilterMode),
                nameof(blockCompression), nameof(blockPivot), nameof(blockSpriteBorder)
            );

            bool backgroundExportChanged = DrawSerializedProperties(
                nameof(backgroundOutputFolder), nameof(backgroundFileName), nameof(backgroundPixelsPerUnit), nameof(backgroundFilterMode),
                nameof(backgroundCompression), nameof(backgroundPivot), nameof(backgroundSpriteBorder)
            );

            bool previewChanged = DrawSerializedProperties(nameof(autoPreview));

            DrawSerializedProperties(
                nameof(refreshPreviewButton), nameof(pixelArtDefaultsButton), nameof(resetDefaultsButton),
                nameof(generateBlockButton), nameof(generateBackgroundButton), nameof(generateBothButton)
            );

            DrawSerializedProperties(nameof(lastBlockSprite), nameof(lastBackgroundSprite));

            bool applied = serializedWindow.ApplyModifiedProperties();
            bool anyFieldChanged = applied || presetChanged || blockChanged || backgroundChanged || blockExportChanged || backgroundExportChanged || previewChanged;

            if (anyFieldChanged)
            {
                ClampAllSettings();

                UpdatePreviewAfterChanges(
                    blockChanged || previewChanged,
                    backgroundChanged || previewChanged,
                    blockExportChanged,
                    backgroundExportChanged
                );

                SavePrefs();
                Repaint();
            }

            DrawStatusCards();
            DrawPreviewArea();

            EditorGUILayout.EndScrollView();
        }

        // -------------------------------------------------------
        // Attribute-driven inspector drawing
        // -------------------------------------------------------
        private void EnsureSerializedWindow()
        {
            if (serializedWindow == null || serializedWindow.targetObject != this)
                serializedWindow = new SerializedObject(this);
        }

        private bool DrawSerializedProperties(params string[] propertyNames)
        {
            bool changed = false;

            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = serializedWindow.FindProperty(propertyNames[i]);
                if (property == null)
                {
                    EditorGUILayout.HelpBox($"Missing serialized property: {propertyNames[i]}", MessageType.Warning);
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            if (EditorGUI.EndChangeCheck())
                changed = true;

            return changed;
        }

        private void DrawWindowBanner()
        {
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("MOST — Block Sprite Generator", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Attribute-styled generator window with preset assets, separated export settings, and safe overwrite checks.", EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(8);
        }

        private void DrawStatusCards()
        {
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Output Status", EditorStyles.boldLabel);

                var (blockW, blockH) = GetBlockDimensions();
                var (bgW, bgH) = GetBackgroundDimensions(fullSize: true);

                DrawTextureSizeHelpBox("Block texture size", blockW, blockH);
                DrawTextureSizeHelpBox($"Background texture size ({bgColumns} × {bgRows})", bgW, bgH);

                string blockPath = BuildAssetPath(blockOutputFolder, blockFileName);
                string backgroundPath = BuildAssetPath(backgroundOutputFolder, backgroundFileName);

                MessageType pathMessageType = PathsEqual(blockPath, backgroundPath) ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(
                    $"Block output: {blockPath}\nBackground output: {backgroundPath}",
                    pathMessageType
                );
            }
        }

        private void DrawTextureSizeHelpBox(string label, int width, int height)
        {
            long memoryBytes = EstimateTextureMemoryBytes(width, height);
            int maxTextureSize = Mathf.Max(1, SystemInfo.maxTextureSize);

            MessageType messageType = MessageType.Info;
            if (width > maxTextureSize || height > maxTextureSize || (long)width * height > int.MaxValue)
                messageType = MessageType.Error;
            else if (memoryBytes > LargeTextureWarningBytes || width > 4096 || height > 4096)
                messageType = MessageType.Warning;

            EditorGUILayout.HelpBox(
                $"{label}: {width} × {height} px\n" +
                $"Estimated raw RGBA32 memory: {FormatBytes(memoryBytes)}\n" +
                $"Current system max texture size: {maxTextureSize}px",
                messageType
            );
        }

        // -------------------------------------------------------
        // Inspector button callbacks
        // -------------------------------------------------------
        private void ButtonLoadActivePreset()
        {
            if (activePreset == null)
            {
                ShowNotification(new GUIContent("Assign a preset first"));
                return;
            }

            Undo.RecordObject(this, "Load Block Sprite Generator Preset");
            ApplyFromPreset(activePreset);
            SavePrefs();
            RebuildPreviews();
            ShowNotification(new GUIContent("Preset loaded"));
            Repaint();
        }

        private void ButtonSaveToActivePreset()
        {
            if (activePreset == null)
            {
                ShowNotification(new GUIContent("Assign a preset first"));
                return;
            }

            Undo.RecordObject(activePreset, "Save Block Sprite Generator Preset");
            CopyToPreset(activePreset);
            EditorUtility.SetDirty(activePreset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SavePrefs();
            ShowNotification(new GUIContent("Preset saved"));
        }

        private UnityEngine.Object ButtonSaveAsNewPreset()
        {
            BlockSpriteGeneratorPreset created = SavePresetAsNewAsset();
            if (created == null)
                return null;

            activePreset = created;
            SavePrefs();
            ShowNotification(new GUIContent("New preset created"));
            Repaint();
            return created;
        }

        private void ButtonRefreshPreview()
        {
            RebuildPreviews();
            ShowNotification(new GUIContent("Preview refreshed"));
            Repaint();
        }

        private void ButtonApplyPixelArtDefaults()
        {
            Undo.RecordObject(this, "Apply Pixel Art Defaults");
            ApplyPixelArtDefaults();
            SavePrefs();
            RebuildPreviews();
            ShowNotification(new GUIContent("Pixel art defaults applied"));
            Repaint();
        }

        private void ButtonResetDefaults()
        {
            Undo.RecordObject(this, "Reset Block Sprite Generator Defaults");
            ResetToDefaults();
            SavePrefs();
            RebuildPreviews();
            ShowNotification(new GUIContent("Defaults restored"));
            Repaint();
        }

        // -------------------------------------------------------
        // Preview drawing
        // -------------------------------------------------------
        private void DrawPreviewArea()
        {
            EditorGUILayout.Space(12);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                DrawPreviewRow();
            }
        }

        private void DrawPreviewRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
                {
                    EditorGUILayout.LabelField("Block", EditorStyles.miniBoldLabel);
                    Rect r = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(false));
                    if (blockPreviewTex != null)
                        GUI.DrawTexture(r, blockPreviewTex, ScaleMode.ScaleToFit, true);
                    else
                        EditorGUI.HelpBox(r, "No block preview", MessageType.Info);
                }

                GUILayout.Space(10);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Background", EditorStyles.miniBoldLabel);
                    Rect r = GUILayoutUtility.GetRect(1, 220, GUILayout.ExpandWidth(true));
                    r.height = 220;
                    if (bgPreviewTex != null)
                        GUI.DrawTexture(r, bgPreviewTex, ScaleMode.ScaleToFit, true);
                    else
                        EditorGUI.HelpBox(r, "No background preview", MessageType.Info);
                }
            }
        }

        private void UpdatePreviewAfterChanges(
            bool blockTextureChanged,
            bool backgroundTextureChanged,
            bool blockExportChanged,
            bool backgroundExportChanged
        )
        {
            bool blockMissing = blockPreviewTex == null;
            bool backgroundMissing = bgPreviewTex == null;

            if (autoPreview || blockMissing)
            {
                if (blockTextureChanged || blockMissing)
                    RebuildBlockPreview();
            }

            if (autoPreview || backgroundMissing)
            {
                if (backgroundTextureChanged || backgroundMissing)
                    RebuildBackgroundPreview();
            }

            if (blockPreviewTex != null && blockExportChanged)
                blockPreviewTex.filterMode = blockFilterMode;

            if (bgPreviewTex != null && backgroundExportChanged)
                bgPreviewTex.filterMode = backgroundFilterMode;
        }

        private void CleanupPreviewTextures()
        {
            CleanupBlockPreviewTexture();
            CleanupBackgroundPreviewTexture();
        }

        private void CleanupBlockPreviewTexture()
        {
            if (blockPreviewTex != null)
                DestroyImmediate(blockPreviewTex);
            blockPreviewTex = null;
        }

        private void CleanupBackgroundPreviewTexture()
        {
            if (bgPreviewTex != null)
                DestroyImmediate(bgPreviewTex);
            bgPreviewTex = null;
        }

        private void RebuildPreviews()
        {
            RebuildBlockPreview();
            RebuildBackgroundPreview();
        }

        private void RebuildBlockPreview()
        {
            CleanupBlockPreviewTexture();

            blockPreviewTex = BuildBlockTexture();
            if (blockPreviewTex != null)
                blockPreviewTex.hideFlags = HideFlags.HideAndDontSave;
        }

        private void RebuildBackgroundPreview()
        {
            CleanupBackgroundPreviewTexture();

            bgPreviewTex = BuildBackgroundTexture(preview: true);
            if (bgPreviewTex != null)
                bgPreviewTex.hideFlags = HideFlags.HideAndDontSave;
        }

        // -------------------------------------------------------
        // Generation (PNG + import as Sprite)
        // -------------------------------------------------------
        private void GenerateBlock()
        {
            ClampAllSettings();

            var (w, h) = GetBlockDimensions();
            if (!ValidateTextureSize(w, h, "Block"))
                return;

            if (!TryPrepareExportPlan(blockOutputFolder, blockFileName, "Block", out var plan))
            {
                ShowNotification(new GUIContent("Block generation cancelled"));
                return;
            }

            var tex = BuildBlockTexture();
            var sprite = SaveTextureAsSprite(
                tex,
                plan,
                blockPixelsPerUnit,
                blockFilterMode,
                blockCompression,
                blockPivot,
                blockSpriteBorder
            );

            lastBlockSprite = sprite;

            if (sprite != null)
                EditorGUIUtility.PingObject(sprite);

            ShowNotification(new GUIContent(sprite != null ? "Block generated" : "Block generation failed"));
        }

        private void GenerateBackground()
        {
            ClampAllSettings();

            var (w, h) = GetBackgroundDimensions(fullSize: true);
            if (!ValidateTextureSize(w, h, "Background"))
                return;

            if (!TryPrepareExportPlan(backgroundOutputFolder, backgroundFileName, "Background", out var plan))
            {
                ShowNotification(new GUIContent("Background generation cancelled"));
                return;
            }

            var tex = BuildBackgroundTexture(preview: false);
            var sprite = SaveTextureAsSprite(
                tex,
                plan,
                backgroundPixelsPerUnit,
                backgroundFilterMode,
                backgroundCompression,
                backgroundPivot,
                backgroundSpriteBorder
            );

            lastBackgroundSprite = sprite;

            if (sprite != null)
                EditorGUIUtility.PingObject(sprite);

            ShowNotification(new GUIContent(sprite != null ? "Background generated" : "Background generation failed"));
        }

        private void GenerateBoth()
        {
            ClampAllSettings();

            var (blockW, blockH) = GetBlockDimensions();
            var (bgW, bgH) = GetBackgroundDimensions(fullSize: true);

            if (!ValidateTextureSize(blockW, blockH, "Block"))
                return;

            if (!ValidateTextureSize(bgW, bgH, "Background"))
                return;

            string proposedBlockPath = BuildAssetPath(blockOutputFolder, blockFileName);
            string proposedBackgroundPath = BuildAssetPath(backgroundOutputFolder, backgroundFileName);

            if (PathsEqual(proposedBlockPath, proposedBackgroundPath))
            {
                EditorUtility.DisplayDialog(
                    "Same Output Path",
                    "Block and Background are using the same output path. Please use different file names or output folders before generating both.",
                    "OK"
                );
                return;
            }

            if (!TryPrepareExportPlan(blockOutputFolder, blockFileName, "Block", out var blockPlan))
            {
                ShowNotification(new GUIContent("Generate Both cancelled"));
                return;
            }

            if (!TryPrepareExportPlan(backgroundOutputFolder, backgroundFileName, "Background", out var backgroundPlan))
            {
                ShowNotification(new GUIContent("Generate Both cancelled"));
                return;
            }

            if (PathsEqual(blockPlan.assetPath, backgroundPlan.assetPath))
            {
                EditorUtility.DisplayDialog(
                    "Same Final Output Path",
                    "After resolving unique names, Block and Background still point to the same output path. Please use different file names or folders.",
                    "OK"
                );
                return;
            }

            var blockTex = BuildBlockTexture();
            var backgroundTex = BuildBackgroundTexture(preview: false);

            lastBlockSprite = SaveTextureAsSprite(
                blockTex,
                blockPlan,
                blockPixelsPerUnit,
                blockFilterMode,
                blockCompression,
                blockPivot,
                blockSpriteBorder
            );

            lastBackgroundSprite = SaveTextureAsSprite(
                backgroundTex,
                backgroundPlan,
                backgroundPixelsPerUnit,
                backgroundFilterMode,
                backgroundCompression,
                backgroundPivot,
                backgroundSpriteBorder
            );

            if (lastBackgroundSprite != null)
                EditorGUIUtility.PingObject(lastBackgroundSprite);
            else if (lastBlockSprite != null)
                EditorGUIUtility.PingObject(lastBlockSprite);

            bool success = lastBlockSprite != null && lastBackgroundSprite != null;
            ShowNotification(new GUIContent(success ? "Block and Background generated" : "Generate Both finished with errors"));
        }

        private bool TryPrepareExportPlan(DefaultAsset targetFolder, string fileName, string outputLabel, out ExportPlan plan)
        {
            string assetPath = BuildAssetPath(targetFolder, fileName);
            string fullPath = AssetPathToFullPath(assetPath);

            if (OutputAlreadyExists(assetPath, fullPath))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    $"Replace Existing {outputLabel} Output?",
                    $"An output file already exists at:\n\n{assetPath}\n\nChoose Replace to overwrite it, Create Unique to save with a new name, or Cancel.",
                    "Replace",
                    "Cancel",
                    "Create Unique"
                );

                if (choice == 1)
                {
                    plan = new ExportPlan();
                    return false;
                }

                if (choice == 2)
                {
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath).Replace("\\", "/");
                    fullPath = AssetPathToFullPath(assetPath);
                }
            }

            plan = new ExportPlan(outputLabel, assetPath, fullPath);
            return true;
        }

        private Sprite SaveTextureAsSprite(
            Texture2D tex,
            ExportPlan plan,
            float spritePixelsPerUnit,
            FilterMode textureFilterMode,
            TextureImporterCompression textureCompression,
            Vector2 spritePivot,
            Vector4 spriteBorder
        )
        {
            if (tex == null)
                return null;

            int texWidth = tex.width;
            int texHeight = tex.height;

            try
            {
                string directory = Path.GetDirectoryName(plan.fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                byte[] pngBytes = tex.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0)
                    throw new IOException($"Failed to encode {plan.label} texture as PNG.");

                File.WriteAllBytes(plan.fullPath, pngBytes);
                DestroyImmediate(tex);
                tex = null;

                AssetDatabase.ImportAsset(plan.assetPath, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(plan.assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;

                    TextureImporterSettings importerSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(importerSettings);
                    importerSettings.spriteAlignment = (int)SpriteAlignment.Custom;
                    importerSettings.spritePivot = ClampPivot(spritePivot);
                    importerSettings.spriteBorder = ClampBorder(spriteBorder, texWidth, texHeight);
                    importer.SetTextureSettings(importerSettings);

                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = textureFilterMode;
                    importer.spritePixelsPerUnit = Mathf.Max(1f, spritePixelsPerUnit);
                    importer.textureCompression = textureCompression;

                    importer.SaveAndReimport();
                }

                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<Sprite>(plan.assetPath);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
            finally
            {
                if (tex != null)
                    DestroyImmediate(tex);
            }
        }

        // -------------------------------------------------------
        // Presets
        // -------------------------------------------------------
        private BlockSpriteGeneratorPreset SavePresetAsNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Block Sprite Generator Preset",
                "BlockSpriteGeneratorPreset",
                "asset",
                "Choose where to save the preset asset."
            );

            if (string.IsNullOrEmpty(path))
                return null;

            var preset = CreateInstance<BlockSpriteGeneratorPreset>();
            CopyToPreset(preset);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return preset;
        }

        private void ApplyFromPreset(BlockSpriteGeneratorPreset preset)
        {
            if (preset == null)
                return;

            blockFace = preset.blockFace;
            blockTop = preset.blockTop;
            blockBottom = preset.blockBottom;
            blockLeft = preset.blockLeft;
            blockRight = preset.blockRight;

            blockSize = preset.blockSize;
            rimThickness = preset.rimThickness;
            blockCornerRadius = preset.blockCornerRadius;
            blockCornerAA = preset.blockCornerAA;
            blockCornerMode = preset.blockCornerMode;

            bgColumns = preset.bgColumns;
            bgRows = preset.bgRows;
            bgCellPixelSize = preset.bgCellPixelSize;

            bgFill = preset.bgFill;
            bgLineH = preset.bgLineH;
            bgLineV = preset.bgLineV;
            bgOutline = preset.bgOutline;

            bgGridLineThickness = preset.bgGridLineThickness;
            bgOutlineThickness = preset.bgOutlineThickness;
            bgCornerRadius = preset.bgCornerRadius;
            bgCornerAA = preset.bgCornerAA;

            blockOutputFolder = preset.blockOutputFolder;
            blockFileName = preset.blockFileName;
            blockPixelsPerUnit = preset.blockPixelsPerUnit;
            blockFilterMode = preset.blockFilterMode;
            blockCompression = preset.blockCompression;
            blockPivot = preset.blockPivot;
            blockSpriteBorder = preset.blockSpriteBorder;

            backgroundOutputFolder = preset.backgroundOutputFolder;
            backgroundFileName = preset.backgroundFileName;
            backgroundPixelsPerUnit = preset.backgroundPixelsPerUnit;
            backgroundFilterMode = preset.backgroundFilterMode;
            backgroundCompression = preset.backgroundCompression;
            backgroundPivot = preset.backgroundPivot;
            backgroundSpriteBorder = preset.backgroundSpriteBorder;

            autoPreview = preset.autoPreview;

            ClampAllSettings();
        }

        private void CopyToPreset(BlockSpriteGeneratorPreset preset)
        {
            if (preset == null)
                return;

            ClampAllSettings();

            preset.blockFace = blockFace;
            preset.blockTop = blockTop;
            preset.blockBottom = blockBottom;
            preset.blockLeft = blockLeft;
            preset.blockRight = blockRight;

            preset.blockSize = blockSize;
            preset.rimThickness = rimThickness;
            preset.blockCornerRadius = blockCornerRadius;
            preset.blockCornerAA = blockCornerAA;
            preset.blockCornerMode = blockCornerMode;

            preset.bgColumns = bgColumns;
            preset.bgRows = bgRows;
            preset.bgCellPixelSize = bgCellPixelSize;

            preset.bgFill = bgFill;
            preset.bgLineH = bgLineH;
            preset.bgLineV = bgLineV;
            preset.bgOutline = bgOutline;

            preset.bgGridLineThickness = bgGridLineThickness;
            preset.bgOutlineThickness = bgOutlineThickness;
            preset.bgCornerRadius = bgCornerRadius;
            preset.bgCornerAA = bgCornerAA;

            preset.blockOutputFolder = blockOutputFolder;
            preset.blockFileName = blockFileName;
            preset.blockPixelsPerUnit = blockPixelsPerUnit;
            preset.blockFilterMode = blockFilterMode;
            preset.blockCompression = blockCompression;
            preset.blockPivot = blockPivot;
            preset.blockSpriteBorder = blockSpriteBorder;

            preset.backgroundOutputFolder = backgroundOutputFolder;
            preset.backgroundFileName = backgroundFileName;
            preset.backgroundPixelsPerUnit = backgroundPixelsPerUnit;
            preset.backgroundFilterMode = backgroundFilterMode;
            preset.backgroundCompression = backgroundCompression;
            preset.backgroundPivot = backgroundPivot;
            preset.backgroundSpriteBorder = backgroundSpriteBorder;

            preset.autoPreview = autoPreview;
        }

        private void ApplyPixelArtDefaults()
        {
            blockFilterMode = FilterMode.Point;
            backgroundFilterMode = FilterMode.Point;

            blockCompression = TextureImporterCompression.Uncompressed;
            backgroundCompression = TextureImporterCompression.Uncompressed;

            blockPixelsPerUnit = Mathf.Max(1f, blockSize);
            backgroundPixelsPerUnit = Mathf.Max(1f, bgCellPixelSize);

            blockCornerAA = 0;
            bgCornerAA = 0;
            bgGridLineThickness = 1;

            blockPivot = new Vector2(0.5f, 0.5f);
            backgroundPivot = new Vector2(0.5f, 0.5f);

            blockSpriteBorder = Vector4.zero;
            backgroundSpriteBorder = Vector4.zero;

            ClampAllSettings();
        }

        private void ResetToDefaults()
        {
            var defaults = ScriptableObject.CreateInstance<BlockSpriteGeneratorPreset>();
            ApplyFromPreset(defaults);
            DestroyImmediate(defaults);
        }

        // -------------------------------------------------------
        // Texture building
        // -------------------------------------------------------
        private Texture2D BuildBlockTexture()
        {
            int s = Mathf.Clamp(blockSize, 8, MaxBlockSize);
            int t = Mathf.Clamp(rimThickness, 0, s / 2);
            int r = Mathf.Clamp(blockCornerRadius, 0, s / 2);
            int aa = Mathf.Clamp(blockCornerAA, 0, 16);

            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false, false);
            tex.name = "Block";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = blockFilterMode;

            var pixels = new Color32[s * s];

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float a = RoundedRectAlpha(x, y, s, s, r, aa);
                    if (a <= 0f)
                    {
                        pixels[y * s + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color c = PickBlockColor(x, y, s, t);
                    c.a = a;
                    pixels[y * s + x] = (Color32)c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private Color PickBlockColor(int x, int y, int s, int t)
        {
            if (t <= 0)
                return blockFace;

            int dTop = (s - 1) - y;
            int dBottom = y;
            int dLeft = x;
            int dRight = (s - 1) - x;

            if (blockCornerMode == BlockCornerMode.Strips)
            {
                // Hard regions, priority order (no gradients).
                if (dTop < t) return blockTop;
                if (dBottom < t) return blockBottom;
                if (dLeft < t) return blockLeft;
                if (dRight < t) return blockRight;
                return blockFace;
            }

            // Closest edge wins (hard selection).
            int best = int.MaxValue;
            Color picked = blockFace;
            bool any = false;

            if (dTop < t) { best = dTop; picked = blockTop; any = true; }
            if (dBottom < t && dBottom < best) { best = dBottom; picked = blockBottom; any = true; }
            if (dLeft < t && dLeft < best) { best = dLeft; picked = blockLeft; any = true; }
            if (dRight < t && dRight < best) { best = dRight; picked = blockRight; any = true; }

            return any ? picked : blockFace;
        }

        private Texture2D BuildBackgroundTexture(bool preview)
        {
            int cols = Mathf.Clamp(bgColumns, 1, MaxGridCells);
            int rows = Mathf.Clamp(bgRows, 1, MaxGridCells);
            int cell = Mathf.Clamp(bgCellPixelSize, 4, MaxCellPixelSize);

            int gridT = Mathf.Clamp(bgGridLineThickness, 1, 64);
            int outT = Mathf.Clamp(bgOutlineThickness, 0, 256);
            int radius = Mathf.Clamp(bgCornerRadius, 0, 4096);
            int aa = Mathf.Clamp(bgCornerAA, 0, 16);

            int w = cols * cell + outT * 2;
            int h = rows * cell + outT * 2;

            // Downscale ONLY preview to keep the editor responsive.
            if (preview)
            {
                int maxDim = Mathf.Max(w, h);
                if (maxDim > PreviewMaxDim)
                {
                    float scale = PreviewMaxDim / (float)maxDim;

                    cell = Mathf.Max(1, Mathf.RoundToInt(cell * scale));
                    gridT = Mathf.Max(1, Mathf.RoundToInt(gridT * scale));
                    outT = (outT > 0) ? Mathf.Max(1, Mathf.RoundToInt(outT * scale)) : 0;
                    radius = Mathf.RoundToInt(radius * scale);

                    w = cols * cell + outT * 2;
                    h = rows * cell + outT * 2;

                    if (scale < 0.6f)
                        aa = 0;
                }
            }

            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.name = "BackgroundGrid";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = backgroundFilterMode;

            var pixels = new Color32[w * h];

            float halfGrid = gridT * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float outerA = RoundedRectAlpha(x, y, w, h, radius, aa);
                    if (outerA <= 0f)
                    {
                        pixels[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    bool isOutline = IsOutlinePixel(x, y, w, h, outT, radius);

                    Color c;
                    if (isOutline)
                    {
                        c = bgOutline;
                    }
                    else
                    {
                        int lx = x - outT;
                        int ly = y - outT;

                        bool onV = IsOnInternalBoundary(lx, cell, cols, halfGrid);
                        bool onH = IsOnInternalBoundary(ly, cell, rows, halfGrid);

                        if (onV && onH) c = bgOutline;
                        else if (onV) c = bgLineV;
                        else if (onH) c = bgLineH;
                        else c = bgFill;
                    }

                    c.a *= outerA;
                    pixels[y * w + x] = (Color32)c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        // -------------------------------------------------------
        // Validation + helpers
        // -------------------------------------------------------
        private void ClampAllSettings()
        {
            blockSize = Mathf.Clamp(blockSize, 8, MaxBlockSize);
            rimThickness = Mathf.Clamp(rimThickness, 0, blockSize / 2);
            blockCornerRadius = Mathf.Clamp(blockCornerRadius, 0, blockSize / 2);
            blockCornerAA = Mathf.Clamp(blockCornerAA, 0, 16);

            bgColumns = Mathf.Clamp(bgColumns, 1, MaxGridCells);
            bgRows = Mathf.Clamp(bgRows, 1, MaxGridCells);
            bgCellPixelSize = Mathf.Clamp(bgCellPixelSize, 4, MaxCellPixelSize);
            bgGridLineThickness = Mathf.Clamp(bgGridLineThickness, 1, 64);
            bgOutlineThickness = Mathf.Clamp(bgOutlineThickness, 0, 256);
            bgCornerRadius = Mathf.Clamp(bgCornerRadius, 0, 4096);
            bgCornerAA = Mathf.Clamp(bgCornerAA, 0, 16);

            blockPixelsPerUnit = Mathf.Max(1f, blockPixelsPerUnit);
            backgroundPixelsPerUnit = Mathf.Max(1f, backgroundPixelsPerUnit);

            blockPivot = ClampPivot(blockPivot);
            backgroundPivot = ClampPivot(backgroundPivot);

            var (blockW, blockH) = GetBlockDimensions();
            var (bgW, bgH) = GetBackgroundDimensions(fullSize: true);
            blockSpriteBorder = ClampBorder(blockSpriteBorder, blockW, blockH);
            backgroundSpriteBorder = ClampBorder(backgroundSpriteBorder, bgW, bgH);

            if (string.IsNullOrWhiteSpace(blockFileName))
                blockFileName = "Block";

            if (string.IsNullOrWhiteSpace(backgroundFileName))
                backgroundFileName = "BackgroundGrid";

            if (!IsValidAssetsFolder(GetFolderPrefsPath(blockOutputFolder)))
                blockOutputFolder = null;

            if (!IsValidAssetsFolder(GetFolderPrefsPath(backgroundOutputFolder)))
                backgroundOutputFolder = null;
        }

        private bool ValidateTextureSize(int width, int height, string label)
        {
            int maxTextureSize = Mathf.Max(1, SystemInfo.maxTextureSize);

            if (width <= 0 || height <= 0)
            {
                EditorUtility.DisplayDialog(
                    $"{label} Texture Size Invalid",
                    $"{label} texture size is {width} × {height}px. Width and height must both be greater than zero.",
                    "OK"
                );
                return false;
            }

            if (width > maxTextureSize || height > maxTextureSize)
            {
                EditorUtility.DisplayDialog(
                    $"{label} Texture Too Large",
                    $"{label} texture size is {width} × {height}px.\n\nThis exceeds the current system max texture size of {maxTextureSize}px.",
                    "OK"
                );
                return false;
            }

            long pixels = (long)width * height;
            if (pixels > int.MaxValue)
            {
                EditorUtility.DisplayDialog(
                    $"{label} Texture Too Large",
                    $"{label} texture size is {width} × {height}px.\n\nThis creates {pixels:n0} pixels, which is too large for a single Color32 array.",
                    "OK"
                );
                return false;
            }

            long memoryBytes = EstimateTextureMemoryBytes(width, height);
            if (memoryBytes > LargeTextureWarningBytes)
            {
                return EditorUtility.DisplayDialog(
                    $"{label} Large Texture",
                    $"{label} texture size is {width} × {height}px.\n\nEstimated raw RGBA32 memory: {FormatBytes(memoryBytes)}.\n\nContinue?",
                    "Generate",
                    "Cancel"
                );
            }

            return true;
        }

        private static long EstimateTextureMemoryBytes(int width, int height)
        {
            return (long)Mathf.Max(0, width) * Mathf.Max(0, height) * 4L;
        }

        private static string FormatBytes(long bytes)
        {
            const long kb = 1024L;
            const long mb = kb * 1024L;
            const long gb = mb * 1024L;

            if (bytes >= gb)
                return (bytes / (float)gb).ToString("0.##") + " GB";

            if (bytes >= mb)
                return (bytes / (float)mb).ToString("0.##") + " MB";

            if (bytes >= kb)
                return (bytes / (float)kb).ToString("0.##") + " KB";

            return bytes + " B";
        }

        private (int w, int h) GetBlockDimensions()
        {
            int s = Mathf.Clamp(blockSize, 8, MaxBlockSize);
            return (s, s);
        }

        private (int w, int h) GetBackgroundDimensions(bool fullSize)
        {
            int cols = Mathf.Clamp(bgColumns, 1, MaxGridCells);
            int rows = Mathf.Clamp(bgRows, 1, MaxGridCells);
            int cell = Mathf.Clamp(bgCellPixelSize, 4, MaxCellPixelSize);
            int outT = Mathf.Clamp(bgOutlineThickness, 0, 256);

            int w = cols * cell + outT * 2;
            int h = rows * cell + outT * 2;

            if (!fullSize)
            {
                int maxDim = Mathf.Max(w, h);
                if (maxDim > PreviewMaxDim)
                {
                    float scale = PreviewMaxDim / (float)maxDim;
                    cell = Mathf.Max(1, Mathf.RoundToInt(cell * scale));
                    outT = (outT > 0) ? Mathf.Max(1, Mathf.RoundToInt(outT * scale)) : 0;
                    w = cols * cell + outT * 2;
                    h = rows * cell + outT * 2;
                }
            }

            return (w, h);
        }

        private static Vector2 ClampPivot(Vector2 pivot)
        {
            pivot.x = Mathf.Clamp01(pivot.x);
            pivot.y = Mathf.Clamp01(pivot.y);
            return pivot;
        }

        private static Vector4 ClampBorder(Vector4 border, int width, int height)
        {
            width = Mathf.Max(0, width);
            height = Mathf.Max(0, height);

            border.x = Mathf.Clamp(border.x, 0f, width);
            border.y = Mathf.Clamp(border.y, 0f, height);
            border.z = Mathf.Clamp(border.z, 0f, width);
            border.w = Mathf.Clamp(border.w, 0f, height);

            float horizontal = border.x + border.z;
            if (horizontal > width && horizontal > 0f)
            {
                float scale = width / horizontal;
                border.x *= scale;
                border.z *= scale;
            }

            float vertical = border.y + border.w;
            if (vertical > height && vertical > 0f)
            {
                float scale = height / vertical;
                border.y *= scale;
                border.w *= scale;
            }

            return border;
        }

        private string BuildAssetPath(DefaultAsset targetFolder, string fileName)
        {
            string folder = ResolveFolderPath(targetFolder);
            string fileNameNoExt = SanitizeFileName(fileName);
            return $"{folder}/{fileNameNoExt}.png".Replace("\\", "/");
        }

        private static bool OutputAlreadyExists(string assetPath, string fullPath)
        {
            return File.Exists(fullPath) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(
                (a ?? string.Empty).Replace("\\", "/"),
                (b ?? string.Empty).Replace("\\", "/"),
                System.StringComparison.OrdinalIgnoreCase
            );
        }

        private static string ResolveFolderPath(DefaultAsset folderAsset)
        {
            if (folderAsset == null)
                return "Assets";

            string path = AssetDatabase.GetAssetPath(folderAsset);
            if (IsValidAssetsFolder(path))
                return path;

            return "Assets";
        }

        private static bool IsValidAssetsFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            path = path.Replace("\\", "/");
            return AssetDatabase.IsValidFolder(path) && (path == "Assets" || path.StartsWith("Assets/"));
        }

        private static string GetFolderPrefsPath(DefaultAsset folderAsset)
        {
            if (folderAsset == null)
                return string.Empty;

            string folderPath = AssetDatabase.GetAssetPath(folderAsset);
            return IsValidAssetsFolder(folderPath) ? folderPath.Replace("\\", "/") : string.Empty;
        }

        private static DefaultAsset LoadFolderAsset(string folderPath)
        {
            if (IsValidAssetsFolder(folderPath))
                return AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);

            return null;
        }

        private static string GetAssetPrefsPath(UnityEngine.Object asset)
        {
            return asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
        }

        private static T LoadAssetOrNull<T>(string assetPath) where T : Object
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            // assetPath: "Assets/..."
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "Generated";

            s = s.Trim().Replace('\\', '/');

            int slash = s.LastIndexOf('/');
            if (slash >= 0 && slash < s.Length - 1)
                s = s.Substring(slash + 1);

            try
            {
                s = Path.GetFileNameWithoutExtension(s);
            }
            catch
            {
                // Fall back to the raw text if the platform path parser rejects it.
            }

            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            s = s.Replace('/', '_').Replace('\\', '_').Trim().Trim('.');

            return string.IsNullOrWhiteSpace(s) ? "Generated" : s;
        }

        // Grid boundary check (hard lines). Only boundaries between cells, not outer edges.
        private static bool IsOnInternalBoundary(int localPixel, int cellSize, int cells, float halfThickness)
        {
            if (cells <= 1)
                return false;

            if (cellSize <= 0)
                return false;

            float p = localPixel + 0.5f;
            float idxF = p / cellSize;
            int nearest = Mathf.RoundToInt(idxF);

            if (nearest <= 0 || nearest >= cells)
                return false;

            float boundary = nearest * cellSize;
            return Mathf.Abs(p - boundary) <= halfThickness;
        }

        // Outline pixels follow rounded rect: inside outer but outside inner.
        private static bool IsOutlinePixel(int x, int y, int w, int h, int outlineT, int outerRadius)
        {
            if (outlineT <= 0)
                return false;

            int iw = w - outlineT * 2;
            int ih = h - outlineT * 2;
            if (iw <= 0 || ih <= 0)
                return true;

            int xi = x - outlineT;
            int yi = y - outlineT;
            if (xi < 0 || yi < 0 || xi >= iw || yi >= ih)
                return true;

            int innerRadius = Mathf.Max(0, outerRadius - outlineT);

            // Crisp inner mask for hard region classification.
            float innerA = RoundedRectAlpha(xi, yi, iw, ih, innerRadius, 0);
            return innerA <= 0f;
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Alpha-only rounded rectangle mask (no color blending).
        private static float RoundedRectAlpha(int x, int y, int w, int h, int radius, int aa)
        {
            if (radius <= 0)
                return 1f;

            bool bl = x < radius && y < radius;
            bool br = x >= w - radius && y < radius;
            bool tl = x < radius && y >= h - radius;
            bool tr = x >= w - radius && y >= h - radius;

            if (!(bl || br || tl || tr))
                return 1f;

            float cx = (bl || tl) ? radius : (w - radius);
            float cy = (bl || br) ? radius : (h - radius);

            float px = x + 0.5f;
            float py = y + 0.5f;

            float dx = px - cx;
            float dy = py - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            float edge = dist - radius;

            if (aa <= 0)
                return (edge <= 0f) ? 1f : 0f;

            float t = Mathf.Clamp01((aa - edge) / (2f * aa));
            return Smooth01(t);
        }

        // -----------------------------
        // Persistence (EditorPrefs)
        // -----------------------------
        private string PrefPrefix => $"MOST.BlockSpriteGen.{StableHash(Application.dataPath)}.";

        private string K(string name) => PrefPrefix + name;

        private static int StableHash(string s)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < s.Length; i++)
                    hash = hash * 31 + s[i];
                return hash;
            }
        }

        private static void SetColor(string key, Color c)
        {
            EditorPrefs.SetString(key, "#" + ColorUtility.ToHtmlStringRGBA(c));
        }

        private static Color GetColor(string key, Color fallback)
        {
            string s = EditorPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c))
                return c;
            return fallback;
        }

        private static void SetVector2(string key, Vector2 value)
        {
            EditorPrefs.SetFloat(key + ".x", value.x);
            EditorPrefs.SetFloat(key + ".y", value.y);
        }

        private static Vector2 GetVector2(string key, Vector2 fallback)
        {
            if (!EditorPrefs.HasKey(key + ".x") || !EditorPrefs.HasKey(key + ".y"))
                return fallback;

            return new Vector2(
                EditorPrefs.GetFloat(key + ".x", fallback.x),
                EditorPrefs.GetFloat(key + ".y", fallback.y)
            );
        }

        private static void SetVector4(string key, Vector4 value)
        {
            EditorPrefs.SetFloat(key + ".x", value.x);
            EditorPrefs.SetFloat(key + ".y", value.y);
            EditorPrefs.SetFloat(key + ".z", value.z);
            EditorPrefs.SetFloat(key + ".w", value.w);
        }

        private static Vector4 GetVector4(string key, Vector4 fallback)
        {
            if (!EditorPrefs.HasKey(key + ".x") || !EditorPrefs.HasKey(key + ".y") || !EditorPrefs.HasKey(key + ".z") || !EditorPrefs.HasKey(key + ".w"))
                return fallback;

            return new Vector4(
                EditorPrefs.GetFloat(key + ".x", fallback.x),
                EditorPrefs.GetFloat(key + ".y", fallback.y),
                EditorPrefs.GetFloat(key + ".z", fallback.z),
                EditorPrefs.GetFloat(key + ".w", fallback.w)
            );
        }

        private static TEnum GetEnumPref<TEnum>(string key, TEnum fallback) where TEnum : struct
        {
            int value = EditorPrefs.GetInt(key, System.Convert.ToInt32(fallback));
            if (System.Enum.IsDefined(typeof(TEnum), value))
                return (TEnum)System.Enum.ToObject(typeof(TEnum), value);

            return fallback;
        }

        private void SavePrefs()
        {
            // Preset
            EditorPrefs.SetString(K("activePresetPath"), GetAssetPrefsPath(activePreset));

            // Block
            SetColor(K("blockFace"), blockFace);
            SetColor(K("blockTop"), blockTop);
            SetColor(K("blockBottom"), blockBottom);
            SetColor(K("blockLeft"), blockLeft);
            SetColor(K("blockRight"), blockRight);

            EditorPrefs.SetInt(K("blockSize"), blockSize);
            EditorPrefs.SetInt(K("rimThickness"), rimThickness);
            EditorPrefs.SetInt(K("blockCornerRadius"), blockCornerRadius);
            EditorPrefs.SetInt(K("blockCornerAA"), blockCornerAA);
            EditorPrefs.SetInt(K("blockCornerMode"), (int)blockCornerMode);

            // Background
            EditorPrefs.SetInt(K("bgColumns"), bgColumns);
            EditorPrefs.SetInt(K("bgRows"), bgRows);
            EditorPrefs.SetInt(K("bgCellPixelSize"), bgCellPixelSize);

            SetColor(K("bgFill"), bgFill);
            SetColor(K("bgLineH"), bgLineH);
            SetColor(K("bgLineV"), bgLineV);
            SetColor(K("bgOutline"), bgOutline);

            EditorPrefs.SetInt(K("bgGridLineThickness"), bgGridLineThickness);
            EditorPrefs.SetInt(K("bgOutlineThickness"), bgOutlineThickness);
            EditorPrefs.SetInt(K("bgCornerRadius"), bgCornerRadius);
            EditorPrefs.SetInt(K("bgCornerAA"), bgCornerAA);

            // Export - Block
            EditorPrefs.SetString(K("blockFileName"), blockFileName ?? "Block");
            EditorPrefs.SetFloat(K("blockPixelsPerUnit"), blockPixelsPerUnit);
            EditorPrefs.SetInt(K("blockFilterMode"), (int)blockFilterMode);
            EditorPrefs.SetInt(K("blockCompression"), (int)blockCompression);
            SetVector2(K("blockPivot"), blockPivot);
            SetVector4(K("blockSpriteBorder"), blockSpriteBorder);
            EditorPrefs.SetString(K("blockOutputFolderPath"), GetFolderPrefsPath(blockOutputFolder));

            // Export - Background
            EditorPrefs.SetString(K("backgroundFileName"), backgroundFileName ?? "BackgroundGrid");
            EditorPrefs.SetFloat(K("backgroundPixelsPerUnit"), backgroundPixelsPerUnit);
            EditorPrefs.SetInt(K("backgroundFilterMode"), (int)backgroundFilterMode);
            EditorPrefs.SetInt(K("backgroundCompression"), (int)backgroundCompression);
            SetVector2(K("backgroundPivot"), backgroundPivot);
            SetVector4(K("backgroundSpriteBorder"), backgroundSpriteBorder);
            EditorPrefs.SetString(K("backgroundOutputFolderPath"), GetFolderPrefsPath(backgroundOutputFolder));

            EditorPrefs.SetBool(K("autoPreview"), autoPreview);
        }

        private void LoadPrefs()
        {
            // Preset
            activePreset = LoadAssetOrNull<BlockSpriteGeneratorPreset>(EditorPrefs.GetString(K("activePresetPath"), string.Empty));

            // Block (defaults are current field initializers)
            blockFace = GetColor(K("blockFace"), blockFace);
            blockTop = GetColor(K("blockTop"), blockTop);
            blockBottom = GetColor(K("blockBottom"), blockBottom);
            blockLeft = GetColor(K("blockLeft"), blockLeft);
            blockRight = GetColor(K("blockRight"), blockRight);

            blockSize = EditorPrefs.GetInt(K("blockSize"), blockSize);
            rimThickness = EditorPrefs.GetInt(K("rimThickness"), rimThickness);
            blockCornerRadius = EditorPrefs.GetInt(K("blockCornerRadius"), blockCornerRadius);
            blockCornerAA = EditorPrefs.GetInt(K("blockCornerAA"), blockCornerAA);
            blockCornerMode = GetEnumPref(K("blockCornerMode"), blockCornerMode);

            // Background
            bgColumns = EditorPrefs.GetInt(K("bgColumns"), bgColumns);
            bgRows = EditorPrefs.GetInt(K("bgRows"), bgRows);
            bgCellPixelSize = EditorPrefs.GetInt(K("bgCellPixelSize"), bgCellPixelSize);

            bgFill = GetColor(K("bgFill"), bgFill);
            bgLineH = GetColor(K("bgLineH"), bgLineH);
            bgLineV = GetColor(K("bgLineV"), bgLineV);
            bgOutline = GetColor(K("bgOutline"), bgOutline);

            bgGridLineThickness = EditorPrefs.GetInt(K("bgGridLineThickness"), bgGridLineThickness);
            bgOutlineThickness = EditorPrefs.GetInt(K("bgOutlineThickness"), bgOutlineThickness);
            bgCornerRadius = EditorPrefs.GetInt(K("bgCornerRadius"), bgCornerRadius);
            bgCornerAA = EditorPrefs.GetInt(K("bgCornerAA"), bgCornerAA);

            // Legacy fallbacks from the previous shared export settings.
            string legacyFolderPath = EditorPrefs.GetString(K("outputFolderPath"), string.Empty);
            float legacyPixelsPerUnit = EditorPrefs.GetFloat(K("pixelsPerUnit"), 100f);
            FilterMode legacyFilterMode = (FilterMode)EditorPrefs.GetInt(K("filterMode"), (int)FilterMode.Bilinear);
            bool legacyUncompressed = EditorPrefs.GetBool(K("uncompressed"), true);
            TextureImporterCompression legacyCompression = legacyUncompressed
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;

            // Export - Block
            blockFileName = EditorPrefs.GetString(K("blockFileName"), blockFileName ?? "Block");
            blockPixelsPerUnit = EditorPrefs.GetFloat(K("blockPixelsPerUnit"), legacyPixelsPerUnit);
            blockFilterMode = GetEnumPref(K("blockFilterMode"), legacyFilterMode);
            blockCompression = GetEnumPref(K("blockCompression"), legacyCompression);
            blockPivot = GetVector2(K("blockPivot"), blockPivot);
            blockSpriteBorder = GetVector4(K("blockSpriteBorder"), blockSpriteBorder);
            blockOutputFolder = LoadFolderAsset(EditorPrefs.GetString(K("blockOutputFolderPath"), legacyFolderPath));

            // Export - Background
            backgroundFileName = EditorPrefs.GetString(K("backgroundFileName"), backgroundFileName ?? "BackgroundGrid");
            backgroundPixelsPerUnit = EditorPrefs.GetFloat(K("backgroundPixelsPerUnit"), legacyPixelsPerUnit);
            backgroundFilterMode = GetEnumPref(K("backgroundFilterMode"), legacyFilterMode);
            backgroundCompression = GetEnumPref(K("backgroundCompression"), legacyCompression);
            backgroundPivot = GetVector2(K("backgroundPivot"), backgroundPivot);
            backgroundSpriteBorder = GetVector4(K("backgroundSpriteBorder"), backgroundSpriteBorder);
            backgroundOutputFolder = LoadFolderAsset(EditorPrefs.GetString(K("backgroundOutputFolderPath"), legacyFolderPath));

            autoPreview = EditorPrefs.GetBool(K("autoPreview"), autoPreview);
        }
    }
}
#endif
