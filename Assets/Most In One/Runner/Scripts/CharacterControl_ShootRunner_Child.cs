using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_ShootRunner_Child : MonoBehaviour
    {
        [HideInInspector] public Vector3 IdleTargetPos;

        [BigHeader("Main Settings")]
        public float ToPositionDelta;
        public float JumpForce;

        [BigHeader("Projectile Spawn Settings")]
        public bool IsActive;
        [Required] public GameObject Bullet;
        [Required] public Transform SpawnPoint;
        [InnerHint("(Optional)")] public ParticleSystem MuzzleFlare;
        [Min(.01f)] public float SpawnSpeed;
        public Vector2 StartingDelay;
        public float LifeTime = 1;

        [BigHeader("Object Pooling")]
        public bool UseObjectPool;

        [InnerHint("(Optional)")]
        [Tooltip("Optional parent to keep pooled bullets tidy when they deactivate.")]
        public Transform PoolRoot;

        [BigHeader("Animation Settings")]
        public Animator AnimationController;
        public string RunForward_bool, RunRight_bool, RunLeft_bool, Defeated_bool;

        Vector3 _startPos, _targetPos;
        float _easeDelta = 1, _shootCooldown = 0;

        static readonly Dictionary<GameObject, List<GameObject>> _poolByPrefab = new();
        static readonly Dictionary<GameObject, int> _lifeTokenByInstance = new();
        static int _lifeTokenSeed;

        void Update()
        {
            if (IsActive && _shootCooldown <= 0)
            {
                SpawnBullet();
                _shootCooldown = 1f / Mathf.Max(SpawnSpeed, 0.0001f);
            }
            else _shootCooldown -= Time.deltaTime;

            if (_easeDelta != 1)
            {
                _easeDelta = Mathf.Min(1, _easeDelta + Time.deltaTime / ToPositionDelta);
                transform.localPosition = _startPos - _targetPos * (1 - Mathf.Pow(1 - _easeDelta, 3));
            }
        }

        public void Shoot()
        {
            SpawnBullet();
        }

        void SpawnBullet()
        {
            if (!Bullet || !SpawnPoint) return;

            GameObject bulletObj;

            if (!UseObjectPool)
            {
                bulletObj = Instantiate(Bullet, SpawnPoint.position, transform.rotation);
                Destroy(bulletObj, LifeTime);
            }
            else
            {
                bulletObj = GetOrCreatePooled(Bullet, SpawnPoint.position, transform.rotation);

                // Lifetime: deactivate after time (token prevents disabling after reuse)
                int token = ++_lifeTokenSeed;
                _lifeTokenByInstance[bulletObj] = token;
                StartCoroutine(DeactivateAfterLifetime(bulletObj, LifeTime, token, PoolRoot));
            }

            if (MuzzleFlare) MuzzleFlare.Play();
        }

        static GameObject GetOrCreatePooled(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (!_poolByPrefab.TryGetValue(prefab, out var list))
            {
                list = new List<GameObject>(32);
                _poolByPrefab.Add(prefab, list);
            }

            // Find an inactive one
            for (int i = 0; i < list.Count; i++)
            {
                var obj = list[i];

                if (obj == null)
                {
                    list.RemoveAt(i--);
                    continue;
                }

                if (!obj.activeInHierarchy)
                {
                    obj.transform.SetPositionAndRotation(pos, rot);
                    obj.SetActive(true);
                    return obj;
                }
            }

            // None available -> create new
            var inst = Instantiate(prefab, pos, rot);
            list.Add(inst);
            return inst;
        }

        static IEnumerator DeactivateAfterLifetime(GameObject bulletObj, float lifeTime, int token, Transform poolRoot)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, lifeTime));

            if (bulletObj == null) yield break;

            // Token check prevents an old coroutine from disabling a reused bullet
            if (_lifeTokenByInstance.TryGetValue(bulletObj, out int current) && current == token)
            {
                bulletObj.SetActive(false);
                if (poolRoot) bulletObj.transform.SetParent(poolRoot, false);
            }
        }

        public void EditIdlePos(Vector3 target, bool gameStarted)
        {
            if (gameStarted) StartShooting();
            _startPos = transform.localPosition;
            IdleTargetPos = target;
            _targetPos = _startPos - target;
            _easeDelta = 0;
        }

        public void StartShooting([Optional] float cooldown)
        {
            IsActive = true;
            _shootCooldown = cooldown != 0 ? cooldown : Mathf.Max(0, Random.Range(StartingDelay.x, StartingDelay.y));
        }

        public void DestroyChild()
        {
            IsActive = false;
            transform.parent = null;
            AnimationController.SetBool(Defeated_bool, true);
            GetComponent<Collider>().enabled = false;
            GetComponent<Rigidbody>().isKinematic = true;
            GetComponent<MOST_Damage>().InstantKill();
        }

        public void SetAnimation(string type)
        {
            AnimationController.SetBool(RunForward_bool, false);
            AnimationController.SetBool(RunRight_bool, false);
            AnimationController.SetBool(RunLeft_bool, false);
            if (type == "Left") AnimationController.SetBool(RunLeft_bool, true);
            else if (type == "Right") AnimationController.SetBool(RunRight_bool, true);
            else if (type == "Forward") AnimationController.SetBool(RunForward_bool, true);
        }
    }
}
