using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_HDRGlow : MOST_ActionCore
    {
        public MOSTAction_HDRGlow() { ActionName = "HDR Glow"; }

        [Line]
        [HelpBox("This effect uses Emission to glow, the target material must support _EMISSION key (enabled or disabled)\n" +
            "if the effect not glowing, enable HDR on graphic settings")]
        [Tooltip("Override The Base target, if you want to target a dfferent object")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("When enabled, this action will target all objects under the Target object")]
        public bool TargetAllChildren = true;

        [Line]
        [ColorUsage(true, true), Tooltip("Glow Color, Using Material Emission")]
        public Color GlowColor = new(1f, 0.8f, 0.2f, 1f);

        [Tooltip("HDR Intensity (Emission)")]
        public float GlowIntensity = 2f;

        [Min(0), Tooltip("Action/Glow Time")]
        public float GlowDuration = 0.8f;

        [Tooltip("Controls GlowIntensity Overtime\n1 = Max GlowIntensity")]
        public AnimationCurve GlowPulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        GameObject _target;
        List<Renderer> _childRenderers = new();
        List<List<Material>> _originalMaterials = new();
        List<List<Material>> _glowingMaterials = new();
        List<List<Color>> _originalColors = new();
        float _glowTimer = 0f;

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
            if (!Enabled) return;
            if (IsPlaying)
            {
                _glowTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_glowTimer / GlowDuration);
                float pulseValue = GlowPulseCurve.Evaluate(progress);

                for (int i = 0; i < _childRenderers.Count; i++)
                {
                    for (int j = 0; j < _glowingMaterials[i].Count; j++)
                    {
                        if (_glowingMaterials[i][j].HasProperty("_Color"))
                            _glowingMaterials[i][j].color = Color.Lerp(_originalColors[i][j], GlowColor, pulseValue);

                        if (_glowingMaterials[i][j].HasProperty("_EmissionColor"))
                            _glowingMaterials[i][j].SetColor("_EmissionColor", GlowColor * (pulseValue * GlowIntensity));
                    }
                }
                if (_glowTimer >= GlowDuration) Stop();
            }
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (!Enabled) return;
            if (!IsPlaying)
            {
                // Override the target object
                _target = TargetObject == null ? Owner.BaseTarget : TargetObject;

                // Get all target renderer and store on a list
                if (TargetAllChildren) _childRenderers = _target.GetComponentsInChildren<Renderer>().ToList();
                else _childRenderers.Add(_target.GetComponent<Renderer>());

                // clear all renderer that doesn't contains a mesh, like TMP
                _childRenderers.RemoveAll(item => item.GetComponent<SkinnedMeshRenderer>() == null && item.GetComponent<MeshRenderer>() == null ||
                item.gameObject == null || item.GetComponent<TextMesh>() != null || item.GetComponent<TextMeshPro>() != null);

                for (int i = 0; i < _childRenderers.Count; i++)
                {
                    _originalMaterials.Add(_childRenderers[i].materials.ToList());
                    _originalColors.Add(new List<Color>());
                    _glowingMaterials.Add(new List<Material>());

                    foreach (Material m in _childRenderers[i].materials)
                    {
                        Material newM = new(m);
                        if (newM.HasProperty("_EmissionColor")) newM.EnableKeyword("_EMISSION");
                        if (newM.HasProperty("_Color")) _originalColors[i].Add(newM.color);
                        _glowingMaterials[i].Add(newM);
                    }
                    _childRenderers[i].materials = _glowingMaterials[i].ToArray();
                }
                IsPlaying = true;
                _glowTimer = 0f;
            }
            else _glowTimer = 0f;
            OnUpdate(); // to start the effect on the frame that the action called in instead of the next frame
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            IsPlaying = false;
            for (int i = 0; i < _childRenderers.Count; i++)
                if(_childRenderers[i] != null) _childRenderers[i].materials = _originalMaterials[i].ToArray();

            try
            {
                if (_glowingMaterials != null)
                {
                    for (int i = 0; i < _glowingMaterials.Count; i++)
                    {
                        foreach (Material mat in _glowingMaterials[i])
                            if (mat != null) UnityEngine.Object.Destroy(mat);
                    }
                }
            }
            catch { /* Unity may be destroying objects (scene change/removal), this will make a conflict. Swallow to avoid errors. */ }

            // Clear all data
            _childRenderers.Clear();
            _glowingMaterials.Clear();
            _originalColors.Clear();
            _originalMaterials.Clear();
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}
