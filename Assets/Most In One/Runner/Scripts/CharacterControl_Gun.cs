using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_Gun : MonoBehaviour
    {
        [BigHeader("Main")]
        [Tooltip("Is Moving and shooting\nCall Activate([Optional] float cooldown) or deactivate() to cntrol it")]
        [ReadOnly] public bool IsActive;

        [Tooltip("The Damage/Health system attached to the character")]
        [Required] public MOST_Damage DamageSystem;

        [BigHeader("Projectile Spawn Settings")]
        [Required] public GameObject Bullet;
        [Required] public Transform SpawnPoint;
        [InnerHint("(Optional)")] public ParticleSystem MuzzleFlare;
        [InnerHint("(Optional)")] public AudioSource SpawnSound;
        
        [BigHeader("Animations")]
        [InnerHint("(Optional)")] public Animation Animation;
        public string OnShootAnimation;
        public string OnDefeatAnimation;

        [BigHeader("Game Events")]
        [GUIColor("red")] public LayerMask EndLineMask;
        [Line]
        [Tooltip("Event When the level passed and win")]
        public UnityEvent EndLineTriggerEvent;
        [Tooltip("Event When the level passed and win")]
        public UnityEvent OnWinEvent;
        [Tooltip("Event When the character defeated before level passed and lose")]
        public UnityEvent OnLoseEvent;

        [BigHeader("Upgrade and Data Display")]
        public MOST_Database Holder;
        public string CurrencyName;
        [Line]
        [Header("Health Settings")]
        public float MaxHealth_IncreasePerLevel = 1;
        public int MaxHealth_MaxLevel = 10;
        public TMP_Text MaxHealthText;
        public GameObject CostDispaly_MaxHealth;
        public GameObject OnMaxDisplay_MaxHealth;
        [Line]
        [Header("Fire Rate Settings")]
        public float FireRate = 3;
        public float FireRate_IncreasePerLevel = .3f;
        public int FireRate_MaxLevel = 10;
        public TMP_Text FireRateText;
        public GameObject CostDispaly_FireRate;
        public GameObject OnMaxDisplay_FireRate;
        [Line]
        [Header("Fire Range Settings")]
        public float FireRange = 1;
        public float FireRange_IncreasePerLevel = .1f;
        public int FireRange_MaxLevel = 10;
        public TMP_Text FireRangeText;
        public GameObject CostDispaly_FireRange;
        public GameObject OnMaxDisplay_FireRange;
        [Line]
        [Header("Cost Settings")]
        public int CostPerUpgrade;
        public UnityEvent<bool> EnoughCurrency;
        public UnityEvent<bool> NotEnoughCurrency;
        public UnityEvent OnUpgradeEvent;

        float _shootCooldown,_maxHealth_lvl1, _fireRate_lvl1, _fireRange_lvl1,
            _maxHealth_maxLevel, _fireRate_maxLevel, _fireRange_maxLevel;

        void Start()
        {
            LoadData();
        }

        void Update()
        {
            if (IsActive && _shootCooldown <= 0)
            {
                Destroy( Instantiate(Bullet, SpawnPoint.position, transform.rotation),FireRange);
                if (MuzzleFlare) MuzzleFlare.Play();
                if (SpawnSound) SpawnSound.Play();
                if(Animation) Animation.Play(OnShootAnimation);
                _shootCooldown = 1 / FireRate;
            }
            else _shootCooldown -= Time.deltaTime;
        }

        public void Activate([Optional] float cooldown)
        {
            IsActive = true;
            _shootCooldown = cooldown;
            GetComponent<MOST_RoadMovement>().EnableState(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
        }

        public void UpdateHealth(float amount)
        {
            if (DamageSystem)
            {
                if (amount > 0) DamageSystem.AddHealth(amount);
                else DamageSystem.OnDamage(-amount);
            }
            else Debug.Log("The healing function required MOST_Damage system attached to the object");
        }

        public void OnDefeat()
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
            if(Animation) Animation.Play(OnDefeatAnimation);
            OnLoseEvent?.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Gate")) // Gate Triggerd
            {
                if (other.GetComponent<MOST_Gate>()) // accepts olny if the object has Most_Gate system
                    GateTriggered(other.GetComponent<MOST_Gate>());
            }
            else if (EndLineMask == (EndLineMask | (1 << other.gameObject.layer))) // EndPoint
            {
                EndLineTriggerEvent?.Invoke();
            }
        }

        void GateTriggered(MOST_Gate gate)
        {
            if (gate.IsCollected) return;
            if (gate.Type == MOST_Gate.GateType.Health)
            {
                if (DamageSystem)
                {
                    float health = gate.Calculation(DamageSystem.Health);
                    UpdateHealth(health - DamageSystem.Health);
                }
                else Debug.Log("There is no Damage component attached to the character to get the health value");
            }
            else if (gate.Type == MOST_Gate.GateType.FireRate)
            {
                FireRate = gate.Calculation(FireRate);
            }
            else if (gate.Type == MOST_Gate.GateType.FireRange)
            {
                FireRange = gate.Calculation(FireRange);
            }
            else if (gate.Type == MOST_Gate.GateType.Upgrade)
            {
                // Debug.Log("Upgrade gate Triggered");
            }
            else if (gate.Type == MOST_Gate.GateType.AddChilds)
            {
                // Debug.Log("AddChilds gate Triggered");
            }
            else if (gate.Type == MOST_Gate.GateType.Currency)
            {
                gate.Calculation();
            }
            else
            {
                Debug.Log("This Gate type is not defined on this character control");
            }
        }

        #region Cherecter Upgrade and Data
        void LoadData()
        {
            if (DamageSystem)
            {
                _maxHealth_lvl1 = DamageSystem.MaxHealth; // Start/Level 1 value
                _maxHealth_maxLevel = Mathf.Round((_maxHealth_lvl1 + MaxHealth_IncreasePerLevel * (MaxHealth_MaxLevel - 1)) *100) / 100;

                if (!PlayerPrefs.HasKey("CharacterGun_MaxHealth"))
                    PlayerPrefs.SetFloat("CharacterGun_MaxHealth", DamageSystem.MaxHealth);
                else DamageSystem.MaxHealth = PlayerPrefs.GetFloat("CharacterGun_MaxHealth");
                MaxHealthText.text = DamageSystem.MaxHealth.ToString("0.00");
                if (DamageSystem.MaxHealth >= _maxHealth_maxLevel)
                {
                    CostDispaly_MaxHealth.SetActive(false);
                    OnMaxDisplay_MaxHealth.SetActive(true);
                }
            }

            _fireRate_lvl1 = FireRate; // Start/Level 1 value
            _fireRate_maxLevel = Mathf.Round((_fireRate_lvl1 + FireRate_IncreasePerLevel * (FireRate_MaxLevel - 1)) * 100) / 100;
            if (!PlayerPrefs.HasKey("CharacterGun_FireRate"))
                PlayerPrefs.SetFloat("CharacterGun_FireRate", FireRate);
            else FireRate = PlayerPrefs.GetFloat("CharacterGun_FireRate");
            FireRateText.text = FireRate.ToString("0.00");
            if (FireRate >= _fireRate_maxLevel)
            {
                CostDispaly_FireRate.SetActive(false);
                OnMaxDisplay_FireRate.SetActive(true);
            }

            _fireRange_lvl1 = FireRange; // Start/Level 1 value
            _fireRange_maxLevel = Mathf.Round((_fireRange_lvl1 + FireRange_IncreasePerLevel * (FireRange_MaxLevel - 1)) *100) / 100;
            if (!PlayerPrefs.HasKey("CharacterGun_FireRange"))
                PlayerPrefs.SetFloat("CharacterGun_FireRange", FireRange);
            else FireRange = PlayerPrefs.GetFloat("CharacterGun_FireRange");
            FireRangeText.text = FireRange.ToString("0.00");
            if (FireRange >= _fireRange_maxLevel)
            {
                CostDispaly_FireRange.SetActive(false);
                OnMaxDisplay_FireRange.SetActive(true);
            }
            
            EnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value >= CostPerUpgrade);
            NotEnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value < CostPerUpgrade);
        }

        public void UpgradeMaxHealth([Optional] float value)
        {
            if (DamageSystem)
            {
                if (DamageSystem.MaxHealth >= _maxHealth_maxLevel) Debug.Log("Max Health already at max level.");
                else if (Holder.Get<IntData>(CurrencyName).Value > CostPerUpgrade)
                {
                    Holder.Get<IntData>(CurrencyName).Subtract(CostPerUpgrade);
                    value = value == 0 ? MaxHealth_IncreasePerLevel : value;
                    DamageSystem.UpdateMaxHealth(DamageSystem.MaxHealth + value);
                    MaxHealthText.text = DamageSystem.MaxHealth.ToString("0.00");
                    PlayerPrefs.SetFloat("CharacterGun_MaxHealth", DamageSystem.MaxHealth);
                    if (DamageSystem.MaxHealth >= _maxHealth_maxLevel)
                    {
                        CostDispaly_MaxHealth.SetActive(false);
                        OnMaxDisplay_MaxHealth.SetActive(true);
                    }
                    EnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value >= CostPerUpgrade);
                    NotEnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value < CostPerUpgrade);
                }
            }
            else Debug.Log("There is no Damage component attached to the character to get the health value");
        }

        public void UpgradeFireRate([Optional] float value)
        {
            if (FireRate >= _fireRate_maxLevel) Debug.Log("Fire Rate already at max level.");
            else if (Holder.Get<IntData>(CurrencyName).Value > CostPerUpgrade)
            {
                Holder.Get<IntData>(CurrencyName).Subtract(CostPerUpgrade);
                value = value == 0 ? FireRate_IncreasePerLevel : value;
                FireRate += value;
                FireRateText.text = FireRate.ToString("0.00");
                PlayerPrefs.SetFloat("CharacterGun_FireRate", FireRate);
                if (FireRate >= _fireRate_maxLevel)
                {
                    CostDispaly_FireRate.SetActive(false);
                    OnMaxDisplay_FireRate.SetActive(true);
                }
                EnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value >= CostPerUpgrade);
                NotEnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value < CostPerUpgrade);
            }    
        }

        public void UpgradeFireRange([Optional] float value)
        {
            if (FireRange >= _fireRange_maxLevel) Debug.Log("Fire Range already at max level.");
            else if (Holder.Get<IntData>(CurrencyName).Value > CostPerUpgrade)
            {
                Holder.Get<IntData>(CurrencyName).Subtract(CostPerUpgrade);
                value = value == 0 ? FireRange_IncreasePerLevel : value;
                FireRange += value;
                FireRangeText.text = FireRange.ToString("0.00");
                PlayerPrefs.SetFloat("CharacterGun_FireRange", FireRange);
                if (FireRange >= _fireRange_maxLevel)
                {
                    CostDispaly_FireRange.SetActive(false);
                    OnMaxDisplay_FireRange.SetActive(true);
                }
                EnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value >= CostPerUpgrade);
                NotEnoughCurrency?.Invoke(Holder.Get<IntData>(CurrencyName).Value < CostPerUpgrade);
            }
        }
        #endregion
    }
}
