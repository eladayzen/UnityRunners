using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Solo.MOST_IN_ONE
{
    [CreateAssetMenu(fileName = "M_Database", menuName = "MOST/Database")]
    public class MOST_Database : ScriptableObject
    {
        public enum DataTypes { Int, Float, Bool, String }
        public enum OnExistsPolicy { IgnoreIfExists, OverrideIfExists, NoCondition }

        [HelpBox("MOST_Database stores all game keys and values like currencies, level, scores (current/best), IAP, etc.\nSee docs for usage.")]
        [Tooltip("Unique key for file names. If empty, the asset name is used.")]
        [SerializeField, GUIColor("yellow")] string databaseKey = "database-Keys";

        [SerializeField, Tooltip("If enabled, values & schema are stored in PlayerPrefs instead of files.\nRecommended for (NO real Arbitrary File Path) or browser sandboxs platforms like WebGL")]
        bool UseInternalStorage = false;

        [SerializeField, ReadOnly, Tooltip("Whether this database has been initialized in this device profile.")] bool initialized = false;
        public bool Initialized => initialized;

        [SerializeReference] private List<MData> entries = new();
        public IReadOnlyList<MData> Entries => entries;

        public MData GetById(int id) =>
            entries?.FirstOrDefault(e => e != null && e.id == id);

        public MData GetByName(string name) =>
            entries?.FirstOrDefault(e => e != null && string.Equals(e.DataName, name, StringComparison.Ordinal));

        public T Get<T>(int id) where T : MData =>
            entries?.OfType<T>().FirstOrDefault(e => e.id == id);

        public T Get<T>(string name) where T : MData =>
            entries?.OfType<T>().FirstOrDefault(e => string.Equals(e.DataName, name, StringComparison.Ordinal));

        public bool TryGet<T>(int id, out T result) where T : MData
        {
            result = Get<T>(id);
            return result != null;
        }

        public string Key => string.IsNullOrWhiteSpace(databaseKey) ? name : databaseKey;

        static readonly HashSet<MOST_Database> s_loaded = new();
        static string BaseDir => Path.Combine(Application.persistentDataPath, "MOST");
        string SavePath => Path.Combine(BaseDir, $"{SanitizeKey(Key)}.json");
        string TmpPath => SavePath + ".tmp";

        string SchemaPath => Path.Combine(BaseDir, $"user_db_{SanitizeKey(Key)}.json");
        string SchemaTmp => SchemaPath + ".tmp";

        static string SanitizeKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "default";
            var arr = raw.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            return new string(arr);
        }

        string ValuesPrefsKey => $"MOST_{SanitizeKey(Key)}_VALUES";
        string SchemaPrefsKey => $"MOST_{SanitizeKey(Key)}_SCHEMA";

        bool HasValuesStore() => UseInternalStorage ? PlayerPrefs.HasKey(ValuesPrefsKey) : File.Exists(SavePath);
        bool HasSchemaStore() => UseInternalStorage ? PlayerPrefs.HasKey(SchemaPrefsKey) : File.Exists(SchemaPath);

        [Serializable]
        class SaveFile
        {
            public int version = 1;
            public List<I> ints = new();
            public List<F> floats = new();
            public List<S> strings = new();
            public List<B> bools = new();
        }
        [Serializable] class I { public int id; public int v; public int h; }
        [Serializable] class F { public int id; public float v; public float h; }
        [Serializable] class S { public int id; public string v; }
        [Serializable] class B { public int id; public bool v; }

        [Serializable]
        class UserSchema
        {
            public int version = 1;
            public List<UserInt> ints = new();
            public List<UserFloat> floats = new();
            public List<UserString> strings = new();
            public List<UserBool> bools = new();
        }
        [Serializable] class UserInt { public int id; public string name; public int start; public bool minE; public int min; public bool maxE; public int max; }
        [Serializable] class UserFloat { public int id; public string name; public float start; public bool minE; public float min; public bool maxE; public float max; }
        [Serializable] class UserString { public int id; public string name; public string start; }
        [Serializable] class UserBool { public int id; public string name; public bool start; }

        [NonSerialized] private bool _runtimeInitDone;
        [NonSerialized] public int _skipNextSaves = 0;
        [NonSerialized] bool _schemaUsed = false;
        [NonSerialized] bool _saveScheduled = false;
        [NonSerialized] float _saveAtTime = 0f;

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
            TryRuntimeInit();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying) return;
#endif
            if (!Application.isPlaying) return;

            Save();
            Application.quitting -= OnAppQuitting;
            s_loaded.Remove(this);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureUniqueIds();
            if (!Application.isPlaying)
                SaveSchema();
        }
#endif

        public void Save()
        {
            SaveToJson();
            SaveSchema();
        }

        public void MarkValuesDirtyDelayed(float delaySeconds = 1f)
        {
            _saveScheduled = true;
            _saveAtTime = Time.unscaledTime + delaySeconds;
        }

        public void TickDelayedSave(float now)
        {
            if (!_saveScheduled) return;
            if (now < _saveAtTime) return;
            _saveScheduled = false;
            Save();
        }

        void TryRuntimeInit()
        {
            if (!this) return;
            if (_runtimeInitDone) return;
            _runtimeInitDone = true;

            EnsureUniqueIds();
            s_loaded.Add(this);

            var schema = LoadSchema();
            if (schema != null)
            {
                _schemaUsed = true;
                MergeSchemaEntries(schema);
            }

            if (!HasValuesStore())
            {
                initialized = false;
                SeedFromInitialValues();
                SaveToJson();
            }
            else LoadFromJson();

            BindOwnership();

            Application.quitting -= OnAppQuitting;
            Application.quitting += OnAppQuitting;
        }

        void OnAppQuitting()
        {
            Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsForPlaySession() { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded.Clear();
            Application.quitting -= SaveAllLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookQuit() => Application.quitting += SaveAllLoaded;

        static void SaveAllLoaded()
        {
            foreach (var db in s_loaded)
            {
                if (!db) continue;
                db.Save();
            }
        }

        void SeedFromInitialValues()
        {
            foreach (var e in entries)
            {
                switch (e)
                {
                    case IntData i:
                        i.Value = i.HighestValue = i.Clamp(i.InitialValue);
                        break;
                    case FloatData f:
                        f.Value = f.HighestValue = f.Clamp(f.InitialValue);
                        break;
                    case StringData s:
                        s.Value = s.InitialValue;
                        break;
                    case BoolData b:
                        b.Value = b.InitialValue;
                        break;
                }
            }
        }

        public void EnsureUniqueIds()
        {
            var used = new HashSet<int>();
            int next = 0;
            foreach (var e in entries)
            {
                if (e == null) continue;

                if (e.id < 0 || used.Contains(e.id))
                {
                    while (used.Contains(next)) next++;
                    e.id = next;
                    used.Add(e.id);
                    next++;
                }
                else
                {
                    used.Add(e.id);
                    if (e.id >= next) next = e.id + 1;
                }
            }
        }

        int NextFreeId()
        {
            var used = new HashSet<int>(entries.Where(x => x != null).Select(x => x.id));
            int id = 0; while (used.Contains(id)) id++;
            return id;
        }

        void BindOwnership()
        {
            foreach (var e in entries)
            {
                if (e != null)
                    e._owner = this;
            }
        }

        public void SaveToJson()
        {
            if (_skipNextSaves > 0) { _skipNextSaves--; return; }
            EnsureUniqueIds();

            try
            {
                Directory.CreateDirectory(BaseDir);
                initialized = true;

                var save = new SaveFile();
                foreach (var e in entries)
                {
                    if (e == null) continue;

                    switch (e)
                    {
                        case IntData i:
                            save.ints.Add(new I { id = e.id, v = i.Value, h = i.HighestValue });
                            break;
                        case FloatData f:
                            save.floats.Add(new F { id = e.id, v = f.Value, h = f.HighestValue });
                            break;
                        case StringData s:
                            save.strings.Add(new S { id = e.id, v = s.Value ?? string.Empty });
                            break;
                        case BoolData b:
                            save.bools.Add(new B { id = e.id, v = b.Value });
                            break;
                    }
                }

                var json = JsonUtility.ToJson(save, false);

                if (UseInternalStorage)
                {
                    PlayerPrefs.SetString(ValuesPrefsKey, json);
                    PlayerPrefs.Save();
                }
                else
                {
                    Directory.CreateDirectory(BaseDir);
                    File.WriteAllText(TmpPath, json);
                    if (File.Exists(SavePath)) File.Replace(TmpPath, SavePath, null);
                    else File.Move(TmpPath, SavePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MOST] Save failed for '{Key}': {ex.Message}");
                try { if (File.Exists(TmpPath)) File.Delete(TmpPath); } catch { }
            }
        }

        public void LoadFromJson()
        {
            try
            {
                SeedFromInitialValues();

                string json = UseInternalStorage
                    ? PlayerPrefs.GetString(ValuesPrefsKey, string.Empty)
                    : File.ReadAllText(SavePath);

                var save = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveFile>(json);
                if (save == null) return;

                foreach (var it in save.ints)
                    if (GetById(it.id) is IntData i)
                    {
                        i.Value = it.v;
                        i.HighestValue = Mathf.Max(it.h, i.Value);
                    }

                foreach (var it in save.floats)
                    if (GetById(it.id) is FloatData f)
                    {
                        f.Value = it.v;
                        f.HighestValue = Mathf.Max(it.h, f.Value);
                    }

                foreach (var it in save.strings)
                    if (GetById(it.id) is StringData s)
                        s.Value = it.v ?? string.Empty;

                foreach (var it in save.bools)
                    if (GetById(it.id) is BoolData b)
                        b.Value = it.v;

                initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MOST] Load failed for '{Key}': {ex.Message}. Using InitialValue baseline.");
                initialized = false;
            }
        }

        UserSchema LoadSchema()
        {
            try
            {
                string json;
                if (UseInternalStorage)
                {
                    if (!PlayerPrefs.HasKey(SchemaPrefsKey)) return null;
                    json = PlayerPrefs.GetString(SchemaPrefsKey, string.Empty);
                }
                else
                {
                    if (!File.Exists(SchemaPath)) return null;
                    json = File.ReadAllText(SchemaPath);
                }

                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<UserSchema>(json);

            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MOST] Schema load failed for '{Key}': {ex.Message}");
                return null;
            }
        }

        public void SaveSchema() => SaveSchema(BuildSchemaFromDb());

        void SaveSchema(UserSchema s)
        {
            bool hasSchemaData = s.ints.Count > 0 || s.floats.Count > 0 || s.strings.Count > 0 || s.bools.Count > 0;
            bool schemaExists = HasSchemaStore();
            if (!schemaExists && !_schemaUsed && !hasSchemaData) return;

            try
            {
                var json = JsonUtility.ToJson(s, false);

                if (UseInternalStorage)
                {
                    PlayerPrefs.SetString(SchemaPrefsKey, json);
                    PlayerPrefs.Save();
                }
                else
                {
                    Directory.CreateDirectory(BaseDir);
                    File.WriteAllText(SchemaTmp, json);
                    if (File.Exists(SchemaPath)) File.Replace(SchemaTmp, SchemaPath, null);
                    else File.Move(SchemaTmp, SchemaPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MOST] Schema save failed for '{Key}': {ex.Message}");
                try { if (File.Exists(SchemaTmp)) File.Delete(SchemaTmp); } catch { }
            }
        }

        UserSchema BuildSchemaFromDb()
        {
            var s = new UserSchema();
            foreach (var e in entries)
            {
                switch (e)
                {
                    case IntData i:
                        s.ints.Add(new UserInt
                        {
                            id = i.id,
                            name = i.DataName,
                            start = i.InitialValue,
                            minE = i.EnableMin,
                            min = i.MinValue,
                            maxE = i.EnableMax,
                            max = i.MaxValue
                        });
                        break;
                    case FloatData f:
                        s.floats.Add(new UserFloat
                        {
                            id = f.id,
                            name = f.DataName,
                            start = f.InitialValue,
                            minE = f.EnableMin,
                            min = f.MinValue,
                            maxE = f.EnableMax,
                            max = f.MaxValue
                        });
                        break;
                    case StringData st:
                        s.strings.Add(new UserString { id = st.id, name = st.DataName, start = st.InitialValue });
                        break;
                    case BoolData b:
                        s.bools.Add(new UserBool { id = b.id, name = b.DataName, start = b.InitialValue });
                        break;
                }
            }
            return s;
        }

        void MergeSchemaEntries(UserSchema s)
        {
            foreach (var it in s.ints)
            {
                if (GetById(it.id) is IntData ex)
                {
                    ex.DataName = it.name;
                    ex.InitialValue = it.start;
                    ex.EnableMin = it.minE; ex.MinValue = it.min;
                    ex.EnableMax = it.maxE; ex.MaxValue = it.max;
                }
                else
                {
                    var e = new IntData
                    {
                        DataName = it.name,
                        InitialValue = it.start,
                        Value = it.start,
                        HighestValue = it.start,
                        EnableMin = it.minE,
                        MinValue = it.min,
                        EnableMax = it.maxE,
                        MaxValue = it.max,
                        id = it.id
                    };
                    e.Value = e.Clamp(e.Value);
                    entries.Add(e);
                }
            }

            foreach (var it in s.floats)
            {
                if (GetById(it.id) is FloatData ex)
                {
                    ex.DataName = it.name;
                    ex.InitialValue = it.start;
                    ex.EnableMin = it.minE; ex.MinValue = it.min;
                    ex.EnableMax = it.maxE; ex.MaxValue = it.max;
                }
                else
                {
                    var e = new FloatData
                    {
                        DataName = it.name,
                        InitialValue = it.start,
                        Value = it.start,
                        HighestValue = it.start,
                        EnableMin = it.minE,
                        MinValue = it.min,
                        EnableMax = it.maxE,
                        MaxValue = it.max,
                        id = it.id
                    };
                    e.Value = e.Clamp(e.Value);
                    entries.Add(e);
                }
            }

            foreach (var it in s.strings)
            {
                if (GetById(it.id) is StringData ex)
                {
                    ex.DataName = it.name;
                    ex.InitialValue = it.start;
                }
                else
                {
                    var e = new StringData
                    {
                        DataName = it.name,
                        InitialValue = it.start,
                        Value = it.start,
                        id = it.id
                    };
                    entries.Add(e);
                }
            }

            foreach (var it in s.bools)
            {
                if (GetById(it.id) is BoolData ex)
                {
                    ex.DataName = it.name;
                    ex.InitialValue = it.start;
                }
                else
                {
                    var e = new BoolData
                    {
                        DataName = it.name,
                        InitialValue = it.start,
                        Value = it.start,
                        id = it.id
                    };
                    entries.Add(e);
                }
            }

            EnsureUniqueIds();
            BindOwnership();
        }

        public bool CreateInt(string name, int start = 0, bool enableMin = false, int min = 0,
                              bool enableMax = false, int max = 0,
                              OnExistsPolicy policy = OnExistsPolicy.NoCondition)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var existing = Get<IntData>(name);
            if (existing != null)
            {
                if (policy == OnExistsPolicy.IgnoreIfExists) return false;
                if (policy == OnExistsPolicy.OverrideIfExists)
                {
                    existing.InitialValue = start;
                    existing.EnableMin = enableMin; existing.MinValue = min;
                    existing.EnableMax = enableMax; existing.MaxValue = max;
                    existing.Value = existing.Clamp(start);
                    existing.HighestValue = Math.Max(existing.HighestValue, existing.Value);
#if UNITY_EDITOR
                    if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
                    _schemaUsed = true;
                    BindOwnership();
                    Save();
                    return true;
                }
            }

            var e = new IntData
            {
                DataName = name,
                InitialValue = start,
                Value = start,
                HighestValue = start,
                EnableMin = enableMin,
                MinValue = min,
                EnableMax = enableMax,
                MaxValue = max,
                id = NextFreeId()
            };
            e.Value = e.Clamp(e.Value);
            entries.Add(e);
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
            _schemaUsed = true;
            BindOwnership();
            Save();
            return true;
        }

        public bool CreateFloat(string name, float start = 0f, bool enableMin = false, float min = 0f,
                                bool enableMax = false, float max = 0f,
                                OnExistsPolicy policy = OnExistsPolicy.NoCondition)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var existing = Get<FloatData>(name);
            if (existing != null)
            {
                if (policy == OnExistsPolicy.IgnoreIfExists) return false;
                if (policy == OnExistsPolicy.OverrideIfExists)
                {
                    existing.InitialValue = start;
                    existing.EnableMin = enableMin; existing.MinValue = min;
                    existing.EnableMax = enableMax; existing.MaxValue = max;
                    existing.Value = existing.Clamp(start);
                    existing.HighestValue = Mathf.Max(existing.HighestValue, existing.Value);
#if UNITY_EDITOR
                    if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
                    _schemaUsed = true;
                    BindOwnership();
                    Save();
                    return true;
                }
            }

            var e = new FloatData
            {
                DataName = name,
                InitialValue = start,
                Value = start,
                HighestValue = start,
                EnableMin = enableMin,
                MinValue = min,
                EnableMax = enableMax,
                MaxValue = max,
                id = NextFreeId()
            };
            e.Value = e.Clamp(e.Value);
            entries.Add(e);
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
            _schemaUsed = true;
            BindOwnership();
            Save();
            return true;
        }

        public bool CreateString(string name, string start = "",
                                 OnExistsPolicy policy = OnExistsPolicy.NoCondition)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var existing = Get<StringData>(name);
            if (existing != null)
            {
                if (policy == OnExistsPolicy.IgnoreIfExists) return false;
                if (policy == OnExistsPolicy.OverrideIfExists)
                {
                    existing.InitialValue = start;
                    existing.Value = start;
#if UNITY_EDITOR
                    if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
                    _schemaUsed = true;
                    BindOwnership();
                    Save();
                    return true;
                }
            }

            var e = new StringData { DataName = name, InitialValue = start, Value = start, id = NextFreeId() };
            entries.Add(e);
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
            _schemaUsed = true;
            BindOwnership();
            Save();
            return true;
        }

        public bool CreateBool(string name, bool start = false,
                               OnExistsPolicy policy = OnExistsPolicy.NoCondition)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var existing = Get<BoolData>(name);
            if (existing != null)
            {
                if (policy == OnExistsPolicy.IgnoreIfExists) return false;
                if (policy == OnExistsPolicy.OverrideIfExists)
                {
                    existing.InitialValue = start;
                    existing.Value = start;
#if UNITY_EDITOR
                    if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
                    _schemaUsed = true;
                    BindOwnership();
                    Save();
                    return true;
                }
            }

            var e = new BoolData { DataName = name, InitialValue = start, Value = start, id = NextFreeId() };
            entries.Add(e);
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
            _schemaUsed = true;
            BindOwnership();
            Save();
            return true;
        }

        public static IEnumerable<MOST_Database> GetLoadedDatabases() => s_loaded;

#if UNITY_EDITOR
        public void Editor_DeleteSaveFile()
        {
            try
            {
                if (UseInternalStorage)
                {
                    PlayerPrefs.DeleteKey(ValuesPrefsKey);
                    PlayerPrefs.Save();
                }
                else
                {
                    if (File.Exists(SavePath)) File.Delete(SavePath);
                }
                initialized = false;
            }
            catch (Exception ex) { Debug.LogWarning($"[MOST] Could not delete save for '{Key}': {ex.Message}"); }
        }

        [ContextMenu("Delete Save File (This DB)")]
        void DeleteSaveForThisDb()
        {
            try
            {
                if (UseInternalStorage)
                {
                    PlayerPrefs.DeleteKey(ValuesPrefsKey);
                    PlayerPrefs.Save();
                }
                else
                {
                    if (File.Exists(SavePath)) File.Delete(SavePath);
                }
                initialized = false;
                Debug.Log($"[MOST] Deleted values save for '{Key}'. Next init uses InitialValue.");
            }
            catch (Exception ex) { Debug.LogWarning($"[MOST] Could not delete save for '{Key}': {ex.Message}"); }
        }

        [ContextMenu("Clean Database Values")]
        void Editor_CleanDataValues()
        {
            if (!EditorUtility.DisplayDialog(
                "Clean Data (Values Only)",
                "This resets runtime values to ZERO (strings empty, bools false) without removing entries.\n" +
                "It also deletes the JSON values so the NEXT INIT uses InitialValue.\n\nProceed?",
                "Clean", "Cancel"))
                return;

            Undo.RecordObject(this, "Clean Data Values");

            foreach (var e in entries)
            {
                switch (e)
                {
                    case IntData i: i.Value = 0; i.HighestValue = 0; break;
                    case FloatData f: f.Value = 0f; f.HighestValue = 0f; break;
                    case StringData s: s.Value = string.Empty; break;
                    case BoolData b: b.Value = false; break;
                }
            }

            Editor_DeleteSaveFile();
            _skipNextSaves = 2;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MOST] Data values cleared for '{name}', values save deleted.");
        }

        [ContextMenu("Repair: Ensure Unique IDs")]
        void Editor_RepairIds()
        {
            Undo.RecordObject(this, "Repair IDs");
            EnsureUniqueIds();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif
    }

    [Serializable]
    public abstract class MData
    {
        [NonSerialized] internal MOST_Database _owner;

        [GUIColor("green"), Tooltip("Used to reference this data entry in code/UI.")]
        public string DataName;

        [Tooltip("Unique per entry (used for values persistence and disambiguating duplicate names).")]
        public int id = -1;

        public abstract object ValueAsObject { get; set; }
        public abstract object HighestValueAsObject { get; set; }
    }

    [Serializable]
    public class IntData : MData
    {
        [SerializeField, HideInInspector] int value;
        public int Value { get => value; set { this.value = value; _owner?.MarkValuesDirtyDelayed(); } }
        public override object ValueAsObject { get => Value; set { Value = Convert.ToInt32(value); } }

        public override object HighestValueAsObject { get => HighestValue; set => HighestValue = Convert.ToInt32(value); }

        [Tooltip("Seed used only on first run (or when this entry is newly created and has no save). Does not auto-reset existing saves.")]
        public int InitialValue = 0;

        [ReadOnly, Tooltip("Peak value ever reached. Increases only when Value exceeds the previous peak; never decreases and isn’t set by Initial Value.")]
        public int HighestValue = 0;

        public bool EnableMin = false;
        public int MinValue = 0;
        public bool EnableMax = false;
        public int MaxValue = 100;

        public int Clamp(int v)
        {
            if (EnableMin && v < MinValue) v = MinValue;
            if (EnableMax && v > MaxValue) v = MaxValue;
            return v;
        }

        public int Get() => Value;
        public int Set(int v) { Value = Clamp(v); if (Value > HighestValue) HighestValue = Value; return Value; }
        public int Increment() => Add(1);
        public int Decrement() => Subtract(1);
        public int Add(int d) { if (d < 0) return Subtract(-d); Value = Clamp(Value + d); if (Value > HighestValue) HighestValue = Value; return Value; }
        public int Subtract(int d) { if (d < 0) return Add(-d); Value = Clamp(Value - d);return Value; }
        public bool TrySubtract(int a) { if (a < 0) { Add(-a); return true; } int min = EnableMin ? MinValue : 0; int res = Value - a; if (res < min) return false; Value = Clamp(res); return true; }
        public bool SetIfGreater(int candidate) { int c = Clamp(candidate); if (c > Value) { Value = c; if (Value > HighestValue) HighestValue = Value;return true; } return false; }
        public void ClampMinZero() { if (Value < 0) Value = 0;  }
    }

    [Serializable]
    public class FloatData : MData
    {
        [SerializeField, HideInInspector] float value;
        public float Value { get => value; set { this.value = value; _owner?.MarkValuesDirtyDelayed(); } }
        public override object ValueAsObject { get => Value; set { Value = Convert.ToSingle(value);} }

        public override object HighestValueAsObject { get => HighestValue; set => HighestValue = Convert.ToSingle(value); }

        [Tooltip("Seed used only on first run (or when this entry is newly created and has no save). Does not auto-reset existing saves.")]
        public float InitialValue = 0;

        [ReadOnly, Tooltip("Peak value ever reached. Increases only when Value exceeds the previous peak; never decreases and isn’t set by Initial Value.")]
        public float HighestValue = 0;

        public bool EnableMin = false;
        public float MinValue = 0f;
        public bool EnableMax = false;
        public float MaxValue = 100f;

        const float EPS = 1e-6f;

        public float Clamp(float v)
        {
            if (EnableMin && v < MinValue) v = MinValue;
            if (EnableMax && v > MaxValue) v = MaxValue;
            return v;
        }

        public float Get() => Value;
        public float Set(float v) { Value = Clamp(v); if (Value > HighestValue + EPS) HighestValue = Value;  return Value; }
        public float Increment() => Add(1f);
        public float Decrement() => Subtract(1f);
        public float Add(float d) { if (d < 0f) return Subtract(-d); Value = Clamp(Value + d); if (Value > HighestValue + EPS) HighestValue = Value; return Value; }
        public float Subtract(float d) { if (d < 0f) return Add(-d); Value = Clamp(Value - d); return Value; }
        public bool TrySubtract(float a) { if (a < 0f) { Add(-a); return true; } float min = EnableMin ? MinValue : 0f; float res = Value - a; if (res < min - EPS) return false; Value = Clamp(res); return true; }
        public bool SetIfGreater(float candidate) { float c = Clamp(candidate); if (c > Value + EPS) { Value = c; if (Value > HighestValue + EPS) HighestValue = Value; return true; } return false; }
        public void ClampMinZero() { if (Value < 0f) Value = 0f; }
    }

    [Serializable]
    public class BoolData : MData
    {
        [SerializeField, ReadOnly, GUIColor("cyan"), Tooltip("Value\n“Current runtime flag used by gameplay. It’s saved to disk and restored on load." +
            " Toggling updates the game immediately.”\n\n" +
            "Initial Value\n“Seed used only on first run (or when this entry is newly created and has no save). Does not auto-reset existing saves.”")] bool value;
        public bool Value { get => value; set { this.value = Convert.ToBoolean(value); _owner?.MarkValuesDirtyDelayed(); } }
        public override object ValueAsObject { get => Value; set { Value = Convert.ToBoolean(value); } }
        public override object HighestValueAsObject { get => Value; set => Value = Convert.ToBoolean(value); }

        public bool InitialValue = false;

        public void ToggleValue() { Value = !Value; }
        public void SetBool(bool v) { Value = v; }
    }

    [Serializable]
    public class StringData : MData
    {
        [SerializeField, ReadOnly, GUIColor("cyan"), Tooltip("Value\n“Current runtime text used by gameplay/UI. It’s saved to disk and restored on load." +
            " Editing this updates the game immediately.”\n\n" +
            "Initial Value\n“Seed used only on first run (or when this entry is newly created and has no save). Does not auto-reset existing saves.”")] string value;
        public string Value { get => this.value; set { this.value = value.ToString(); _owner?.MarkValuesDirtyDelayed(); } }
        public string InitialValue;

        public override object ValueAsObject { get => value; set { this.value = value?.ToString() ?? ""; } }
        public override object HighestValueAsObject { get => Value; set => Value = value?.ToString() ?? ""; }
    }
}
