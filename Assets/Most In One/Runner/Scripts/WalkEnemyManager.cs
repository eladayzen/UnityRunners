using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    public class WalkEnemyManager : MonoBehaviour
    {
        [Tooltip("Define the direction of the movement\nor rotate the object toward the movement direction")]
        [ReadOnly] public Vector3 Target; // Define the direction of the object when spawn or use rotation to calculate the direction...

        public bool FollowOnStart;
        public bool StartMove;
        public float MoveSpeed;
        public string FollowedTag;
        public Transform RoatateModel;

        [BigHeader("Animation Settings")]
        public Animator AnimationController;
        public string RunForward_bool, Defeated_bool;

        Transform _charFollow;
        bool _followTag;
        void Start()
        {
            if (!RoatateModel) RoatateModel = transform;
            if (Target == Vector3.zero) Target = RoatateModel.TransformDirection(Vector3.forward);
            RoatateModel.LookAt(Target.normalized + transform.position);
            if (FollowOnStart) StartFollow();
        }

        // Update is called once per frame
        void Update()
        {
            if (StartMove)
            {
                if (!_followTag)
                {
                    AnimationController.SetBool(RunForward_bool, true);
                    transform.position += MoveSpeed * Time.deltaTime * Target;
                }
                else
                {
                    if (_charFollow && _charFollow.gameObject.GetComponent<Collider>().enabled)
                    {
                        Vector3 target = -transform.position + _charFollow.position; target.y = 0;
                        RoatateModel.rotation = Quaternion.LookRotation(target);
                        transform.position += Mathf.Abs(MoveSpeed) * Time.deltaTime * RoatateModel.TransformDirection(Vector3.forward);
                    }
                    else
                    {
                        List<GameObject> chars = GameObject.FindGameObjectsWithTag(FollowedTag).ToList();
                        chars.RemoveAll(item => item.GetComponent<Collider>().enabled == false);
                        if (chars.Count > 0)
                        {
                            _charFollow = chars[0].transform;
                            AnimationController.SetBool(RunForward_bool, true);
                        }
                        else
                        {
                            _charFollow = null;
                            AnimationController.SetBool(RunForward_bool, false);
                        }
                    }
                }
            }
        }

        public void StartFollow()
        {
            _followTag = true;
            List<GameObject> chars = GameObject.FindGameObjectsWithTag(FollowedTag).ToList();
            chars.RemoveAll(item => item.GetComponent<Collider>().enabled == false);
            if (chars.Count > 0)
            {
                _charFollow = chars[0].transform;
                AnimationController.SetBool(RunForward_bool, true);
            }
            else
            {
                _charFollow = null;
                AnimationController.SetBool(RunForward_bool, false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == 10)
            {
                StartFollow();
            }
        }

        public void DestroyChild() // remove this child
        {
            StartMove = false;
            AnimationController.SetBool(Defeated_bool, true);
            GetComponent<MOST_Damage>().InstantKill();
        }

        public void ResetData()
        {
            AnimationController.SetBool(Defeated_bool, false);
            GetComponent<MOST_Damage>().Revive();
            StartMove = true;
            RoatateModel.LookAt(Target.normalized + transform.position);
            if (FollowOnStart) StartFollow();
        }
    }
}