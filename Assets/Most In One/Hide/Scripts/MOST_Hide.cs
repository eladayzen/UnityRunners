using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    /// <summary>
    /// MOST_Hide
    /// Makes any geometry that blocks line-of-sight between Watcher and Watched become transparent
    /// until it no longer blocks. Two approaches:
    ///  - MaterialSwap: swap to a provided transparent material (predictable).
    ///  - PropertyBlock: override alpha on existing shaders (fast; requires color property).
    /// Restores originals and avoids per-frame allocations; supports MeshRenderer & SkinnedMeshRenderer.
    /// </summary>
    [HideScriptField]
    public class MOST_Hide : MonoBehaviour
    {
        public enum TargetAtWatchedObject { WorldTransform, CenterOfMesh, CenterOfMass }
        public enum TransparencyMode { MaterialSwap, PropertyBlock }

        // References & Targets
        [BigHeader("Targets")]
        [Tooltip("Start of the visibility check (ray/line cast). Usually camera or player viewpoint.")]
        [Required] public Transform Watcher;

        [Tooltip("End of the visibility check (ray/line cast). Usually the character to keep visible.")]
        [Required] public Transform Watched;

        [Tooltip("Layers considered occluders that become transparent when they block line of sight.")]
        public LayerMask OccluderLayers = ~0;

        [Tooltip("Point on the watched object to aim for.")]
        public TargetAtWatchedObject WatchedPointMode = TargetAtWatchedObject.WorldTransform;

        [Line]
        [Tooltip("Material used when Mode = MaterialSwap. Should support alpha blending.")]
        [Required, HideIfAny(nameof(Mode), TransparencyMode.PropertyBlock,true)]
        public Material TransparentSwapMaterial;

        [Tooltip("Also apply transparency to each hit object's children.")]
        public bool IncludeChildren = false;

        [Tooltip("Also include the hit object's PARENT and that parent's children (depth-limited).")]
        public bool IncludeParentAndChildren = false;

        [Tooltip("How many levels up from the hit object to include (0 = just immediate parent).")]
        [HideIfAny(nameof(IncludeParentAndChildren), true, false)]
        public int ParentIncludeDepth = 0;

        [Tooltip("How many levels of children to include beneath each included PARENT.")]
        [HideIfAll(nameof(IncludeParentAndChildren), true, false, nameof(IncludeChildren), true, false)]
        public int ChildrenIncludeDepth = 1;

        // Transparency Settings
        [BigHeader("Transparency")]
        [Tooltip("MaterialSwap (default) or PropertyBlock-based alpha override.")]
        public TransparencyMode Mode = TransparencyMode.MaterialSwap;

        [Tooltip("Target alpha for occluders (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)] public float TargetAlpha = 0.35f;

        [Tooltip("Multiply original alpha by TargetAlpha instead of setting it directly.")]
        public bool MultiplyOriginalAlpha = false;

        [Tooltip("Color property names to try when using PropertyBlock (checked in order).")]
        [HideIfAny(nameof(Mode), TransparencyMode.MaterialSwap, true)]
        public string[] ColorProperties = new[] { "_BaseColor", "_Color", "_TintColor" };

        [Tooltip("Main texture property names to try copying when swapping materials.")]
        [HideIfAny(nameof(Mode), TransparencyMode.PropertyBlock, true)]
        public string[] MainTextureProperties = new[] { "_BaseMap", "_MainTex" };

        [Tooltip("Copy tiling/offset for the main texture when swapping materials.")]
        [HideIfAny(nameof(Mode), TransparencyMode.PropertyBlock, true)]
        public bool CopyMainTextureTilingOffset = true;

        // Ray Casting Settings
        [BigHeader("Line Of Sight Extras")]
        [Tooltip("Use a small sphere cast instead of a thin ray (more forgiving geometry).")]
        public bool UseSphereCast = false;

        [Tooltip("Sphere cast radius when enabled.")]
        [HideIfAny(nameof(UseSphereCast), true, false)]
        [Min(0f)] public float SphereRadius = 0.1f;

        [Line]
        [Tooltip("Maximum hits returned per cast (NonAlloc buffer size).")]
        [Min(8)] public int MaxHitsPerCast = 128;

        // Internals (state & caches)
        readonly HashSet<Renderer> _prevOccluders = new();
        readonly HashSet<Renderer> _currOccluders = new();

        // MaterialSwap caches
        readonly Dictionary<Renderer, Material[]> _origMats = new();
        readonly Dictionary<Renderer, Material[]> _swapMats = new();

        MaterialPropertyBlock _mpb;
        RaycastHit[] _hitBuffer;

        void Awake()
        {
            _mpb ??= new MaterialPropertyBlock();
            if (MaxHitsPerCast < 8) MaxHitsPerCast = 128;
            _hitBuffer = new RaycastHit[MaxHitsPerCast];
        }

        void OnDisable() => RestoreAll();
        void OnDestroy() => RestoreAll();

        void Update()
        {
            if (!Watcher || !Watched) return;

            // 1) Determine target point on Watched
            Vector3 targetPoint = GetTargetPoint();

            // 2) Collect occluding GOs
            var occluderGOs = CollectOccluders(Watcher.position, targetPoint);

            // 3) Map to Renderers (MeshRenderer or SkinnedMeshRenderer)
            _currOccluders.Clear();
            foreach (var go in occluderGOs)
            {
                if (!go) continue;
                if (go.TryGetComponent<Renderer>(out var r)) _currOccluders.Add(r);
            }

            // 4) Diff: restore those no longer occluding; apply to new ones
            foreach (var r in _prevOccluders)
                if (!_currOccluders.Contains(r)) Restore(r);

            foreach (var r in _currOccluders)
                if (!_prevOccluders.Contains(r)) ApplyTransparent(r);

            // 5) Swap sets
            _prevOccluders.Clear();
            foreach (var r in _currOccluders) _prevOccluders.Add(r);
        }

        // Core helpers
        Vector3 GetTargetPoint()
        {
            switch (WatchedPointMode)
            {
                case TargetAtWatchedObject.CenterOfMesh:
                    if (Watched.TryGetComponent<Renderer>(out var rend))
                        return rend.bounds.center;
                    Debug.LogWarning("[MOST_Hide] Watched has no Renderer for CenterOfMesh; using Transform.position.");
                    return Watched.position;

                case TargetAtWatchedObject.CenterOfMass:
                    if (Watched.TryGetComponent<Rigidbody>(out var rb))
                        return rb.worldCenterOfMass; // correct world-space COM
                    Debug.LogWarning("[MOST_Hide] Watched has no Rigidbody for CenterOfMass; using Transform.position.");
                    return Watched.position;

                default:
                    return Watched.position;
            }
        }

        HashSet<GameObject> CollectOccluders(Vector3 start, Vector3 end)
        {
            var result = new HashSet<GameObject>();

            // main cast
            Cast(start, end, result);

            // hierarchy expansion
            if (IncludeParentAndChildren)
            {
                // For each hit object: include its parent chain (up to ParentIncludeDepth),
                // and for each included parent include its children (breadth up to ChildrenIncludeDepth).
                foreach (var go in result.ToArray())
                {
                    var p = go.transform.parent;
                    int depth = ParentIncludeDepth <= 0 ? 0 : ParentIncludeDepth; // 0 => immediate parent

                    for (int i = 0; p != null && i <= depth; i++)
                    {
                        result.Add(p.gameObject);
                        IncludeChildrenDepth(p.gameObject, Mathf.Max(1, ChildrenIncludeDepth), result);
                        p = p.parent;
                    }
                }
            }
            else if (IncludeChildren)
            {
                foreach (var go in result.ToArray())
                    IncludeChildrenDepth(go, 1, result); // one level
            }

            return result;
        }

        void Cast(Vector3 start, Vector3 end, HashSet<GameObject> collector)
        {
            Vector3 dir = end - start;
            float dist = dir.magnitude;
            if (dist <= 1e-6f) return;
            dir /= dist;

            if (UseSphereCast)
            {
                int count = Physics.SphereCastNonAlloc(start, SphereRadius, dir, _hitBuffer, dist, OccluderLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (_hitBuffer[i].collider) collector.Add(_hitBuffer[i].collider.gameObject);
            }
            else
            {
                int count = Physics.RaycastNonAlloc(start, dir, _hitBuffer, dist, OccluderLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (_hitBuffer[i].collider) collector.Add(_hitBuffer[i].collider.gameObject);
            }
        }

        static void IncludeChildrenDepth(GameObject root, int depth, HashSet<GameObject> set)
        {
            if (depth <= 0 || root == null) return;
            var q = new Queue<(Transform t, int d)>();
            q.Enqueue((root.transform, 0));

            while (q.Count > 0)
            {
                var (t, d) = q.Dequeue();
                foreach (Transform c in t)
                {
                    set.Add(c.gameObject);
                    if (d + 1 < depth)
                        q.Enqueue((c, d + 1));
                }
            }
        }

        void ApplyTransparent(Renderer r)
        {
            if (!r) return;

            if (Mode == TransparencyMode.PropertyBlock)
            {
                r.GetPropertyBlock(_mpb); // preserve other overrides

                // choose alpha (optionally multiply original from sharedMaterial)
                float alpha = TargetAlpha;
                if (MultiplyOriginalAlpha && r.sharedMaterial && TryGetAnyColor(r.sharedMaterial, out var baseCol))
                    alpha = Mathf.Clamp01(baseCol.a * TargetAlpha);

                // try each configured color property
                foreach (var prop in ColorProperties)
                    if (!string.IsNullOrEmpty(prop))
                        _mpb.SetColor(prop, new Color(1, 1, 1, alpha));

                r.SetPropertyBlock(_mpb);
            }
            else // MaterialSwap
            {
                if (_origMats.ContainsKey(r)) return; // already swapped

                var src = r.materials; // instanced; we keep reference to restore exactly
                _origMats[r] = src;

                var swapped = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var original = src[i];
                    var m = new Material(TransparentSwapMaterial);

                    // color
                    if (TryGetAnyColor(original, out var srcCol))
                    {
                        float a = MultiplyOriginalAlpha ? Mathf.Clamp01(srcCol.a * TargetAlpha) : TargetAlpha;
                        m.color = new Color(srcCol.r, srcCol.g, srcCol.b, a);
                    }
                    else
                    {
                        var c = m.color;
                        m.color = new Color(c.r, c.g, c.b, TargetAlpha);
                    }

                    // main texture & tiling/offset
                    if (TryGetAnyTexture(original, out var tex, out var prop))
                    {
                        m.SetTexture(prop, tex);
                        if (CopyMainTextureTilingOffset)
                        {
                            m.SetTextureScale(prop, original.GetTextureScale(prop));
                            m.SetTextureOffset(prop, original.GetTextureOffset(prop));
                        }
                    }

                    swapped[i] = m;
                }

                _swapMats[r] = swapped;
                r.materials = swapped;
            }
        }

        void Restore(Renderer r)
        {
            if (!r) return;

            if (Mode == TransparencyMode.PropertyBlock)
            {
                _mpb.Clear();
                r.SetPropertyBlock(_mpb);
            }
            else
            {
                if (_origMats.TryGetValue(r, out var originals))
                {
                    r.materials = originals;
                    _origMats.Remove(r);
                }
                if (_swapMats.TryGetValue(r, out var swaps))
                {
                    for (int i = 0; i < swaps.Length; i++)
                        if (swaps[i]) Destroy(swaps[i]);
                    _swapMats.Remove(r);
                }
            }
        }

        void RestoreAll()
        {
            foreach (var kv in _origMats)
                if (kv.Key) kv.Key.materials = kv.Value;

            foreach (var kv in _swapMats)
                if (kv.Value != null)
                    for (int i = 0; i < kv.Value.Length; i++)
                        if (kv.Value[i]) Destroy(kv.Value[i]);

            _origMats.Clear();
            _swapMats.Clear();
            _prevOccluders.Clear();
            _currOccluders.Clear();
            _mpb?.Clear();
        }

        bool TryGetAnyColor(Material m, out Color c)
        {
            // try configured props first
            foreach (var p in ColorProperties)
            {
                if (!string.IsNullOrEmpty(p) && m.HasProperty(p))
                {
                    c = m.GetColor(p);
                    return true;
                }
            }
            // fallback .color
            try { c = m.color; return true; } catch { c = default; return false; }
        }

        bool TryGetAnyTexture(Material m, out Texture t, out string usedProp)
        {
            foreach (var p in MainTextureProperties)
            {
                if (!string.IsNullOrEmpty(p) && m.HasProperty(p))
                {
                    t = m.GetTexture(p);
                    usedProp = p;
                    if (t) return true;
                }
            }
            t = null; usedProp = null;
            return false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (ParentIncludeDepth < 0) ParentIncludeDepth = 0;
            if (ChildrenIncludeDepth < 0) ChildrenIncludeDepth = 0;
            if (MaxHitsPerCast < 8) MaxHitsPerCast = 128;
            if (_hitBuffer == null || _hitBuffer.Length != MaxHitsPerCast)
                _hitBuffer = new RaycastHit[MaxHitsPerCast];
        }
#endif
    }
}
