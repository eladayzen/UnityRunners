using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_ShootRunner : MonoBehaviour
    {
        [BigHeader("Main")]
        [Tooltip("Is Moving and shooting\nCall Activate([Optional] float cooldown) or deactivate() to cntrol it")]
        [ReadOnly] public bool IsActive;

        [Tooltip("The child holder object inside the character object")]
        [Required] public Transform ChildHolder;

        [Tooltip("The movement system attached to the character")]
        [Required] public MOST_RoadMovement MovementSystem;

        [BigHeader("Crowd Settings")]
        [ReadOnly, GUIColor("cyan")] public int NumberOfChilds;
        [GUIColor("green"), Min(1)] public int StartAmount = 1;

        [Tooltip("Rescale factor of the distance inside the circle")]
        [Range(0f, 3f)] public float DistanceFactor = .15f;

        [Tooltip("Jump radius for each step")]
        [Range(0f, 3f)] public float Radius = 2;

        [Tooltip("extra scale of the Character Collider added to its auto scaled collision")]
        [Min(0)] public float ExtraCollisionScale;
        [Tooltip("To Display the current child cound")]
        public TMP_Text CurrentChildCount;

        [BigHeader("Game Events")]
        [GUIColor("red")] public LayerMask EndLineMask;
        [Line]
        [Tooltip("Event When the level passed and win")]
        public UnityEvent EndLineTriggerEvent;
        [Tooltip("Event When the level passed and win")]
        public UnityEvent OnWinEvent;
        [Tooltip("Event When the character defeated before level passed and lose")]
        public UnityEvent OnLoseEvent;

        [BigHeader("Childs & Upgrade Settings")]
        [ReadOnly, GUIColor("cyan")] public int CurrentLevel = 1;

        [Tooltip("Sort it for each level, last one is the max level")]
        public GameObject[] ChildPrefabs;
        [Tooltip("The delay before start shooting (first spawned bullet)")]
        public float StartShootingDelay;
        [Tooltip("If enabled, all childs will shoot at the same time")]
        public bool SyncChildsShooting = true;

        [Tooltip("For better controlling and prevent overlapping, shoot sound controlled by the parent\n" +
            "you can disable it by empty this list")]
        public AudioSource[] ShootSoundEffects;

        [BigHeader("Upgrade Effects")]
        public MOST_Action Action;
        public string UpActionName;
        public float UpActionDuration;
        public string DownActionName;
        public float DownActionDuration;

        float _manualShootCooldown;
        string _movedirection;
        bool _endTrigger, _manualTrigger;
        Vector2 _idleConsetrains;

        void Start()
        {
            _idleConsetrains = GetComponent<MOST_RoadMovement>().RoadConstraints;
            if (StartAmount > ChildHolder.childCount) InstantAddChilds(StartAmount - ChildHolder.childCount);
            else UpdateChildNumber();
        }
        void Update()
        {
            ChildAnimationControl();
            if (NumberOfChilds != ChildHolder.childCount) UpdateChildNumber();
            if (SyncChildsShooting && IsActive) ChildShooting_Sync();
        }

        public void Activate() // Called When Game Starts and character moves
        {
            IsActive = true;
            GetComponent<MOST_RoadMovement>().EnableState(true);
            if (!SyncChildsShooting) for (int i = 0; i < ChildHolder.childCount; i++)
                    ChildHolder.GetChild(i).GetComponent<CharacterControl_ShootRunner_Child>().StartShooting();
            else
            {
                _manualShootCooldown = StartShootingDelay;
                _manualTrigger = true;
            }
        }

        public void Deactivate() 
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
            for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).GetComponent<CharacterControl_ShootRunner_Child>().IsActive = false;
            _manualTrigger = false;
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
                if (!_endTrigger)
                {
                    _endTrigger = true;
                    EndLineTriggerEvent?.Invoke();
                }
                else EndGameTrigger();
            }
        }

        void GateTriggered(MOST_Gate gate)
        {
            if (gate.IsCollected) return;
            if (gate.Type == MOST_Gate.GateType.Health)
            {
                for (int i = 0; i < ChildHolder.childCount; i++)
                {
                    if (ChildHolder.GetChild(i).GetComponent<MOST_Damage>())
                    {
                        float original = ChildHolder.GetChild(i).GetComponent<MOST_Damage>().Health;
                        float health = gate.Calculation(original);
                        if (health > original) ChildHolder.GetChild(i).GetComponent<MOST_Damage>().AddHealth(health - original);
                        else ChildHolder.GetChild(i).GetComponent<MOST_Damage>().OnDamage(original - health);
                    }
                    else Debug.Log("The healing function required MOST_Damage system attached to the object");
                }
            }
            else if (gate.Type == MOST_Gate.GateType.FireRate)
            {
                // Debug.Log("FireRate gate Triggered");
            }
            else if (gate.Type == MOST_Gate.GateType.FireRange)
            {
                // Debug.Log("FireRange gate Triggered");
            }
            else if (gate.Type == MOST_Gate.GateType.Upgrade)
            {
                gate.Calculation(1);
                UpgradeChilds();
            }
            else if (gate.Type == MOST_Gate.GateType.AddChilds)
            {
                SpawnChilds((int)gate.Calculation(NumberOfChilds));
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

        void EndGameTrigger()
        {
            Deactivate();
            if (_endTrigger) OnWinEvent?.Invoke();
            else OnLoseEvent?.Invoke();
        }

        #region Child control Functions
        
        public void UpdateHealth(float amount)
        {
            for (int i = 0; i < ChildHolder.childCount; i++)
            {
                if (ChildHolder.GetChild(i).GetComponent<MOST_Damage>())
                {
                    if (amount > 0) ChildHolder.GetChild(i).GetComponent<MOST_Damage>().AddHealth(amount);
                    else ChildHolder.GetChild(i).GetComponent<MOST_Damage>().OnDamage(-amount);
                }
                else Debug.Log("The healing function required MOST_Damage system attached to the object");
            }
        }

        void SpawnChilds(int NextNumOfChilds)
        {
            if (NextNumOfChilds < NumberOfChilds)
            {
                int val = NumberOfChilds;
                for (int i = NextNumOfChilds; i < val; i++)
                    ChildHolder.GetChild(ChildHolder.childCount - 1).gameObject.GetComponent<CharacterControl_ShootRunner_Child>().DestroyChild();
            }
            else
            {
                for (int i = NumberOfChilds; i < NextNumOfChilds; i++)
                {
                    Instantiate(ChildPrefabs[CurrentLevel - 1], ChildHolder.position, Quaternion.identity, ChildHolder);
                    UpdateIdlePositions();
                }
            }
            UpdateChildNumber(); // after spawn update child number 
            UpdateConsitrains(); // after spawn update Road consitrains
            _movedirection = "Idle"; // after spawn update animation
        }

        public void UpdateIdlePositions()
        {
            for (int i = 0; i < ChildHolder.childCount; i++)
            {
                GameObject child = ChildHolder.GetChild(i).gameObject;
                float xPos = DistanceFactor * Mathf.Sqrt(i) * Mathf.Cos(i * Radius);
                float zPos = DistanceFactor * Mathf.Sqrt(i) * Mathf.Sin(i * Radius);
                child.GetComponent<CharacterControl_ShootRunner_Child>().EditIdlePos(new Vector3(xPos, 0, zPos), IsActive && !SyncChildsShooting); // if the game started set run animation true and move the child
            }
        }

        public void UpdateChildNumber() // this also for checking if defeated or not by check if number of childs == 0
        {
            NumberOfChilds = ChildHolder.childCount;
            CurrentChildCount.text = NumberOfChilds.ToString();
            if (NumberOfChilds == 0)  // __________ Defeated ____________ 
            {
                EndGameTrigger();
            }
            else if (!_endTrigger && ChildHolder.childCount > 0) UpdateConsitrains();
        }

        void UpdateConsitrains() // fix the main character collision scale, position and update road Consitrains
        {
            Vector2 DistUpdate = Vector2.one * ChildHolder.GetChild(0).GetComponent<CharacterControl_ShootRunner_Child>().IdleTargetPos.x;
            for (int i = 0; i < ChildHolder.childCount; i++) // find the most shifted child on right and left
            {
                DistUpdate.x = Mathf.Min(DistUpdate.x, ChildHolder.GetChild(i).GetComponent<CharacterControl_ShootRunner_Child>().IdleTargetPos.x); // Max Child Offset On Left
                DistUpdate.y = Mathf.Max(DistUpdate.y, ChildHolder.GetChild(i).GetComponent<CharacterControl_ShootRunner_Child>().IdleTargetPos.x); // Max Child Offset On Right
            }
            DistUpdate *= ChildHolder.transform.localScale.x;

            // update the Road Consetrains of Most_RoadMovement
            GetComponent<MOST_RoadMovement>().RoadConstraints = new Vector2(
                Mathf.Min(0, _idleConsetrains.x - DistUpdate.x), Mathf.Max(0, _idleConsetrains.y - DistUpdate.y));

            // update the scale of character collider
            GetComponent<SphereCollider>().radius = (Mathf.Abs(DistUpdate.x - DistUpdate.y) * ChildHolder.transform.localScale.x / 4) + ExtraCollisionScale;

            // update the position of character collider
            GetComponent<SphereCollider>().center = new Vector3((DistUpdate.x + DistUpdate.y) / 2,
                GetComponent<SphereCollider>().center.y, GetComponent<SphereCollider>().center.z);
        }

        public void InstantAddChilds(int amount) // this method for quick and instant add childs
        {
            SpawnChilds(amount + NumberOfChilds);
        }
        public void UpgradeChilds()
        {
            if (CurrentLevel >= ChildPrefabs.Length) Debug.Log("Already Max Level");
            else StartCoroutine(Upgrade());
        }

        void ChildShooting_Sync()
        {
            if (_manualTrigger && IsActive && _manualShootCooldown <= 0)
            {
                for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).GetComponent<CharacterControl_ShootRunner_Child>().Shoot();
                _manualShootCooldown = 1 / ChildPrefabs[CurrentLevel - 1].GetComponent<CharacterControl_ShootRunner_Child>().SpawnSpeed;
                if(ShootSoundEffects.Length >= CurrentLevel) ShootSoundEffects[CurrentLevel - 1].Play();
            }
            else _manualShootCooldown -= Time.deltaTime;
        }

        IEnumerator Upgrade()
        {
            CurrentLevel++;
            _manualTrigger = false;
            for (int i = 0; i < ChildHolder.childCount; i++)
            {
                ChildHolder.GetChild(i).gameObject.GetComponent<CharacterControl_ShootRunner_Child>().IsActive = false;
            }
            Action.PlayAction(UpActionName);
            yield return new WaitForSeconds(UpActionDuration - 2 * Time.deltaTime);
            Action.StopAction(UpActionName);
            int v = ChildHolder.childCount;
            for (int i = 0; i < v; i++)
            {
                Vector3 pos = ChildHolder.GetChild(i).position;
                Destroy(ChildHolder.GetChild(i).gameObject);

                GameObject newChild = Instantiate(ChildPrefabs[CurrentLevel - 1], pos, Quaternion.identity, ChildHolder);
                if (!SyncChildsShooting) newChild.GetComponent<CharacterControl_ShootRunner_Child>().StartShooting(DownActionDuration);
            }
            Action.PlayAction(DownActionName);
            if (SyncChildsShooting)
            {
                _manualTrigger = true;
                _manualShootCooldown = DownActionDuration;
            }
            _movedirection = "Idle";
        }

        void ChildAnimationControl()
        {
            if (MovementSystem && _movedirection != MovementSystem.MovementDirection)
            {
                _movedirection = MovementSystem.MovementDirection;
                for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).GetComponent<CharacterControl_ShootRunner_Child>()
                        .SetAnimation(_movedirection == "Forward" && MovementSystem.ForwardSpeed == 0 ? "Idle" : _movedirection);
            }
        }
        #endregion
    }
}