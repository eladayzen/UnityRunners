using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_GrayShift : MOST_ActionCore
    {
        public MOSTAction_GrayShift() { ActionName = "Gray Shift"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        [Tooltip("If enabled, affect all child Renderers under the target. If disabled, only the target's own Renderer is used.")]
        public bool TargetAllChildren = true;

        [Line]
        [Tooltip("Optional material template to use for the grayscale copies. If unset, each original material is cloned and tinted.\n"
               + "Tip: Provide a simple unlit/standard template here to keep the effect consistent across different source materials.")]
        [InnerHint("(Optional)")] public Material UsedMaterial;

        [Tooltip("Tint strength for the gray copy. 1 keeps original color; 0 produces black (use 0..1).")]
        [Range(0, 1)] public float GrayShiftScale = .5f;

        GameObject _target;
        List<Renderer> _childRenderers = new();
        List<List<Material>> _GrayMaterials = new();
        List<List<Material>> _originalMaterials = new();

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
            base.OnUpdate();
        }

        protected override void Play() // This function called to start the action // Use PlayAction to call it
        {
            if (!Enabled) return;
            Stop();

            // Resolve target
            _target = TargetObject == null ? Owner.BaseTarget : TargetObject;

            // Collect renderers
            if (TargetAllChildren)
                _childRenderers = _target.GetComponentsInChildren<Renderer>(includeInactive: false).ToList();
            else
                _childRenderers.Add(_target.GetComponent<Renderer>());

            // Remove renderers that don't actually render meshes (exclude TextMesh / TextMeshPro, etc.)
            _childRenderers.RemoveAll(item =>
                (
                    item.GetComponent<SkinnedMeshRenderer>() == null &&
                    item.GetComponent<MeshRenderer>() == null
                )
                || item.GetComponent<TextMesh>() != null
                || item.GetComponent<TextMeshPro>() != null
                || item.GetComponent<TextMeshProUGUI>() != null
            );

            for (int i = 0; i < _childRenderers.Count; i++)
            {
                _originalMaterials.Add(_childRenderers[i].materials.ToList());
                _GrayMaterials.Add(new List<Material>());

                foreach (Material m in _childRenderers[i].materials)
                {
                    // Only materials with _Color can be tinted safely; others are skipped.
                    if (m != null && m.HasProperty("_Color"))
                    {
                        var template = UsedMaterial != null ? UsedMaterial : m;
                        var newM = new Material(template)
                        {
                            color = m.color * GrayShiftScale
                        };
                        _GrayMaterials[i].Add(newM);
                    }
                    else
                    {
                        // Keep original if we can't tint (preserve array layout)
                        _GrayMaterials[i].Add(m);
                    }
                }

                _childRenderers[i].materials = _GrayMaterials[i].ToArray();
            }

            IsPlaying = true;
        }

        protected override void Stop() // This function Stops the action // Use StopAction to call it
        {
            try
            {
                // Restore originals
                for (int i = 0; i < _childRenderers.Count; i++)
                {
                    if (_childRenderers[i] != null && i < _originalMaterials.Count)
                        _childRenderers[i].materials = _originalMaterials[i].ToArray();
                }

                // Destroy the gray instances we created
                if (_GrayMaterials != null)
                {
                    for (int i = 0; i < _GrayMaterials.Count; i++)
                    {
                        foreach (Material mat in _GrayMaterials[i])
                            if (mat != null && (UsedMaterial == null || mat.shader == UsedMaterial.shader)) // best-effort guard
                                UnityEngine.Object.Destroy(mat);
                    }

                    // Clear all data
                    _childRenderers.Clear();
                    _GrayMaterials.Clear();
                    _originalMaterials.Clear();
                }
            }
            catch { /* Unity may be destroying objects (scene change/removal), this will make a conflict. Swallow to avoid errors. */ }
            IsPlaying = false;
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
            Stop();
        }
    }
}
