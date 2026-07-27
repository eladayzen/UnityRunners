using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_Damage : MonoBehaviour
    {
        public enum DestroyType { Deactivate, Destroy, None }

        [BigHeader("Settings")] // Main settings and starting data
        [Tooltip("Current health value (read-only at runtime).")]
        [ReadOnly, Min(0)] public float Health;

        [Tooltip("Maximum health points when fully healed.")]
        [GUIColor("green"), Min(1)] public float MaxHealth = 1000;

        [Tooltip("What to do with this GameObject after health reaches 0 (upon defeat).")]
        public DestroyType DefeatType;

        [Tooltip("Delay (seconds) before applying DefeatType after OnDefeat is invoked. Does not delay the OnDefeat event itself.")]
        [Min(0)] public float DelayBeforeApply;

        // Hit Detection
        [BigHeader("Hit Detection")]
        [Tooltip("Receive damage/heal through Trigger callbacks (OnTriggerEnter).")]
        public bool UseOnTrigger = true;

        [Tooltip("Receive damage/heal through Collision callbacks (OnCollisionEnter).")]
        public bool UseOnCollision = true;

        // Damage Section
        [BigHeader("Damage Section")]
        [Tooltip("Layers that count as damage sources when they collide/trigger with this object.")]
        public LayerMask DamageLayers;

        [Tooltip("Tags that count as damage sources when they collide/trigger with this object.")]
        public string[] DamageTags;

        [Line(1, .99f, spacing: 6)]
        [Tooltip("Destroy the incoming damage object (e.g., bullet) after it deals damage.")]
        public bool DestroyDamageObjWhenHit;

        [Tooltip("Ignore further collisions with the same damage collider after it hits once (prevents multiple hits).")]
        public bool DisableCollisionAfterDamage;

        [Tooltip("Fallback damage amount if the hitter has no custom DamageAmountSetter attached.")]
        [GUIColor("red"), Min(0)] public float DefaultDamageAmount = 100;

        // Healing Section
        [BigHeader("Healing Section")]

        [Tooltip("Layers that count as healing sources when they collide/trigger with this object.")]
        public LayerMask HealLayers;

        [Tooltip("Tags that count as healing sources when they collide/trigger with this object.")]
        public string[] HealTags;

        [Line(1, .99f, spacing: 6)]
        [Tooltip("Destroy the incoming heal object after it applies healing.")]
        public bool DestroyHealObjWhenHit;

        [Tooltip("Fallback heal amount if the healer has no custom DamageAmountSetter attached.")]
        [GUIColor("cyan"), Min(0)] public float DefaultHealAmount = 500;

        // Health Bar
        [BigHeader("Health Bar")]
        [Tooltip("If enabled, synchronize a HealthBar component with this damage system.")]
        public bool EnableHealthBar;

        [Tooltip("Required if Health Bar syncing is enabled. Assign a component with HealthBar behavior.")]
        [Required("(Require If)")] public HealthBar HealthBar;

        [Tooltip("If enabled, disables the HealthBar GameObject when health reaches 0 (on defeat).")]
        public bool DeactivateOnKillingHit;

        // Events
        [BigHeader("Events")]
        [Tooltip("Invoked when this object takes damage and remains alive.")]
        public UnityEvent OnTakeDamage = new();

        [Tooltip("Invoked whenever health is increased (healed).")]
        public UnityEvent OnHeal = new();

        [Tooltip("Invoked once when health reaches 0 (before DefeatType is applied).")]
        public UnityEvent OnDefeat = new();

        [Tooltip("Invoked once when OnRevive Called (must be defeated) and health reset to MaxHealth.")]
        public UnityEvent OnRevive = new();

        bool _isDefeated;

        void Start()
        {
            Health = MaxHealth;
            if (HealthBar) HealthBar.ResetMaxHealth(MaxHealth);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!UseOnCollision) return;
            bool compareDamage = false;
            bool compareHeal = false;

            // Compare the collider tag to each tag in DamageTags
            foreach (string tag in DamageTags) if (collision.gameObject.CompareTag(tag)) compareDamage = true; // Check tags
            if (!compareDamage) compareDamage = DamageLayers == (DamageLayers | (1 << collision.gameObject.layer)); // Check layers if tags not match

            if (compareDamage)
            {
                if (collision.gameObject.GetComponent<DamageAmountSetter>())
                    OnDamage(collision.gameObject.GetComponent<DamageAmountSetter>().GetDamage(this, collision.contacts[0].point));
                else OnDamage();

                if (DisableCollisionAfterDamage && GetComponent<Collider>())
                    Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider);
                if (DestroyDamageObjWhenHit) Destroy(collision.gameObject);
            }
            else
            {
                foreach (string tag in HealTags) if (collision.gameObject.CompareTag(tag)) compareHeal = true; // Check tags
                if (!compareHeal) compareHeal = HealLayers == (HealLayers | (1 << collision.gameObject.layer)); // Check layers if tags not match

                if (compareHeal)
                {
                    if (collision.gameObject.GetComponent<DamageAmountSetter>())
                        AddHealth(collision.gameObject.GetComponent<DamageAmountSetter>().GetHealth(this, collision.contacts[0].point));
                    else AddHealth();
                    if (DestroyHealObjWhenHit) Destroy(collision.gameObject);
                }
            }
        }

        void OnTriggerEnter(Collider collider)
        {
            if (!UseOnTrigger) return;
            bool compareDamage = false;
            bool compareHeal = false;

            // Compare the collider tag to each tag in DamageTags
            foreach (string tag in DamageTags) if (collider.gameObject.CompareTag(tag)) compareDamage = true; // Check tags
            if (!compareDamage) compareDamage = DamageLayers == (DamageLayers | (1 << collider.gameObject.layer)); // Check layers if tags not match

            if (compareDamage)
            {
                if (collider.attachedRigidbody && collider.attachedRigidbody.gameObject.GetComponent<DamageAmountSetter>())
                    OnDamage(collider.attachedRigidbody.gameObject.GetComponent<DamageAmountSetter>().GetDamage(this, collider.ClosestPoint(transform.position)));
                else if (collider.gameObject.GetComponent<DamageAmountSetter>())
                    OnDamage(collider.gameObject.GetComponent<DamageAmountSetter>().GetDamage(this, collider.ClosestPoint(transform.position)));
                else OnDamage();
                if (DisableCollisionAfterDamage && GetComponent<Collider>())
                    Physics.IgnoreCollision(GetComponent<Collider>(), collider);
                if (DestroyDamageObjWhenHit) Destroy(collider.gameObject);
            }
            else
            {
                foreach (string tag in HealTags) if (collider.gameObject.CompareTag(tag)) compareHeal = true; // Check tags
                if (!compareHeal) compareHeal = HealLayers == (HealLayers | (1 << collider.gameObject.layer)); // Check layers if tags not match

                if (compareHeal)
                {
                    if (collider.attachedRigidbody && collider.attachedRigidbody.gameObject.GetComponent<DamageAmountSetter>())
                        AddHealth(collider.attachedRigidbody.gameObject.GetComponent<DamageAmountSetter>().GetHealth(this, collider.ClosestPoint(transform.position)));
                    else if (collider.gameObject.GetComponent<DamageAmountSetter>())
                        AddHealth(collider.gameObject.GetComponent<DamageAmountSetter>().GetHealth(this, collider.ClosestPoint(transform.position)));
                    else AddHealth();
                    if (DestroyHealObjWhenHit) Destroy(collider.gameObject);
                }
            }
        }

        public void UpdateMaxHealth(float newMaxHealth)
        {
            Health += newMaxHealth - MaxHealth;
            MaxHealth = newMaxHealth;
        }

        public void AddHealth(float amount = float.NaN)
        {
            amount = amount is float.NaN ? DefaultHealAmount : Mathf.Max(0,amount);
            Health = Mathf.Min(Health + amount, MaxHealth);
            OnHeal.Invoke();
            if (EnableHealthBar) HealthBar.UpdateHealth(Health);
        }

        public void OnDamage(float amount = float.NaN)
        {
            amount = amount is float.NaN ? DefaultDamageAmount : Mathf.Max(0, amount);
            Health -= amount;

            if (Health <= 0) InstantKill();
            else OnTakeDamage.Invoke();
            if (EnableHealthBar)
            {
                if (Health <= 0 && DeactivateOnKillingHit)
                {
                    HealthBar.Health = 0;
                    HealthBar.gameObject.SetActive(false);
                }
                else HealthBar.UpdateHealth(Health);
            }
        }

        public void InstantKill()
        {
            StartCoroutine(KillObject());
        }

        public void Revive()
        {
            if (_isDefeated && DefeatType != DestroyType.Destroy)
            {
                _isDefeated = false;
                gameObject.SetActive(true); 
                Health = MaxHealth;
                if (EnableHealthBar) HealthBar.UpdateHealth(Health);
                OnRevive?.Invoke();
            }
        }

        IEnumerator KillObject()
        {
            if (!_isDefeated)
            {
                _isDefeated = true;
                OnDefeat.Invoke(); // Activate OnDefeat Events
                yield return new WaitForSeconds(DelayBeforeApply);
                if (DefeatType == DestroyType.Destroy) Destroy(gameObject);
                else if (DefeatType == DestroyType.Deactivate) gameObject.SetActive(false);
            }
        }

        public float GetHealth() => Health;
        public bool IsDefeated() => Health <= 0;
    }
}
