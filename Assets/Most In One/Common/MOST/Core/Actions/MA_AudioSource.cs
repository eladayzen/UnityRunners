using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_AudioSource : MOST_ActionCore
    {
        public MOSTAction_AudioSource() { ActionName = "Audio Source"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("If PlayAction called and the audio still playing, create a new audio source and play" +
            "\nallowing multipl sources, instead restart the audio source")]
        public bool OverrideOnReplay;

        [Tooltip("don't destroy the audio object\nuseful for background music and spammed audio sources")]
        public bool DontDestroyOnLoad;

        [Line]
        [Required, Tooltip("Audio clip to play on this source.")]
        public AudioClip AudioClip;

        [Tooltip("AudioMixerGroup that receives this AudioSource's output (routing).")]
        public AudioMixerGroup OutputAudioMixerGroup;

        [Tooltip("Mutes this AudioSource.")]
        public bool Mute = false;

        [Tooltip("Bypass effect filters attached to this AudioSource (e.g., Low/High Pass, Reverb Filter) on the same GameObject.")]
        public bool BypassEffects = false;

        [Tooltip("Bypass effects applied by the AudioListener (global/listener-level effects).")]
        public bool BypassListenerEffects = false;

        [Tooltip("Ignore Reverb Zones; do not send signal to them.")]
        public bool BypassReverbZones = false;

        [Tooltip("If enabled, the clip restarts when it reaches the end.")]
        public bool Loop = false;

        [Line]
        [Range(0, 256), Tooltip("Playback priority (0 = highest, 256 = lowest). Lower numbers are kept when audio voices are limited.")]
        public int priority = 128;

        [Range(0f, 1f), Tooltip("Output volume of the AudioSource.")]
        public float Volume = 1f;

        [Range(-3f, 3f), Tooltip("Pitch multiplier. >1 plays faster/higher; <1 slower/lower.")]
        public float Pitch = 1f;

        [Range(-1f, 1f), Tooltip("Stereo pan (-1 = left, 1 = right). Primarily affects 2D playback (Spatial Blend = 0).")]
        public float PanStereo = 0f;

        [Range(0f, 1.1f), Tooltip("Blend between 2D and 3D spatialization (0 = fully 2D, 1 = fully 3D).")]
        public float SpatialBlend = 0f;

        [Range(0f, 1.1f), Tooltip("Amount of signal routed to Reverb Zones (0 = none, 1 = full, up to 1.1 to boost).")]
        public float ReverbZoneMix = 1f;

        [Line]
        [Range(0f, 5f), Tooltip("Doppler effect level (0 disables Doppler).")]
        public float DopplerLevel = 1f;

        [Range(0, 360), Tooltip("Spread of a 3D sound in speaker space (degrees). Higher values widen the source.")]
        public int Spread = 0;

        [Tooltip("How volume falls off with distance: Logarithmic (natural), Linear, or Custom (uses CustomRolloffCurve).")]
        public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;

        [Tooltip("Distance (world units) at which the AudioSource starts attenuating. Inside this range the volume is at maximum.")]
        public float MinDistance = 1f;

        [Tooltip("Distance (world units) where the sound is fully attenuated for Log/Linear modes. For Custom, used as the curve's max distance.")]
        public float MaxDistance = 500f;

        [Line]
        [Tooltip("Custom volume-versus-distance curve used when RolloffMode is Custom. X = normalized distance (Min→Max), Y = volume.")]
        public AnimationCurve CustomRolloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("Custom spread-versus-distance curve. X = normalized distance (Min→Max), Y = spread (0..360).")]
        public AnimationCurve SpreadCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Custom Reverb Zone Mix-versus-distance curve. X = normalized distance (Min→Max), Y = mix (0..1.1).")]
        public AnimationCurve ReverbZoneMixCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        List<AudioSource> _audioHandler = new();


        public override void OnAwake() // As Awake function
        {
            base.OnAwake();
        }

        public override void OnValidate() // Called On Each inspector vaildation as Validate function
        {
#if UNITY_EDITOR
            base.OnValidate();
#endif
        }

        public override void OnLateUpdate() // Called each frame as LateUpdate function
        {
            base.OnLateUpdate();
        }

        public override void OnUpdate() // Called each frame as Update function
        {
            for (int i = _audioHandler.Count - 1; i >= 0; i--)
            {
                if (!_audioHandler[i].loop && (_audioHandler[i] == null || !_audioHandler[i].isPlaying))
                {
                    if (_audioHandler[i] != null) UnityEngine.Object.Destroy(_audioHandler[i].gameObject);
                    _audioHandler.RemoveAt(i);
                    if (_audioHandler.Count == 0) IsPlaying = false;
                }
            }
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (AudioClip == null)
            {
                Debug.LogWarning("No audio clip assigned to AudioSourcePlayer");
                return;
            }
            if (DontDestroyOnLoad && GameObject.Find($"AudioInstance_{AudioClip.name}")) return;
            if (OverrideOnReplay || !IsPlaying)
            {
                GameObject audioObject = new($"AudioInstance_{AudioClip.name}");
                audioObject.transform.position = (TargetObject == null ? Owner.BaseTarget : TargetObject).transform.position;
                if (DontDestroyOnLoad) UnityEngine.Object.DontDestroyOnLoad(audioObject);

                AudioSource audioSource = CreateAudioSource(audioObject);
                audioSource.Play();
                _audioHandler.Add(audioSource);
                IsPlaying = true;
            }
            else
            {
                _audioHandler[0].GetComponent<AudioSource>().Play();
            }
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            if (DontDestroyOnLoad) return;
            try
            {
                for (int i = _audioHandler.Count - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_audioHandler[i].gameObject);
            }
            catch { /* Unity may be destroying objects (scene change/removal), this will make a conflict. Swallow to avoid errors. */ }
            _audioHandler.Clear();
            IsPlaying = false;
        }

        // Method to copy properties from an existing AudioSource
        public void CopyFromAudioSource(AudioSource source)
        {
            if (source == null) return;

            AudioClip = source.clip;
            OutputAudioMixerGroup = source.outputAudioMixerGroup;
            Mute = source.mute;
            BypassEffects = source.bypassEffects;
            BypassListenerEffects = source.bypassListenerEffects;
            BypassReverbZones = source.bypassReverbZones;
            PlayOnAwake = source.playOnAwake;
            Loop = source.loop;
            Volume = source.volume;
            Pitch = source.pitch;
            PanStereo = source.panStereo;
            SpatialBlend = source.spatialBlend;
            ReverbZoneMix = source.reverbZoneMix;
            DopplerLevel = source.dopplerLevel;
            Spread = (int)source.spread;
            RolloffMode = source.rolloffMode;
            MinDistance = source.minDistance;
            MaxDistance = source.maxDistance;
            priority = source.priority;

            // Copy custom curves
            CustomRolloffCurve = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            SpreadCurve = source.GetCustomCurve(AudioSourceCurveType.Spread);
            ReverbZoneMixCurve = source.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix);
        }

        // Method to apply these properties to an AudioSource
        public void ApplyToAudioSource(AudioSource target)
        {
            if (target == null) return;

            target.clip = AudioClip;
            target.outputAudioMixerGroup = OutputAudioMixerGroup;
            target.mute = Mute;
            target.bypassEffects = BypassEffects;
            target.bypassListenerEffects = BypassListenerEffects;
            target.bypassReverbZones = BypassReverbZones;
            target.playOnAwake = PlayOnAwake;
            target.loop = Loop;
            target.volume = Volume;
            target.pitch = Pitch;
            target.panStereo = PanStereo;
            target.spatialBlend = SpatialBlend;
            target.reverbZoneMix = ReverbZoneMix;
            target.dopplerLevel = DopplerLevel;
            target.spread = Spread;
            target.rolloffMode = RolloffMode;
            target.minDistance = MinDistance;
            target.maxDistance = MaxDistance;
            target.priority = priority;

            // Apply custom curves
            target.SetCustomCurve(AudioSourceCurveType.CustomRolloff, CustomRolloffCurve);
            target.SetCustomCurve(AudioSourceCurveType.Spread, SpreadCurve);
            target.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, ReverbZoneMixCurve);
        }

        // Create a new AudioSource with these properties on a GameObject
        public AudioSource CreateAudioSource(GameObject targetGameObject)
        {
            if (targetGameObject == null) return null;

            AudioSource newSource = targetGameObject.AddComponent<AudioSource>();
            ApplyToAudioSource(newSource);
            return newSource;
        }

        // Clone method
        public MOSTAction_AudioSource Clone()
        {
            return new MOSTAction_AudioSource
            {
                AudioClip = AudioClip,
                OutputAudioMixerGroup = OutputAudioMixerGroup,
                Mute = Mute,
                BypassEffects = BypassEffects,
                BypassListenerEffects = BypassListenerEffects,
                BypassReverbZones = BypassReverbZones,
                PlayOnAwake = PlayOnAwake,
                Loop = Loop,
                Volume = Volume,
                Pitch = Pitch,
                PanStereo = PanStereo,
                SpatialBlend = SpatialBlend,
                ReverbZoneMix = ReverbZoneMix,
                DopplerLevel = DopplerLevel,
                Spread = Spread,
                RolloffMode = RolloffMode,
                MinDistance = MinDistance,
                MaxDistance = MaxDistance,
                priority = priority,
                CustomRolloffCurve = new AnimationCurve(CustomRolloffCurve.keys),
                SpreadCurve = new AnimationCurve(SpreadCurve.keys),
                ReverbZoneMixCurve = new AnimationCurve(ReverbZoneMixCurve.keys)
            };
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}