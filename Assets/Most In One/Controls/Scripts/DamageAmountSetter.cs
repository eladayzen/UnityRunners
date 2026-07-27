using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class DamageAmountSetter : MonoBehaviour
    {
        public bool DeavtivateInstead;
        [BigHeader("Damage")]
        [Min(0)] public float DamageAmount;
        [Min(0)] public int DestroyAfterGetDamage;
        public GameObject OnDamageSpawn;
        public UnityEvent OnGetDamage;
        
        [BigHeader("Health")]
        [Min(0)] public float HealthAmount;
        [Min(0)] public int DestroyAfterGetHealth;
        
        public GameObject OnHealingSpawn;
        public UnityEvent OnGetHealth;

        [ReadOnly, HideIfAny(MOSTEdit.RuntimeOnly)]
        public List<MOST_Damage> AffectedSystemsDamg = new();
        [ReadOnly, HideIfAny(MOSTEdit.RuntimeOnly)]
        public List<MOST_Damage> AffectedSystemsHeal = new();

        int _healCount, _dmgCount;

        void OnEnable()
        {
            _healCount = DestroyAfterGetHealth;
            _dmgCount = DestroyAfterGetDamage;

            AffectedSystemsDamg.Clear();
            AffectedSystemsHeal.Clear();
        }

        public float GetHealth(MOST_Damage mDamage = null, Vector3? point = null)
        {
            if (mDamage != null) AffectedSystemsHeal.Add(mDamage);
            if (OnHealingSpawn && point != null) Destroy(Instantiate(OnHealingSpawn, (Vector3)point, Quaternion.Euler(0, transform.eulerAngles.y + 180, 0)), 4);
            OnGetHealth?.Invoke();
            if (_healCount > 0)
            {
                _healCount--;
                if (_healCount <= 0)
                {
                    if (DeavtivateInstead) gameObject.SetActive(false);
                    else Destroy(gameObject);
                }
            }
            return HealthAmount;
        }

        public float GetDamage(MOST_Damage mDamage = null, Vector3? point = null)
        {
            if (mDamage != null) AffectedSystemsDamg.Add(mDamage);
            if (OnDamageSpawn && point != null) Destroy(Instantiate(OnDamageSpawn, (Vector3)point, Quaternion.Euler(0, transform.eulerAngles.y + 180, 0)), 4);
            OnGetDamage?.Invoke();
            if (_dmgCount > 0)
            {
                _dmgCount--;
                if (_dmgCount <= 0)
                {
                    if (DeavtivateInstead) gameObject.SetActive(false);
                    else Destroy(gameObject);
                }
            }
            return DamageAmount;
        }
    }
}