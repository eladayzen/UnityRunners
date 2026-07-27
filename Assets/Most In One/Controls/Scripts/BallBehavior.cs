using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    public class BallBehavior : MonoBehaviour
    {
        [ReadOnly] public bool IsAttached;
        public float ForwardRotSpeed;

        public Transform Pointer;
        public Vector3 Offset;
        public bool FollowOnX;
        public bool FollowOnY;
        public bool FollowOnZ;
        public Vector3 RotateSpeed;

        public Transform StartPoint;
        public GameObject BlueScoreArea;
        public GameObject RedScoreArea;

        public GameObject OnScoreEffect;
        public Animation OnScoreEffectUI;

        [Tooltip("Event WWhen Score on Blue")]
        public UnityEvent OnScoreBlue = new(); // Events when Cycle Starts

        [Tooltip("Event WWhen Score on Red")]
        public UnityEvent OnScoreRed = new(); // Events when Cycle Ends

        Rigidbody rb;
        float _RotSpeed;
        bool _isRotated;
        Vector3 _idleScale;
        void Start()
        {
            _idleScale = transform.localScale;
            rb = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void Update()
        {
            Pointer.position = new Vector3(FollowOnX ? transform.position.x + Offset.x : Pointer.position.x,
                                             FollowOnY ? transform.position.y + Offset.y : Pointer.position.y,
                                             FollowOnZ ? transform.position.z + Offset.z : Pointer.position.z);
            Pointer.eulerAngles += 60 * Time.deltaTime * RotateSpeed;

            if (_isRotated) _RotSpeed = ForwardRotSpeed * 10;
            else _RotSpeed = Mathf.MoveTowards(_RotSpeed, 0, _RotSpeed * Time.deltaTime * 3);

            if (IsAttached) transform.Rotate(_RotSpeed * Time.deltaTime * Vector3.right);
            else rb.AddForce(-rb.linearVelocity * Time.deltaTime);
        }

        public void OnInputUpdated(Vector2 direction) // call it every time updating the direction
        {
            if (direction.magnitude != 0) // apply only vallied direction // for stop moving use OnInputReleased()
            {
                _isRotated = true;
            }
        }

        public void OnInputReleased() // exceeded used if you using delta scaler or the most controller, check Most-Controller
        {
            _isRotated = false;
            if (GetComponent<TrailRenderer>()) GetComponent<TrailRenderer>().enabled = false;
        }

        void OnCollisionEnter(Collision collision)
        {
            if(collision.gameObject == BlueScoreArea)
            {
                OnScoreBlue?.Invoke();
                Destroy(Instantiate(OnScoreEffect, transform.position, Quaternion.identity), 5);
                OnScoreEffectUI.Play();
                ResetBall();
            }
            else if (collision.gameObject == RedScoreArea)
            {
                OnScoreRed?.Invoke();
                Destroy(Instantiate(OnScoreEffect, transform.position, Quaternion.identity), 5);
                OnScoreEffectUI.Play();
                ResetBall();
                transform.position = StartPoint.position;
            }
        }

        public void ResetBall()
        {
            IsAttached = false;
            transform.SetParent(null);
            
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.eulerAngles = Vector3.zero;
            transform.localScale = _idleScale;

            if (GetComponent<TrailRenderer>()) GetComponent<TrailRenderer>().enabled = true;
        }

        public void AttachBall()
        {
            IsAttached = true;
            if (GetComponent<TrailRenderer>()) GetComponent<TrailRenderer>().enabled = false;
        }
    }
}
