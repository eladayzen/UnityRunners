using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;

namespace Solo.MOST_IN_ONE
{
    [CreateAssetMenu(fileName = "M_AudioManager", menuName = "MOST/Audio Manager"), HideScriptField]
    public class MOST_AudioManager : ScriptableObject
    {
        [BigHeader("Audio Mixer Data")]
        [GUIColor("yellow"), Tooltip("Main AudioMixer that holds the exposed volume parameters. Safe to leave null: Apply() will no-op if unset.")]
        public AudioMixer Mixer;

        // ---------- Mixer Groups (optional) ----------
        [Header("Mixer Groups")]
        [InnerHint("(Optional)")]
        [Tooltip("Master output group reference for routing/organization by your playback controller. Safe to leave null.")]
        public AudioMixerGroup MasterGroup;

        [InnerHint("(Optional)")]
        [Tooltip("Music output group reference (for music sources). Safe to leave null.")]
        public AudioMixerGroup MusicGroup;

        [InnerHint("(Optional)")]
        [Tooltip("SFX output group reference (for sound effects). Safe to leave null.")]
        public AudioMixerGroup SFXGroup;

        [InnerHint("(Optional)")]
        [Tooltip("UI output group reference (for UI clicks/notifications). Safe to leave null.")]
        public AudioMixerGroup UIGroup;

        // ---------- Exposed Parameter Names ----------
        [Header("Exposed Parameter Names")]
        [Tooltip("Exposed name of the Master group's Volume parameter in the Mixer (case-sensitive). Create in Audio Mixer via: right-click Volume → Expose, then rename here.")]
        public string MasterVolumeParam = "MasterVolume";

        [Tooltip("Exposed name of the Music group's Volume parameter in the Mixer (case-sensitive).")]
        public string MusicVolumeParam = "MusicVolume";

        [Tooltip("Exposed name of the SFX group's Volume parameter in the Mixer (case-sensitive).")]
        public string SfxVolumeParam = "SFXVolume";

        [Tooltip("Exposed name of the UI group's Volume parameter in the Mixer (case-sensitive).")]
        public string UIVolumeParam = "UIVolume";

        // ---------- Current State ----------
        [BigHeader("Current State")]
        [Header("Master"), ReadOnlyIf(nameof(autoSaveInEditor), false), Range(0f, 1f)]
        [Tooltip("Master volume (0–1). Applied as dB to MasterVolumeParam (0 → −80 dB, 1 → 0 dB).")]
        public float MasterVolume = 1f;

        [ReadOnlyIf(nameof(autoSaveInEditor), false)]
        [Tooltip("Master mute. When off, MasterVolumeParam is forced to −80 dB regardless of slider.")]
        public bool Master_IsOn = true;

        [Header("Music"), ReadOnlyIf(nameof(autoSaveInEditor), false), Range(0f, 1f)]
        [Tooltip("Music volume (0–1). Applied to MusicVolumeParam.")]
        public float MusicVolume = 1f;

        [ReadOnlyIf(nameof(autoSaveInEditor), false)]
        [Tooltip("Music mute. When off, MusicVolumeParam is forced to −80 dB.")]
        public bool Music_IsOn = true;

        [Header("SFX"), ReadOnlyIf(nameof(autoSaveInEditor), false), Range(0f, 1f)]
        [Tooltip("SFX volume (0–1). Applied to SfxVolumeParam.")]
        public float SFXVolume = 1f;

        [ReadOnlyIf(nameof(autoSaveInEditor), false)]
        [Tooltip("SFX mute. When off, SfxVolumeParam is forced to −80 dB.")]
        public bool SFX_IsOn = true;

        [Header("UI"), ReadOnlyIf(nameof(autoSaveInEditor), false), Range(0f, 1f)]
        [Tooltip("UI volume (0–1). Applied to UIVolumeParam.")]
        public float UIVolume = 1f;

        [ReadOnlyIf(nameof(autoSaveInEditor), false)]
        [Tooltip("UI mute. When off, UIVolumeParam is forced to −80 dB.")]
        public bool UI_IsOn = true;

        // ---------- Persistence, Save/Load ----------
        [BigHeader("Persistence, Save/Load")]
        [Tooltip("On entering Play Mode, try to load JSON from Application.persistentDataPath; if missing, current inspector values are used.")]
        [SerializeField] bool autoLoadOnEnable = true;

        [Tooltip("Automatically write JSON after SetVolume / SetEnabled / ApplyAll / RestoreState during Play Mode.")]
        [SerializeField] bool autoSaveOnChange = true;

        [Tooltip("Also auto-save when editing values in the Inspector (Edit Mode). Leave OFF if you want the fields to remain read-only via ReadOnlyIf.")]
        [SerializeField] bool autoSaveInEditor = false;

        [Tooltip("Unique key for file name. If empty, the asset name is used.")]
        [SerializeField] string databaseKey = "audio_state.json";

        public enum Channel { Master, Music, SFX, UI }
        const float MuteDb = -80f;

        void OnEnable()
        {
            if (autoLoadOnEnable && Application.isPlaying)
            {
                LoadFromFile(applyNow: false);   // push loaded values to mixer and ApplyAll
                AutoSaveIfNeeded(force: true);   // ensure file exists/normalized, if not... create a new one
            }
        }

        void OnValidate()
        {
            if (!Application.isPlaying && autoSaveInEditor) // mirror inspector edits to mixer if autoSaveInEditor
            {
                ApplyAll();
                SaveToFile();
            }
        }

        // ---------- Public controls ----------
        public void SetVolume(Channel ch, float slider01)
        {
            slider01 = Mathf.Clamp01(slider01);
            switch (ch)
            {
                case Channel.Master: MasterVolume = slider01; break;
                case Channel.Music: MusicVolume = slider01; break;
                case Channel.SFX: SFXVolume = slider01; break;
                case Channel.UI: UIVolume = slider01; break;
            }
            Apply(ch);
            AutoSaveIfNeeded();
        }

        public void SetEnabled(Channel ch, bool on)
        {
            switch (ch)
            {
                case Channel.Master: Master_IsOn = on; break;
                case Channel.Music: Music_IsOn = on; break;
                case Channel.SFX: SFX_IsOn = on; break;
                case Channel.UI: UI_IsOn = on; break;
            }
            Apply(ch);
            AutoSaveIfNeeded();
        }

        // Apply every channel to the mixer (safe if mixer/params are missing)
        public void ApplyAll()
        {
            Apply(Channel.Master);
            Apply(Channel.Music);
            Apply(Channel.SFX);
            Apply(Channel.UI);
        }

        public void Apply(Channel ch)
        {
            if (Mixer == null) return; // mixer optional
            string p = ParamName(ch);
            if (string.IsNullOrEmpty(p)) return;

            float vol = GetVolume01(ch);
            bool on = GetEnabled(ch);
            float db = on ? LinearToDb(vol) : MuteDb;
            Mixer.SetFloat(p, db);
        }

        // Optional: provide your other manager an easy way to fetch groups (may be null)
        public AudioMixerGroup GetGroup(Channel ch)
        {
            return ch switch
            {
                Channel.Master => MasterGroup,
                Channel.Music => MusicGroup,
                Channel.SFX => SFXGroup,
                Channel.UI => UIGroup,
                _ => null,
            };
        }

        public string Serialize()
        {
            // Keep persistence lean—just floats/bools (no UnityEngine.Object refs)
            var p = new Persist
            {
                mv = MasterVolume,
                mOn = Master_IsOn,
                uv = UIVolume,
                uOn = UI_IsOn,
                xv = SFXVolume,
                xOn = SFX_IsOn,
                vv = MusicVolume,
                vOn = Music_IsOn
            };
            return JsonUtility.ToJson(p);
        }

        public bool Deserialize(string json, bool applyNow = true)
        {
            try
            {
                var p = JsonUtility.FromJson<Persist>(json);
                if (p == null) return false;

                MasterVolume = Mathf.Clamp01(p.mv); Master_IsOn = p.mOn;
                UIVolume = Mathf.Clamp01(p.uv); UI_IsOn = p.uOn;
                SFXVolume = Mathf.Clamp01(p.xv); SFX_IsOn = p.xOn;
                MusicVolume = Mathf.Clamp01(p.vv); Music_IsOn = p.vOn;

                if (applyNow) ApplyAll();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioManagerSO] Deserialize failed: {e.Message}");
                return false;
            }
        }

        public bool SaveToFile(string path = null)
        {
            try
            {
                var p = string.IsNullOrEmpty(path)
                    ? Path.Combine(Application.persistentDataPath, databaseKey)
                    : path;
                File.WriteAllText(p, Serialize());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioManagerSO] SaveToFile failed: {e.Message}");
                return false;
            }
        }

        public bool LoadFromFile(string path = null, bool applyNow = true)
        {
            try
            {
                var p = string.IsNullOrEmpty(path)
                    ? Path.Combine(Application.persistentDataPath, databaseKey)
                    : path;
                if (!File.Exists(p)) return false;
                var json = File.ReadAllText(p);
                return Deserialize(json, applyNow);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioManagerSO] LoadFromFile failed: {e.Message}");
                return false;
            }
        }

        // ---------- Internals ----------
        void AutoSaveIfNeeded(bool force = false)
        {
            if (force || (autoSaveOnChange && (Application.isPlaying || autoSaveInEditor)))
                SaveToFile();
        }

        float GetVolume01(Channel ch)
        {
            return ch switch
            {
                Channel.Master => MasterVolume,
                Channel.Music => MusicVolume,
                Channel.SFX => SFXVolume,
                Channel.UI => UIVolume,
                _ => 1f,
            };
        }

        bool GetEnabled(Channel ch)
        {
            return ch switch
            {
                Channel.Master => Master_IsOn,
                Channel.Music => Music_IsOn,
                Channel.SFX => SFX_IsOn,
                Channel.UI => UI_IsOn,
                _ => true,
            };
        }

        string ParamName(Channel ch)
        {
            return ch switch
            {
                Channel.Master => MasterVolumeParam,
                Channel.Music => MusicVolumeParam,
                Channel.SFX => SfxVolumeParam,
                Channel.UI => UIVolumeParam,
                _ => null,
            };
        }

        public static float LinearToDb(float v) => Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f;
        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

        [Serializable]
        class Persist // Minimal DTO for JSON (private, not used elsewhere)
        {
            public float mv, uv, xv, vv;   // master/ui/sfx/music volumes
            public bool mOn, uOn, xOn, vOn;
        }
    }
}