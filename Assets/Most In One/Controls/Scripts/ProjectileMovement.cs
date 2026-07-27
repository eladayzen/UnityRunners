using System.Runtime.InteropServices;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    // This Behaviour for fixed position projectiles that has a start point and end point
    public class Projectile_Movement : MonoBehaviour
    {
        [Tooltip("The point that projectile will land on")]
        public Vector3 TargetPosition;

        [Tooltip("Max Y axis point on projectile move")]
        public float MaxHeight;

        [Tooltip("Time to reach TargetPosition starting from StartPosition")]
        public float HitTime;

        [Tooltip("Using random vector and multiply it by rot speed to give a random rotation")]
        public float RandomRotationSpeed;

        public bool UseGravityAtEnd = true;

        float _time;
        Vector3 _startPoint, _controlPoint, _randomRotVector;
        Transform _target;

        public void Activate([Optional] Transform target) // Call this method to activate projectile
        {
            _target = target;
            if (_target) TargetPosition = _target.position;
            _startPoint = transform.position;
            _randomRotVector = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * Random.Range(.4f * RandomRotationSpeed, RandomRotationSpeed);
            _controlPoint = (_startPoint + TargetPosition) / 2 + Vector3.up * MaxHeight;
        }
        void Update()
        {
            if(_target) TargetPosition = _target.position;
            _controlPoint = (_startPoint + TargetPosition) / 2 + Vector3.up * MaxHeight;

            _time += Time.deltaTime / HitTime;
            Vector3 m1 = Vector3.Lerp(_startPoint, _controlPoint, _time);
            Vector3 m2 = Vector3.Lerp(_controlPoint, TargetPosition, _time);
            transform.position = Vector3.Lerp(m1, m2, _time);
            transform.Rotate(30 * Time.deltaTime * _randomRotVector);
            if (_time >= 1)
            {
                if (UseGravityAtEnd && GetComponent<Rigidbody>())
                {
                    GetComponent<Rigidbody>().useGravity = true;
                    GetComponent<Rigidbody>().isKinematic = false;
                    GetComponent<Rigidbody>().linearVelocity = 2 * MaxHeight * (transform.position - _controlPoint).normalized / HitTime;
                }
                Destroy(this); // destroy this behavior after reaching hit point... after that this behavior is useless
            }
        }
    }
}
