using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_Count : MonoBehaviour
    {
        [BigHeader("Main")]
        [Tooltip("Is Moving and shooting\nCall Activate([Optional] float cooldown) or deactivate() to cntrol it")]
        [ReadOnly] public bool IsActive;

        [Tooltip("Spawn prefab")]
        [Required] public GameObject ChildPrefab;

        [Tooltip("The child holder object inside the character object")]
        [Required] public Transform ChildHolder;
        [Tooltip("The child holder object inside the character object")]
        [Required] public GameObject CollisionHolder;
        [Tooltip("The child holder object inside the character object")]
        [Required] public BoxCollider CollisionRef;

        [BigHeader("Crowd Settings")]
        [Tooltip("Represent the current number of childs, call it to check the current state of the character")]
        [ReadOnly,GUIColor("cyan")] public int NumberOfChilds;

        [GUIColor("green"), Min(1)] public int StartAmount = 1;

        [Tooltip("Rescale factor of the distance inside the circle")]
        [Range(0f, 3f)] public float DistanceFactor = .15f;

        [Tooltip("Jump radius for each step")]
        [Range(0f, 3f)] public float Radius = 2;

        //[Tooltip("extra scale of the Character Collider added to its auto scaled collision")]
        //[Min(0)] public float ExtraCollisionScale;
        [Tooltip("To Display the current child cound")]
        public TMP_Text CurrentChildCount;

        [BigHeader("End Line")]
        [GUIColor("red")] public LayerMask EndLineMask;
        [Tooltip("Applied when end point triggered")]
        public float MoveSpeedAtEnd;
        [Tooltip("Applied when end point triggered")]
        public Vector3 CameraOffset;
        [Tooltip("Applied when end point triggered")]
        public Vector3 CameraRotation;
        [Tooltip("Applied when end point triggered")]
        public float CameraOffsetSpeed;

        [BigHeader("Game Events")]
        [Tooltip("Event When the level passed and win")]
        public UnityEvent EndLineTriggerEvent;
        [Tooltip("Event When the level passed and win")]
        public UnityEvent OnWinEvent;
        [Tooltip("Event When the character defeated before level passed and lose")]
        public UnityEvent OnLoseEvent;

        List<BoxCollider> _colliderList = new();
        bool _attack, _endTrigger;
        Transform _attackPos;
        Vector3 _tmpPos = Vector3.zero, _startCamPos; float _camOffsetY;
        Vector2 _idleConsetrains;

        void Start()
        {
            _startCamPos = Camera.main.transform.position - transform.position;
            _idleConsetrains = GetComponent<MOST_RoadMovement>().RoadConstraints;
            if (StartAmount > ChildHolder.childCount) InstantAddChilds(StartAmount - ChildHolder.childCount);
            else UpdateChildNumber();
        }
        void Update()
        {
            if (_attack) for (int i = 0; i < ChildHolder.childCount; i++) // move all childs toward enemies
                    ChildHolder.GetChild(i).transform.position = Vector3.MoveTowards(
                        ChildHolder.GetChild(i).transform.position, _attackPos.position, Time.deltaTime * 1.5f);

            if (_endTrigger) MoveAtEndGame(); // when passed end point
        }

        public void Activate() // Called When Game Starts and character moves
        {
            IsActive = true;
            GetComponent<MOST_RoadMovement>().EnableState(true); // enable movement system
            for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).GetComponent<CharacterControl_Count_Child>().EditAnimation(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
            for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).GetComponent<CharacterControl_Count_Child>().EditAnimation(false);
        }

        public void StopAttack()
        {
            _attack = false;
            GetComponent<MOST_RoadMovement>().EnableState(true); // re enable controls
            for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).localEulerAngles = Vector3.zero;
            UpdateIdlePositions(); // reset all childs positions
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Gate")) // Gate Triggerd
            {
                if (other.transform.GetComponent<MOST_Gate>().IsCollected) return;
                SpawnChilds((int)other.transform.GetComponent<MOST_Gate>().Calculation(NumberOfChilds));
            }
            else if (other.CompareTag("Enemy"))
            {
                if (other.gameObject.layer.Equals(7)) // EnemySpot // Type 1
                {
                    _attack = true; _attackPos = other.transform;
                    other.gameObject.GetComponent<EnemyZoneControl>().StartAttacking(gameObject);
                    GetComponent<MOST_RoadMovement>().EnableState(false);
                    for (int i = 0; i < ChildHolder.childCount; i++) ChildHolder.GetChild(i).LookAt(_attackPos);
                }
            }
            else if (other.CompareTag("Score Object")) // EndPoint Obst
            {
                _camOffsetY = _counterLevels * TowerChildOffsetY - 2; // for each stair
            }
            else if (EndLineMask == (EndLineMask | (1 << other.gameObject.layer))) // EndPoint
            {
                if (_endTrigger) return; // to prevent multipl calls
                _endTrigger = true;
                StartCoroutine(EndLineTriggered());
                GetComponent<MOST_RoadMovement>().EnableState(false);
                EndLineTriggerEvent?.Invoke();
            }
        }

        #region Child control Functions
        void SpawnChilds(int NextNumOfChilds) // Opreation "Add" for spawn more childs and "FixPos" for reset all childs positions
        {
            if (NextNumOfChilds < NumberOfChilds)
            {
                int val = NumberOfChilds;
                for (int i = NextNumOfChilds; i < val; i++)
                    ChildHolder.GetChild(ChildHolder.childCount - 1).gameObject.GetComponent<CharacterControl_Count_Child>().DestroyChild();
            }
            else
            {
                for (int i = NumberOfChilds; i < NextNumOfChilds; i++)
                    Instantiate(ChildPrefab, ChildHolder.position, Quaternion.identity, ChildHolder);
                UpdateIdlePositions();
            }
            UpdateChildNumber(); // after spawn update child number 
            UpdateConsitrains(); // after spawn update Road consitrains
        }

        public void UpdateIdlePositions()
        {
            for (int i = 0; i < ChildHolder.childCount; i++)
            {
                GameObject child = ChildHolder.GetChild(i).gameObject;
                float xPos = DistanceFactor * Mathf.Sqrt(i) * Mathf.Cos(i * Radius);
                float zPos = DistanceFactor * Mathf.Sqrt(i) * Mathf.Sin(i * Radius);
                child.GetComponent<CharacterControl_Count_Child>().EditIdlePos(new Vector3(xPos, 0, zPos), IsActive); // if the game started set run animation true and move the child
            }
            UpdateCollider();
        }

        public void UpdateCollider()
        {
            if (_colliderList.Count > ChildHolder.childCount)
            {
                int count = ChildHolder.childCount - _colliderList.Count;
                for (int i = 0; i < count -1; i++)
                {
                    Destroy(_colliderList[^1]);
                    _colliderList.Remove(_colliderList[^1]);
                }
            }
            for (int i = 0; i < ChildHolder.childCount; i++)
            {
                if (_colliderList.Count -1 < i) _colliderList.Add(CollisionHolder.AddComponent<BoxCollider>());
                _colliderList[i].isTrigger = CollisionRef.isTrigger;
                _colliderList[i].size = CollisionRef.size;
                _colliderList[i].center = ChildHolder.GetChild(i).GetComponent<CharacterControl_Count_Child>().IdleTargetPos + new Vector3(0,CollisionRef.center.y,0);
            }
        }

        public void UpdateChildNumber() // this also for checking if defeated or not by check if number of childs == 0
        {
            NumberOfChilds = ChildHolder.childCount;
            if(CurrentChildCount) CurrentChildCount.text = NumberOfChilds.ToString();
            if (NumberOfChilds == 0)  // __________ Defeated ____________ 
            {
                if (_attack) _attackPos.gameObject.GetComponent<EnemyZoneControl>().StopAttacking();
                else GetComponent<MOST_RoadMovement>().Enable = false;
                OnLoseEvent?.Invoke();
            }
            else if (!_attack && ChildHolder.childCount > 0)
            {
                UpdateConsitrains();
                UpdateCollider();
            }
        }

        void UpdateConsitrains() // fix the main character collision scale, position and update road Consitrains
        {
            Vector2 DistUpdate = Vector2.one * ChildHolder.GetChild(0).GetComponent<CharacterControl_Count_Child>().IdleTargetPos.x;
            for (int i = 0; i < ChildHolder.childCount; i++) // find the most shifted child on right and left
            {
                DistUpdate.x = Mathf.Min(DistUpdate.x, ChildHolder.GetChild(i).GetComponent<CharacterControl_Count_Child>().IdleTargetPos.x); // Max Child Offset On Left
                DistUpdate.y = Mathf.Max(DistUpdate.y, ChildHolder.GetChild(i).GetComponent<CharacterControl_Count_Child>().IdleTargetPos.x); // Max Child Offset On Right
            }
            DistUpdate *= ChildHolder.transform.localScale.x;

            // update the Road Consetrains of Most_RoadMovement
            GetComponent<MOST_RoadMovement>().RoadConstraints = new Vector2(
                Mathf.Min(0, _idleConsetrains.x - DistUpdate.x), Mathf.Max(0, _idleConsetrains.y - DistUpdate.y));

            //// update the scale of character collider
            //GetComponent<SphereCollider>().radius = (Mathf.Abs(DistUpdate.x - DistUpdate.y) * ChildHolder.transform.localScale.x / 4) + ExtraCollisionScale;

            //// update the position of character collider
            //GetComponent<SphereCollider>().center = new Vector3((DistUpdate.x + DistUpdate.y) / 2,
            //    GetComponent<SphereCollider>().center.y, GetComponent<SphereCollider>().center.z);
        }

        public void InstantAddChilds(int amount) // this method for quick and instant add childs
        {
            SpawnChilds(amount + NumberOfChilds);
        }
        #endregion

        #region EndGame and Tower Settings
        /// ________________Tower Settings________________
        [BigHeader("Tower Settings")]
        [Tooltip("if the number of childs is too much for the tower, you can set a limit for the number of childs at endgame")]
        [Min(1f)] public int MaxChildsAtEndGame;

        [Tooltip("the maxmumm number of childs for each level (starting from 1)")]
        [Min(2f)] public int MaxChildsOnLevel;

        [Tooltip("times that the number of childs will be repated in next level (starting from 1)")]
        [Range(1, 10)] public int RepeatNumTimes;

        [Tooltip("Distance between each child horizontally")]
        [Range(0.1f, 3f)] public float TowerChildOffsetX;

        [Tooltip("Distance between each level vertically")]
        [Range(0.1f, 3f)] public float TowerChildOffsetY;

        [Tooltip("Delay between building each level in sceonds")]
        [Range(0.01f, 1f)] public float LevelBuildDelay = .075f;

        [Tooltip("Repated everytime a level built")] public AudioSource TowerBuildSFX;

        List<List<GameObject>> _towerLevelList = new(); // Hold tower levels and each level hold childs
        float _counterLevels = 0;

        IEnumerator EndLineTriggered()
        {
            if (NumberOfChilds > MaxChildsAtEndGame)
            {
                for (int i = MaxChildsAtEndGame; i < NumberOfChilds; i++) Destroy(ChildHolder.GetChild(i).gameObject);
                NumberOfChilds = ChildHolder.childCount;
            }

            for (int i = 0; i < ChildHolder.childCount; i++) // Change the movement system of the child... check PushTowardTower() inside child control
                ChildHolder.GetChild(i).gameObject.GetComponent<CharacterControl_Count_Child>().PushTowardTower();

            int numInRow = 1; int repeatlevelTimes = RepeatNumTimes; int childCounter = 0;
            while (childCounter <= NumberOfChilds)
            {
                List<GameObject> tmplist = new(); // hold the childs inside current level on tower
                float offset = TowerChildOffsetX * (numInRow - 1) / 2; // the ful scale offset start child and end child on level / 2
                yield return new WaitForSeconds(LevelBuildDelay);
                TowerBuildSFX.Play();
                foreach (List<GameObject> list in _towerLevelList) // every time building a level we push all prev levels up by TowerChildOffsetY
                {
                    Vector3 pos = list[0].transform.localPosition; pos.y += TowerChildOffsetY;
                    for (int i = 0; i < list.Count; i++)
                        list[i].transform.localPosition = new Vector3(list[i].transform.localPosition.x, pos.y, 0);
                }

                if (numInRow + childCounter > NumberOfChilds) // to make all levels full we calculate the reqired number of childs and add the missing childs for last level
                    for (int j = 0; j < numInRow + childCounter - NumberOfChilds; j++)
                        Instantiate(ChildPrefab, transform.position, Quaternion.identity, ChildHolder);

                for (int i = 0; i < numInRow; i++) // set the current level children offset
                {
                    ChildHolder.GetChild(i + childCounter).transform.localPosition = new Vector3(offset, 0.1f, 0);
                    ChildHolder.GetChild(i + childCounter).gameObject.GetComponent<CharacterControl_Count_Child>().StopMoving(true); // for instant push toward tower
                    tmplist.Add(ChildHolder.GetChild(i + childCounter).gameObject);
                    offset -= TowerChildOffsetX;
                }

                childCounter += numInRow; _counterLevels++;
                _towerLevelList.Add(tmplist);

                if (repeatlevelTimes-- == 1)
                {
                    if (MaxChildsOnLevel > numInRow) numInRow++;
                    repeatlevelTimes = RepeatNumTimes;
                }
            }
        }

        void MoveAtEndGame()
        {
            // after disable controls... Apply a new movement to character and Move the crowd toward the middle of the road... X axis to 0
            transform.position = new(Mathf.MoveTowards(transform.position.x, 0, MoveSpeedAtEnd * Time.deltaTime / 2),
                transform.position.y, transform.position.z + MoveSpeedAtEnd * Time.deltaTime);

            // Setup the next camera position
            _tmpPos = Vector3.MoveTowards(_tmpPos, new Vector3(CameraOffset.x, _camOffsetY + CameraOffset.y, CameraOffset.z), Time.deltaTime * CameraOffsetSpeed);

            // Apply the new camera position and rotation
            Camera.main.transform.SetPositionAndRotation(_startCamPos + transform.position + _tmpPos,
                Quaternion.RotateTowards(Camera.main.transform.rotation, Quaternion.Euler(CameraRotation), Time.deltaTime * CameraOffsetSpeed * 7));

            if (ChildHolder.childCount == 0) // after all childs hit the stairs
            {
                if (_endTrigger) OnWinEvent?.Invoke();
                else OnLoseEvent?.Invoke();
                _endTrigger = false; // Game Over // Multiplayer Staris done
            }
        }
        #endregion
    }
}
