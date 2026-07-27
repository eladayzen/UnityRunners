using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Solo.MOST_IN_ONE
{
    public enum LevelStoringType
    {
        Prefabs = 0,
        Scenes = 1,
        ScriptableObjects = 3,
        None = 2
    }

    [Serializable]
    public struct ObjectsJump
    {
        [Tooltip("Unique identifier used to reference this menu entry (e.g., by OnWinMenuName / OnLoseMenuName). Keep names unique.")]
        public string Name;

        [Tooltip("Root GameObject/panel to activate when this entry is selected. Other panels can be disabled based on 'DeactivateAllOnEnable'.")]
        public GameObject TargetObject;

        [Tooltip("UI Button that triggers showing this entry's TargetObject.")]
        public Button Listener;

        [Tooltip("If true, deactivate all other TargetObjects before activating this one. If false, only activate this one without touching others.")]
        public bool DeactivateAllOnEnable;
    }

    [DisallowMultipleComponent, HideScriptField]
    public class UniversalGameManager : MonoBehaviour
    {
        [HelpBox("Menu Jump wires listed Buttons to show specific UI panels. Enable 'UseMenuJump' and fill 'Menus' so each Button reveals its target panel. Use 'DeactivateAllOnEnable' to ensure only one panel is visible at a time.")]
        [BigHeader("Menu Jump")]
        public bool UseMenuJump;

        [Tooltip("Mappings of Buttons to the panels they should show. Used only when 'UseMenuJump' is enabled.")]
        public ObjectsJump[] Menus;

        [BigHeader("Levels Control"), Tooltip("How levels are provided: Prefabs (instantiated), Scenes (loaded), or None (menu-only/testing).")]
        public LevelStoringType LevelStoreType = LevelStoringType.Prefabs;

        [Required, HideIfAny(nameof(LevelStoreType), LevelStoringType.None, true)]
        public MOST_Database DatabaseHolder;
        [HideIfAny(nameof(LevelStoreType), LevelStoringType.None, true), Tooltip("Name of the IntData entry that stores the CURRENT level (1-based).")]
        public string LevelDataName = "CurrentLevel";

#if UNITY_EDITOR
        [Line]
        [Required, Tooltip("Main menu Scene used as the entry point/shell scene for level loading.")]
        public SceneAsset MainMenu;
#endif
        // Runtime mirror
        [HideInInspector, SerializeField] private string _mainMenuPath;

        [Tooltip("List contains all levels (Prefabs). Index 0 is Level 1.")]
        [HideIfAny(nameof(LevelStoreType), LevelStoringType.Prefabs, false)]
        public GameObject[] LevelsPrefs;

#if UNITY_EDITOR
        [HideIfAny(nameof(LevelStoreType), LevelStoringType.Scenes, false),
        Tooltip("List contains all levels (Scenes). Index 0 is Level 1.")]
        public SceneAsset[] LevelsScene;
#endif

        [HideIfAny(nameof(LevelStoreType), LevelStoringType.ScriptableObjects, false)]
        [Tooltip("List contains all levels as ScriptableObjects. Index 0 is Level 1.")]
        public ScriptableObject[] LevelsScriptables;

        [HideIfAny(nameof(LevelStoreType), LevelStoringType.ScriptableObjects, false)]
        [GUIColor("cyan"), Tooltip("Method name invoked on the selected ScriptableObject.\nSupported signatures: (), (int), (UniversalGameManager), (UniversalGameManager,int), (int,UniversalGameManager).")]
        public string ScriptableMessageName = "BuildLevel";

        // ---------- Runtime mirror ----------
        [HideInInspector, SerializeField] private string[] _levelsScenePaths;

        [Tooltip("For Prefabs: parent transform for instantiated level prefabs."), InnerHint("(Optional)"),
        HideIfAny(nameof(LevelStoreType), LevelStoringType.Prefabs, false)]
        public Transform PrefabParent;

        [Tooltip("For Scenes: when true, level scenes are loaded additively into the shell (recommended)."),
        HideIfAny(nameof(LevelStoreType), LevelStoringType.Scenes, false)]
        public bool AdditiveScenes = true;

        [Line]
        [Tooltip("Load a specific custom level instead of using the current level index (useful for debugging)."),
        HideIfAny(nameof(LevelStoreType), LevelStoringType.None, true)]
        public bool LoadCustomLevel = false;

        [Tooltip("Custom prefab to load when 'LoadCustomLevel' is true."),
        HideIfAny(nameof(LevelStoreType), LevelStoringType.Prefabs, false)]
        public GameObject CustomLevelPref;

#if UNITY_EDITOR
        [Tooltip("Custom scene to load when 'LoadCustomLevel' is true."),
        HideIfAny(nameof(LevelStoreType), LevelStoringType.Scenes, false)]
        public SceneAsset CustomLevelScene;
#endif

        [HideIfAny(nameof(LevelStoreType), LevelStoringType.ScriptableObjects, false)]
        [Tooltip("Custom ScriptableObject level to load when 'LoadCustomLevel' is true.")]
        public ScriptableObject CustomLevelScriptable;
        // ---------- Runtime mirror ----------
        [HideInInspector, SerializeField] private string _customLevelScenePath;

        [Line]
        [Tooltip("Name (ObjectsJump.Name) of the menu to show when the player wins.")]
        public string OnWinMenuName;
        [Tooltip("Delay in seconds before showing the win menu.")]
        public float DelayBeforeWinJump;

        [Tooltip("Name (ObjectsJump.Name) of the menu to show when the player loses.")]
        public string OnLoseMenuName;
        [Tooltip("Delay in seconds before showing the lose menu.")]
        public float DelayBeforeLoseJump;

        [Tooltip("Auto-advance after OnWin().")]
        public bool AutoAdvanceOnWin = true;
        [Tooltip("Seconds to wait before advancing after win (uses Invoke, not coroutines). Set 0 for instant.")]
        [Min(0f)] public float NextLevelDelay = 0f;

        [BigHeader("Events")]
        [HelpBox("Call OnStart() to start the game and Invoke OnStartPlay\n" +
                "Call OnWin() to finish the game and Invoke OnWinEvent\n" +
            "Call OnLose() to finish the game and Invoke OnLoseEvent")]
        [Tooltip("Invoked after a level is loaded/instantiated (hook from your code where appropriate).")]
        public UnityEvent OnLoadEvent;

        [Tooltip("Invoke when gameplay actually starts (e.g., from a Play button).")]
        public UnityEvent OnStartPlay;

        [Tooltip("Invoked during OnWin() before switching to the win menu.")]
        public UnityEvent OnWinEvent;

        [Tooltip("Invoked during OnLose() before switching to the lose menu.")]
        public UnityEvent OnLoseEvent;

        [BigHeader("Audio Settings")]
        [HelpBox("Connect a MOST_AudioManager to sync sliders/toggles with audio parameters"), Required]
        public MOST_AudioManager AudioManager;
        [Tooltip("Load saved audio settings on Start(). If no save exists, current AudioManager values are applied and saved.")]
        public bool LoadOnStart = true;     // load JSON on Start()
        [Tooltip("Save audio settings to file whenever a UI control changes.")]
        public bool SaveOnUIChange = true;  // explicitly save to file on each UI change

        [Header("Sliders (0..1)")]
        [InnerHint("(Optional)")] public Slider MasterSlider;
        [InnerHint("(Optional)")] public Slider MusicSlider;
        [InnerHint("(Optional)")] public Slider SFXSlider;
        [InnerHint("(Optional)")] public Slider UISlider;

        [Header("On/Off Toggles")]
        [InnerHint("(Optional)")] public Toggle MasterToggle;
        [InnerHint("(Optional)")] public Toggle MusicToggle;
        [InnerHint("(Optional)")] public Toggle SFXToggle;
        [InnerHint("(Optional)")] public Toggle UIToggle;

        [BigHeader("Haptic Settings")]
        [HelpBox("")]
        [InnerHint("(Optional)")] public Toggle HapticToggle;

        bool _wired;
        int _currentLevel = 1;
        readonly Dictionary<Button, UnityAction> _installed = new();

        void Awake()
        {
            InitLevels();
        }

        void Start()
        {
            if (UseMenuJump) BindObjectJumpListeners();
            if (AudioManager != null) InitAudio();
            //else Debug.LogWarning("[AudioManagerUITest] AudioManager is not assigned. Audio system disabled");
            if (HapticToggle)
            {
                HapticToggle.isOn = MOST_HapticFeedback.HapticsEnabled;
                HapticToggle.onValueChanged.AddListener(UpdateHapticToggle);
            }
            OnLoadEvent.Invoke();
        }

        void OnDestroy()
        {
            UnwireAudioListeners();
            foreach (var kv in _installed)
                if (kv.Key) kv.Key.onClick.RemoveListener(kv.Value);
            _installed.Clear();
        }

        #region Level Control
        void InitLevels()
        {
            if (LevelStoreType == LevelStoringType.None) return;
            if (LoadCustomLevel)
            {
                Loading(0);
                return;
            }
            if (DatabaseHolder == null)
            {
                Debug.LogWarning("[LevelController] DatabaseHolder is null, Level Controller Disabled.");
                return;
            }
            var data = DatabaseHolder.Get<IntData>(LevelDataName);
            if (data == null)
            {
                Debug.LogError($"[LevelController] IntData '{LevelDataName}' not found in DB.");
                return;
            }

            _currentLevel = Mathf.Max(1, data.Value);
            Loading(_currentLevel);
        }

        public void OnStart()
        {
            OnStartPlay?.Invoke();
        }

        public void OnWin()
        {
            var data = DatabaseHolder.Get<IntData>(LevelDataName);
            data.Increment();
            DatabaseHolder.SaveToJson();
            _currentLevel = data.Value;
            StartCoroutine(GameOverState(true));
        }

        public void OnLose()
        {
            StartCoroutine(GameOverState(false));
        }

        public IEnumerator GameOverState(bool isWin)
        {
            if (isWin)
            {
                OnWinEvent.Invoke();
                yield return new WaitForSeconds(DelayBeforeWinJump);
                ObjectsJump menu = Menus.First(name => OnWinMenuName == name.Name);
                if (menu.Name == OnWinMenuName) ShowOnly(menu.TargetObject, menu.DeactivateAllOnEnable);
                if (AutoAdvanceOnWin)
                {
                    yield return new WaitForSeconds(NextLevelDelay);
                    RestartCurrent();
                }
            }
            else
            {
                OnLoseEvent?.Invoke();
                yield return new WaitForSeconds(DelayBeforeLoseJump);
                ObjectsJump menu = Menus.First(name => OnLoseMenuName == name.Name);
                if (menu.Name == OnLoseMenuName) ShowOnly(menu.TargetObject, menu.DeactivateAllOnEnable);
            }
        }

        public void LoadNext() => SetAndLoad(_currentLevel + 1);
        public void LoadPrev() => SetAndLoad(Mathf.Max(1, _currentLevel - 1));
        public void LoadLevel(int value) => SetAndLoad(Mathf.Max(1, value));
        public void RestartCurrent() => SetAndLoad(_currentLevel, writeToDatabase: false);

        void SetAndLoad(int level, bool writeToDatabase = true)
        {
            if (writeToDatabase)
            {
                var data = DatabaseHolder.Get<IntData>(LevelDataName);
                if (data != null) { data.Set(level); DatabaseHolder.SaveToJson(); }
            }

            if (LevelStoreType == LevelStoringType.Scenes && !AdditiveScenes)
            {
                int count = GetLevelCount();
                int idx = Mathf.Clamp(level, 1, Mathf.Max(1, count)) - 1;

                string targetName = LoadCustomLevel
                    ? GetCustomLevelSceneName()
                    : GetLevelSceneNameByIndex(idx);

                if (!string.IsNullOrEmpty(targetName))
                    SceneManager.LoadScene(targetName);
                else
                    Debug.LogError("[UniversalGameManager] Target scene name is empty.");
            }
            else
            {
                var main = GetMainMenuName();
                if (!string.IsNullOrEmpty(main))
                    SceneManager.LoadScene(main);
                else
                    Debug.LogError("[UniversalGameManager] MainMenu scene name is empty.");
            }
        }

        public void Loading(int level)
        {
            if (LevelStoreType == LevelStoringType.Scenes && AdditiveScenes)
            {
                int count = GetLevelCount();
                int idx = Mathf.Clamp(level, 1, Mathf.Max(1, count)) - 1;

                string targetName = LoadCustomLevel
                    ? GetCustomLevelSceneName()
                    : GetLevelSceneNameByIndex(idx);

                if (!string.IsNullOrEmpty(targetName))
                    SceneManager.LoadSceneAsync(targetName, LoadSceneMode.Additive);
                else
                    Debug.LogError("[UniversalGameManager] Target scene name is empty.");
            }
            else if (LevelStoreType == LevelStoringType.Prefabs)
            {
                Instantiate(
                    LoadCustomLevel ? CustomLevelPref : LevelsPrefs[Mathf.Min(level, LevelsPrefs.Length) - 1],
                    Vector3.zero, Quaternion.identity,
                    PrefabParent ? PrefabParent : null);
            }
            else if (LevelStoreType == LevelStoringType.ScriptableObjects)
            {
                var target = LoadCustomLevel ? CustomLevelScriptable : LevelsScriptables[Mathf.Clamp(level, 1, LevelsScriptables.Length) - 1];

                if (!LoadCustomLevel && (LevelsScriptables == null || LevelsScriptables.Length == 0))
                {
                    Debug.LogError("[UniversalGameManager] LevelsScriptables is empty.");
                    return;
                }

                if (!target)
                {
                    Debug.LogError(LoadCustomLevel ? "[UniversalGameManager] Custom Level Scriptable is null" : $"[UniversalGameManager] Scriptable level is null at index {Mathf.Clamp(level, 1, LevelsScriptables.Length) - 1}.");
                    return;
                }

                FireScriptableMessage(target, level);
            }
        }

        void FireScriptableMessage(ScriptableObject target, int levelIndex)
        {
            if (!target) return;
            if (string.IsNullOrEmpty(ScriptableMessageName)) return;

            var t = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var m =
                t.GetMethod(ScriptableMessageName, flags, null, new[] { typeof(UniversalGameManager), typeof(int) }, null) ??
                t.GetMethod(ScriptableMessageName, flags, null, new[] { typeof(int), typeof(UniversalGameManager) }, null) ??
                t.GetMethod(ScriptableMessageName, flags, null, new[] { typeof(UniversalGameManager) }, null) ??
                t.GetMethod(ScriptableMessageName, flags, null, new[] { typeof(int) }, null) ??
                t.GetMethod(ScriptableMessageName, flags, null, Type.EmptyTypes, null);

            if (m == null)
            {
                Debug.LogWarning($"[UniversalGameManager] Scriptable '{target.name}' has no method '{ScriptableMessageName}'.");
                return;
            }

            try
            {
                var ps = m.GetParameters();
                if (ps.Length == 0) m.Invoke(target, null);
                else if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) m.Invoke(target, new object[] { levelIndex });
                else if (ps.Length == 1 && ps[0].ParameterType == typeof(UniversalGameManager)) m.Invoke(target, new object[] { this });
                else if (ps.Length == 2 && ps[0].ParameterType == typeof(UniversalGameManager) && ps[1].ParameterType == typeof(int))
                    m.Invoke(target, new object[] { this, levelIndex });
                else if (ps.Length == 2 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(UniversalGameManager))
                    m.Invoke(target, new object[] { levelIndex, this });
                else
                    m.Invoke(target, null);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        #endregion

        #region Menu Jump
        public void BindObjectJumpListeners()
        {
            if (Menus == null) return;

            for (int i = 0; i < Menus.Length; i++)
            {
                var btn = Menus[i].Listener;
                var target = Menus[i].TargetObject;

                if (!btn || !target) continue;

                // If we previously attached a handler to this button, remove it first
                if (_installed.TryGetValue(btn, out var existing))
                {
                    btn.onClick.RemoveListener(existing);
                    _installed.Remove(btn);
                }

                // Capture a local so the lambda binds the correct target
                var capturedTarget = target;
                int va = i;
                void handler() => ShowOnly(capturedTarget, Menus[va].DeactivateAllOnEnable);
                btn.onClick.AddListener(handler);
                _installed[btn] = handler;
            }
        }

        void ShowOnly(GameObject target, bool deactivateAll) // Activate only the given target, deactivate all others
        {
            if (Menus == null) return;

            if (!deactivateAll) target.SetActive(true);
            else
            {
                for (int i = 0; i < Menus.Length; i++)
                {
                    var go = Menus[i].TargetObject;
                    if (!go) continue;

                    bool shouldBeActive = go == target;
                    if (go.activeSelf != shouldBeActive)
                        go.SetActive(shouldBeActive);
                }
            }
        }
        #endregion

        #region Audio Section
        void InitAudio()
        {
            if (LoadOnStart)
            {
                if (!AudioManager.LoadFromFile(applyNow: false)) // Try load; if nothing saved yet, use whatever is in the Audio manager and apply
                {
                    AudioManager.ApplyAll(); // Keep current Audio manager  values (inspector defaults) and apply them
                    AudioManager.SaveToFile(); // Create initial save file
                }
                else AudioManager.ApplyAll();
            }
            SyncUIFromAudioManager();
            WireAudioListeners();
        }

        void SyncUIFromAudioManager() // sync&apply all data saved on audio manager to all controllers (sliders and Toggles)
        {
            if (MasterSlider) MasterSlider.SetValueWithoutNotify(AudioManager.MasterVolume);
            if (MusicSlider) MusicSlider.SetValueWithoutNotify(AudioManager.MusicVolume);
            if (SFXSlider) SFXSlider.SetValueWithoutNotify(AudioManager.SFXVolume);
            if (UISlider) UISlider.SetValueWithoutNotify(AudioManager.UIVolume);

            if (MasterToggle) MasterToggle.SetIsOnWithoutNotify(AudioManager.Master_IsOn);
            if (MusicToggle) MusicToggle.SetIsOnWithoutNotify(AudioManager.Music_IsOn);
            if (SFXToggle) SFXToggle.SetIsOnWithoutNotify(AudioManager.SFX_IsOn);
            if (UIToggle) UIToggle.SetIsOnWithoutNotify(AudioManager.UI_IsOn);
        }

        void WireAudioListeners() // Add Listeners for all sliders and toggles (if added)
        {
            if (_wired) return;
            _wired = true;

            if (MasterSlider) MasterSlider.onValueChanged.AddListener(UpdateMasterSlider);
            if (MusicSlider) MusicSlider.onValueChanged.AddListener(UpdateMusicSlider);
            if (SFXSlider) SFXSlider.onValueChanged.AddListener(UpdateSFXSlider);
            if (UISlider) UISlider.onValueChanged.AddListener(UpdateUISlider);

            if (MasterToggle) MasterToggle.onValueChanged.AddListener(UpdateMasterToggle);
            if (MusicToggle) MusicToggle.onValueChanged.AddListener(UpdateMusicToggle);
            if (SFXToggle) SFXToggle.onValueChanged.AddListener(UpdateSFXToggle);
            if (UIToggle) UIToggle.onValueChanged.AddListener(UpdateUIToggle);
        }

        void UnwireAudioListeners() // Remove all Listeners for all sliders and toggles (if added)
        {
            if (!_wired) return;
            _wired = false;

            if (MasterSlider) MasterSlider.onValueChanged.RemoveListener(UpdateMasterSlider);
            if (MusicSlider) MusicSlider.onValueChanged.RemoveListener(UpdateMusicSlider);
            if (SFXSlider) SFXSlider.onValueChanged.RemoveListener(UpdateSFXSlider);
            if (UISlider) UISlider.onValueChanged.RemoveListener(UpdateUISlider);

            if (MasterToggle) MasterToggle.onValueChanged.RemoveListener(UpdateMasterToggle);
            if (MusicToggle) MusicToggle.onValueChanged.RemoveListener(UpdateMusicToggle);
            if (SFXToggle) SFXToggle.onValueChanged.RemoveListener(UpdateSFXToggle);
            if (UIToggle) UIToggle.onValueChanged.RemoveListener(UpdateUIToggle);
        }

        // ---------- Handlers: Sliders ----------

        public void UpdateMasterSlider(float v)
        {
            AudioManager.SetVolume(MOST_AudioManager.Channel.Master, v);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        public void UpdateMusicSlider(float v)
        {
            AudioManager.SetVolume(MOST_AudioManager.Channel.Music, v);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        public void UpdateSFXSlider(float v)
        {
            AudioManager.SetVolume(MOST_AudioManager.Channel.SFX, v);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        public void UpdateUISlider(float v)
        {
            AudioManager.SetVolume(MOST_AudioManager.Channel.UI, v);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        // ---------- Handlers: Toggles ----------

        public void UpdateMasterToggle(bool on)
        {
            AudioManager.SetEnabled(MOST_AudioManager.Channel.Master, on);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        public void UpdateMusicToggle(bool on)
        {
            AudioManager.SetEnabled(MOST_AudioManager.Channel.Music, on);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        public void UpdateSFXToggle(bool on)
        {
            AudioManager.SetEnabled(MOST_AudioManager.Channel.SFX, on);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }

        public void UpdateUIToggle(bool on)
        {
            AudioManager.SetEnabled(MOST_AudioManager.Channel.UI, on);
            if (SaveOnUIChange) AudioManager.SaveToFile();
        }
        #endregion

        #region Haptic
        public void UpdateHapticToggle(bool on)
        {
            MOST_HapticFeedback.HapticsEnabled = on;
            HapticToggle.isOn = MOST_HapticFeedback.HapticsEnabled;
        }
        #endregion

        #region Build-safe Scene helpers & Editor sync
        private static string SceneNameFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int slash = path.LastIndexOf('/');
            int dot = path.LastIndexOf('.');
            if (slash < 0) slash = -1;
            if (dot < 0 || dot < slash) dot = path.Length;
            return path.Substring(slash + 1, dot - (slash + 1));
        }

        private int GetLevelCount()
        {
#if UNITY_EDITOR
            return LevelsScene != null ? LevelsScene.Length : (_levelsScenePaths != null ? _levelsScenePaths.Length : 0);
#else
            return _levelsScenePaths != null ? _levelsScenePaths.Length : 0;
#endif
        }

        private string GetMainMenuName() => SceneNameFromPath(_mainMenuPath);

        private string GetLevelSceneNameByIndex(int idx)
        {
#if UNITY_EDITOR
            // Prefer mirrored path (synced in OnValidate), fallback to asset path if needed
            if (_levelsScenePaths != null && idx >= 0 && idx < _levelsScenePaths.Length && !string.IsNullOrEmpty(_levelsScenePaths[idx]))
                return SceneNameFromPath(_levelsScenePaths[idx]);

            if (LevelsScene != null && idx >= 0 && idx < LevelsScene.Length && LevelsScene[idx] != null)
                return SceneNameFromPath(AssetDatabase.GetAssetPath(LevelsScene[idx]));

            return string.Empty;
#else
            if (_levelsScenePaths == null || idx < 0 || idx >= _levelsScenePaths.Length) return string.Empty;
            return SceneNameFromPath(_levelsScenePaths[idx]);
#endif
        }

        private string GetCustomLevelSceneName() => SceneNameFromPath(_customLevelScenePath);

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncScenePathsFromAssets();

            // Optional warnings if not in Build Settings
            void WarnIfNotInBuild(string path, string label)
            {
                if (string.IsNullOrEmpty(path)) return;
                bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == path && s.enabled);
                if (!inBuild) Debug.LogWarning($"[UniversalGameManager] Scene not in Build Settings: {label} → {SceneNameFromPath(path)}");
            }

            WarnIfNotInBuild(_mainMenuPath, nameof(MainMenu));
            if (_levelsScenePaths != null)
                for (int i = 0; i < _levelsScenePaths.Length; i++)
                    WarnIfNotInBuild(_levelsScenePaths[i], $"{nameof(LevelsScene)}[{i}]");
            if (!string.IsNullOrEmpty(_customLevelScenePath))
                WarnIfNotInBuild(_customLevelScenePath, nameof(CustomLevelScene));
        }

        private void SyncScenePathsFromAssets()
        {
            // MainMenu
            if (MainMenu != null)
            {
                var p = AssetDatabase.GetAssetPath(MainMenu);
                if (!string.IsNullOrEmpty(p) && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    _mainMenuPath = p;
            }

            // LevelsScene
            if (LevelsScene != null)
            {
                if (_levelsScenePaths == null || _levelsScenePaths.Length != LevelsScene.Length)
                    _levelsScenePaths = new string[LevelsScene.Length];

                for (int i = 0; i < LevelsScene.Length; i++)
                {
                    var sa = LevelsScene[i];
                    if (sa == null) { _levelsScenePaths[i] = string.Empty; continue; }
                    var p = AssetDatabase.GetAssetPath(sa);
                    _levelsScenePaths[i] = (!string.IsNullOrEmpty(p) && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) ? p : string.Empty;
                }
            }
            else
            {
                _levelsScenePaths = null;
            }

            // CustomLevelScene
            if (CustomLevelScene != null)
            {
                var p = AssetDatabase.GetAssetPath(CustomLevelScene);
                if (!string.IsNullOrEmpty(p) && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    _customLevelScenePath = p;
            }
        }
#endif
        #endregion
    }
}
