using System;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_Collision_Trigger : MOST_ActionCore
    {
        public MOSTAction_Collision_Trigger() { ActionName = "Collision and Trigger"; }

        [Line]
        [Tooltip("Optional override for Owner.BaseTarget. If assigned, this action operates on this object instead of the base target.")]
        [InnerHint("(Optional)")] public GameObject TargetObject;

        public bool UseOnCollision;
        public bool UseOnTrigger;

        [Tooltip("Layers that detecor will sense (must have a collider)")]
        public LayerMask TargetLayers;

        [Tooltip("Tags that detecor will sense (must have a collider). Leave empty to accept all.")]
        public string[] TargetTags;

        [Tooltip("Fires when a matching CollisionEnter occurs")]
        public UnityEvent<GameObject> OnCollisionEnter;
        [Tooltip("Fires when a matching CollisionExit occurs")]
        public UnityEvent<GameObject> OnCollisionExit;
        [Tooltip("Fires when a matching TriggerEnter occurs")]
        public UnityEvent<GameObject> OnTriggerEnter;
        [Tooltip("Fires when a matching TriggerExit occurs")]
        public UnityEvent<GameObject> OnTriggerExit;

        GameObject _target;

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

        public override void OnUpdate()  // Called each frame as Update function
        {
            base.OnUpdate();
        }

        protected override void Play()
        {
            IsPlaying = true;
            _target = TargetObject == null ? Owner.BaseTarget : TargetObject;
            if (!_target.GetComponent<Detector_Core>())
                _target.AddComponent<Detector_Core>().Initialize(this);
        }
        protected override void Stop()
        {
            IsPlaying = false;
        }

        public override void OnDestroy() // Called when this Owner behavior got destroyed
        {
            base.OnDestroy();
        }

        bool PassesFilters(GameObject go)
        {
            foreach (string tag in TargetTags) if (go.CompareTag(tag)) return true; // Check tags
            return TargetLayers == (TargetLayers | (1 << go.layer)); // Check layers if tags not match
        }

        public void OnTriggerEnterReciver(Collider other)
        {
            if (!IsPlaying || !UseOnTrigger || other == null) return;
            var go = other.gameObject;
            if (!PassesFilters(go)) return;
            OnTriggerEnter?.Invoke(go);
        }

        public void OnTriggerExitReciver(Collider other)
        {
            if (!IsPlaying || !UseOnTrigger || other == null) return;
            var go = other.gameObject;
            if (!PassesFilters(go)) return;
            OnTriggerExit?.Invoke(go);
        }

        public void OnCollisionEnterReciver(Collision other)
        {
            if (!IsPlaying || !UseOnCollision || other == null) return;
            var go = other.gameObject;
            if (!PassesFilters(go)) return;
            OnCollisionEnter?.Invoke(go);
        }

        public void OnCollisionExitReciver(Collision other)
        {
            if (!IsPlaying || !UseOnCollision || other == null) return;
            var go = other.gameObject;
            if (!PassesFilters(go)) return;
            OnCollisionExit?.Invoke(go);
        }
    }
}