using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_Count_Child : MonoBehaviour
    {
        [HideInInspector] public Vector3 IdleTargetPos;

        [BigHeader("Main Settings")]
        [Tooltip("the time reqired to move from point to anther (better than fixed speed in spawn objects)")]
        public float ToPositionDelta;
        public float JumpForce;

        public Animator AnimationController;

        [BigHeader("Splat Prefabs")]
        public GameObject[] SplatList;
        public GameObject[] EnemySplatList;

        GameObject _parentObj, _holderObj;
        Vector3 _startPos, _targetPos;
        float _easeDelta; // if ease delta == 1...child not moving
        bool _defeated;

        void Start()
        {
            _holderObj = transform.parent.gameObject; // Child Holder Object
            _parentObj = transform.parent.parent.gameObject; // Character
        }

        void Update()
        {
            if (_easeDelta != 1)
            {
                _easeDelta = Mathf.Min(1, _easeDelta + Time.deltaTime / ToPositionDelta);
                transform.localPosition = _startPos - _targetPos * (1 - Mathf.Pow(1 - _easeDelta, 3)); // this represent easing function Out Cubic
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == 11) // Jump Platform
            {
                GetComponent<Rigidbody>().linearVelocity = Vector3.up * JumpForce;
            }
            else if (other.CompareTag("Enemy"))
            {
                if (_defeated) return;
                if (other.gameObject.layer == 8) // EnemyChar // Type 2
                {
                    if (other.gameObject.GetComponent<DamageAmountSetter>())
                    {
                        if (other.gameObject.GetComponent<DamageAmountSetter>().DamageAmount == 0) return;
                        other.gameObject.GetComponent<DamageAmountSetter>().DamageAmount = 0;
                        _defeated = true;
                    }
                    else return;

                    Instantiate(EnemySplatList[Random.Range(0, EnemySplatList.Length)], other.gameObject.transform.position + Vector3.up * .05f, Quaternion.Euler(new Vector3(90, 0, 0)));
                    Destroy(other.gameObject);
                    DestroyChild();
                }
                if (other.gameObject.layer == 9) // Type 3
                {
                    DestroyChild();
                }
            }
            else if (other.CompareTag("Score Object")) // Staris at end game
            {
                transform.SetParent(null);
                AnimationController.SetBool("Run", false);
            }
        }

        public void EditIdlePos(Vector3 target, bool RunAnim) // change the current child position
        {
            AnimationController.SetFloat("RandomSpeed", Random.Range(.8f, 1.2f)); //  randomize animation speed for better look
            AnimationController.SetBool("Run", RunAnim);
            _startPos = transform.localPosition;
            IdleTargetPos = target;
            _targetPos = _startPos - target;
            _easeDelta = 0;
        }

        public void PushTowardTower() // all childs push toward start tower position ( 0 at z axiz);
        {
            _startPos = transform.localPosition;
            _targetPos = _startPos; _targetPos.x = 0;
            ToPositionDelta *= 8;
            _easeDelta = 0;
            GetComponent<Rigidbody>().isKinematic = true; // stop all phy to build the tower
            GetComponent<Rigidbody>().useGravity = false; // stop all phy to build the tower
        }

        public void StopMoving(bool RunAnimationState) // force stop moving childs and edit the animations 
        {
            _easeDelta = 1;
            EditAnimation(RunAnimationState);
        }

        public void EditAnimation(bool move) // quick animation edit idle and run
        {
            AnimationController.SetBool("Run", move);
        }

        public void DestroyChild() // remove this child
        {
            transform.parent = null; // unparent this child
            if (!_holderObj.GetComponent<AudioSource>().isPlaying) _holderObj.GetComponent<AudioSource>().Play(); // child destroy sound effect
            Vector3 SpawnPos = transform.position + Vector3.forward * Random.Range(-.5f, .5f) + Vector3.right * Random.Range(-.5f, .5f); // random splat position
            Instantiate(SplatList[Random.Range(0, SplatList.Length)], SpawnPos + Vector3.up * .05f, Quaternion.Euler(new Vector3(90, 0, 0))); // spawn splat
            _parentObj.GetComponent<CharacterControl_Count>().UpdateChildNumber(); // update child number at main character
            Destroy(gameObject);  // now destroy it
        }
    }
}