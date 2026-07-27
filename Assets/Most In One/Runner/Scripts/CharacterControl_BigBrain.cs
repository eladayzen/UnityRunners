using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_BigBrain : MonoBehaviour
    {
        [BigHeader("Main")]
        [Tooltip("Is Moving and shooting\nCall Activate([Optional] float cooldown) or deactivate() to cntrol it")]
        [ReadOnly] public bool IsActive;

        [Tooltip("Character Head, (To be scaled object)")]
        [Required] public GameObject CharacterHead;

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
        [Tooltip("Event when endline object triggered")]
        public UnityEvent EndLineTriggerEvent;
        [Tooltip("Event When the level passed and win")]
        public UnityEvent OnWinEvent;
        [Tooltip("Event When the character defeated before level passed and lose")]
        public UnityEvent OnLoseEvent;

        [BigHeader("Animations")]
        [HelpBox("Optional animation controller, automatically adjusted inside character behavior\n" +
            "you can ignore it and use your custom anim system", HelpBoxKind.Info)]
        [InnerHint("(Optional)")] public Animator AnimationController;
        public string Run_bool, Win_bool, Defeated_bool, EndLineRun_bool;

        float _targetscale, _timer;
        Vector3 _tmpPos, _startCamPos;
        bool _endTrigger, _defeated;

        void Start()
        {
            _startCamPos = Camera.main.transform.position - transform.position;
            _targetscale = CharacterHead.transform.localScale.x;
        }
        void Update()
        {
            CharacterHead.transform.localScale = Vector3.MoveTowards(CharacterHead.transform.localScale, (.01f + _targetscale) * Vector3.one, Time.deltaTime * 10);
            if (IsActive) MoveAtEndGame();
        }

        public void Activate() // Called When Game Starts and character moves
        {
            IsActive = true;
            GetComponent<MOST_RoadMovement>().EnableState(true);
            if (AnimationController) AnimationController.SetBool(Run_bool, true);
        }

        public void Deactivate() // Called When Game Starts and character moves
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
            if (AnimationController) AnimationController.SetBool(Run_bool, false);
        }

        void MoveAtEndGame()
        {
            if (_endTrigger)
            {
                // after disable controls... Apply a new movement to character and Move the crowd toward the middle of the road... X axis to 0
                transform.position = new(Mathf.MoveTowards(transform.position.x, 0, MoveSpeedAtEnd * Time.deltaTime / 2),
                    transform.position.y, transform.position.z + MoveSpeedAtEnd * Time.deltaTime);

                // Setup the next camera position
                _tmpPos = Vector3.MoveTowards(_tmpPos, CameraOffset, Time.deltaTime * CameraOffsetSpeed);

                // Apply the new camera position and rotation
                Camera.main.transform.SetPositionAndRotation(_startCamPos + transform.position + _tmpPos,
                    Quaternion.RotateTowards(Camera.main.transform.rotation, Quaternion.Euler(CameraRotation), Time.deltaTime * CameraOffsetSpeed * 7));
            }
            else if (_defeated)
            {
                if (_timer > 1)
                {
                    transform.position += MoveSpeedAtEnd * 0.5f * Time.deltaTime * transform.TransformDirection(Vector3.forward);
                    transform.eulerAngles += 100 * Time.deltaTime * Vector3.up;
                }
                else _timer += Time.deltaTime;
            }
        }

        public void ScaleController(float amount, bool IsAdd) // IsAdd true if you want add amount to current scale... false if you want set new scale
        {
            if (IsAdd) _targetscale = Mathf.Max(0, _targetscale + amount / 40);
            else _targetscale = Mathf.Max(0, amount);
            if (_targetscale == 0) // Defeated
            {
                if (_endTrigger) OnWinEvent?.Invoke();
                else OnLoseEvent?.Invoke();
                GetComponent<MOST_RoadMovement>().EnableState(false);
                CharacterHead.transform.localScale = Vector3.zero;
                if (_endTrigger)
                {
                    _endTrigger = false;
                    if (AnimationController) AnimationController.SetBool(Win_bool, true);
                }
                else
                {
                    _defeated = true;
                    if (AnimationController) AnimationController.SetBool(Defeated_bool, true);
                    if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = false;
                }
            }
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Gate") && !_defeated) // Gate
            {
                if (other.transform.GetComponent<MOST_Gate>().IsCollected) return;
                ScaleController(other.transform.GetComponent<MOST_Gate>().Calculation(_targetscale), false);
            }
            else if (other.CompareTag("Enemy")) // Enemy Reduce
            {
                ThrowObstical(other.gameObject);
                if (other.gameObject.layer == 7) ScaleController(-15, true);
                else if (other.gameObject.layer == 8) ScaleController(-25, true);
                else if (other.gameObject.layer == 9) ScaleController(-40, true);
            }
            else if (EndLineMask == (EndLineMask | (1 << other.gameObject.layer)) && !_defeated) // EndPoint
            {
                GetComponent<MOST_RoadMovement>().EnableState(false);
                transform.eulerAngles = Vector3.zero;
                _endTrigger = true;
                if (AnimationController) AnimationController.SetBool(EndLineRun_bool, true);
                EndLineTriggerEvent?.Invoke();
            }
            else if (other.CompareTag("Score Object")) // EndPoint Obst Multiplayer
            {
                if (!_defeated) ScaleController(-35, true);
                ThrowObstical(other.gameObject);
            }
        }
        void ThrowObstical(GameObject obst)
        {
            obst.GetComponent<AudioSource>().Play();
            Vector3 force = new(Random.Range(4, 12) * (Random.Range(0, 100) > 50 ? 1 : -1), Random.Range(3, 10), Random.Range(10, 35));
            obst.GetComponent<Rigidbody>().AddForce(force, ForceMode.Impulse);
            obst.GetComponent<Rigidbody>().angularVelocity = force / 5;
            obst.GetComponent<Collider>().enabled = false;
        }

        public void InstantAddScale(float amount)
        {
            ScaleController(amount, true);
        }
    }
}
