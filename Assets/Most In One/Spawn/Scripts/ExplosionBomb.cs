using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class ExplosionBomb : MonoBehaviour
    {
        public enum ExplosionType { Four_Directions_Tile, Sphere }
        [BigHeader("Main Settings")]
        [Tooltip("the type of explosion")] public ExplosionType ExplosionDirection;
        public bool StartCountDownAtStart = true;

        [BigHeader("Damage Objects")]
        [Tooltip("this will be used in Four_Directions explosion")] public GameObject SquareExplosionPrefab;
        [Tooltip("this will be used in Sphere explosion")] public GameObject SphereDamagePrefab;

        [BigHeader("Explosion Settings")]
        [Tooltip("Scale of explosion\nthis will applied at SphereDamage object scale\nand number of SquareExplosionPrefab Tiles")]
        [Min(1)] public float ExplosionScale;

        [Tooltip("Delay before explosion")][Min(.1f)] public float ExplosionStartDelay;
        [Tooltip("The speed of explosion wave")]
        [Min(.1f)] public float ExplosionSpeed;

        [Tooltip("time that explosion damage area will be active after explosion")]
        [Min(.1f)] public float ExplosionCollisionDuration;

        [Tooltip("applied at Four_Directions\nthese objects will stop explosion from spreading")]
        [HideIfAny(nameof(ExplosionDirection),ExplosionType.Four_Directions_Tile,false)]
        public LayerMask BlockLayers;

        [BigHeader("Tick-Tack")]
        public float InitialTickInterval = 1.0f; // Starting speed
        public float MaxedTickInterval = 0.1f;   // Maximum speed
        public float StartingAccelerationIn = 3;
        public float AccelerationRate = .1f;

        [BigHeader("Events")]
        public UnityEvent OnStartCounting;
        public UnityEvent TickInterval;
        public UnityEvent OnExplosion;

        float _tickTimer, _explosionTimer;
        bool _right, _left, _forward, _back, _exploded;
        GameObject _damageSphere;

        void Start()
        {
            
            if (StartCountDownAtStart) StartCoroutine(Explosion(true));
        }

        void Update()
        {
            if(_exploded)
            {
                if (_damageSphere) _damageSphere.transform.localScale =
                        Vector3.MoveTowards(_damageSphere.transform.localScale, Vector3.one * ExplosionScale, ExplosionSpeed * Time.deltaTime);
            }
        }

        IEnumerator TickTack()
        {
            yield return new WaitForSeconds(_tickTimer);
            _explosionTimer -= _tickTimer;
            if (_explosionTimer <= 0 || _exploded) yield break;

            TickInterval?.Invoke();
            if (_explosionTimer <= ExplosionStartDelay - StartingAccelerationIn)
                _tickTimer = Mathf.Max(_tickTimer - AccelerationRate, MaxedTickInterval);
            StartCoroutine(TickTack());
        }

        IEnumerator Explosion(bool withDelay)
        {
            _explosionTimer = ExplosionStartDelay;
            _tickTimer = InitialTickInterval;
            StartCoroutine(TickTack());
            OnStartCounting?.Invoke();

            yield return new WaitForSeconds(withDelay ? ExplosionStartDelay : Time.deltaTime);
            if (_exploded) yield break; // break if the explosion already occured

            OnExplosion?.Invoke();

            int CurrentJump = 1;
            _exploded = true;

            if(GetComponent<Collider>()) GetComponent<Collider>().enabled = false;
            transform.eulerAngles = Vector3.zero;

            if (GetComponent<Projectile_Movement>()) Destroy(GetComponent<Projectile_Movement>());
            // Spawn explosion effect at the original position first ...

            if (ExplosionDirection == ExplosionType.Four_Directions_Tile)
            {
                Destroy(Instantiate(SquareExplosionPrefab, transform.position, Quaternion.Euler(0, 90, 0)), ExplosionCollisionDuration);
                Destroy(Instantiate(SquareExplosionPrefab, transform.position, Quaternion.Euler(0, 0, 0)), ExplosionCollisionDuration);

                while (ExplosionScale > CurrentJump) // ... and then expand the explosion
                {
                    yield return new WaitForSeconds(1 / ExplosionSpeed);
                    if (!_right) // Check obsticals and spawn explosion for each square on Right
                    {
                        Ray ray = new(transform.position, Vector3.right);
                        if (Physics.Raycast(ray.origin, ray.direction, out _, CurrentJump, BlockLayers)) _right = true;
                        StartCoroutine(SquareExplosionState(Instantiate(SquareExplosionPrefab, transform.position + Vector3.right * CurrentJump,
                            Quaternion.Euler(0, 90, 0)), _right));
                    }
                    if (!_left) // Check obsticals and spawn explosion for each square on Left
                    {
                        Ray ray = new(transform.position, Vector3.left);
                        if (Physics.Raycast(ray.origin, ray.direction, out _, CurrentJump, BlockLayers)) _left = true;
                        StartCoroutine(SquareExplosionState(Instantiate(SquareExplosionPrefab, transform.position + Vector3.left * CurrentJump,
                           Quaternion.Euler(0, 90, 0)), _left));
                    }
                    if (!_forward) // Check obsticals and spawn explosion for each square on Forward
                    {
                        Ray ray = new(transform.position, Vector3.forward);
                        if (Physics.Raycast(ray.origin, ray.direction, out _, CurrentJump, BlockLayers)) _forward = true;
                        StartCoroutine(SquareExplosionState(Instantiate(SquareExplosionPrefab, transform.position + Vector3.forward * CurrentJump,
                           Quaternion.Euler(0, 0, 0)), _forward));
                    }
                    if (!_back) // Check obsticals and spawn explosion for each square on Back
                    {
                        Ray ray = new(transform.position, Vector3.back);
                        if (Physics.Raycast(ray.origin, ray.direction, out _, CurrentJump, BlockLayers)) _back = true;
                        StartCoroutine(SquareExplosionState(Instantiate(SquareExplosionPrefab, transform.position + Vector3.back * CurrentJump,
                           Quaternion.Euler(0, 0, 0)), _back));
                    }
                    CurrentJump++;
                }
            }
            else
            {
                _damageSphere = Instantiate(SphereDamagePrefab, transform.position, Quaternion.identity);
                _damageSphere.transform.localScale = Vector3.one * ExplosionScale/5;
                Destroy(_damageSphere, ExplosionCollisionDuration);
            }
            
            Destroy(gameObject, ExplosionCollisionDuration * 3 + 3); // auto destruct of the game object
        }
        public void ExplodeNow() // Called for instant distruction
        {
            if (!_exploded) StartCoroutine(Explosion(false));
        }
        IEnumerator SquareExplosionState(GameObject square, bool end)
        {
            ParticleSystem.MainModule module = square.GetComponent<ParticleSystem>().main;
            module.startLifetime = ExplosionCollisionDuration; // set the partical lifetime
            yield return new WaitForSeconds(end ? Time.deltaTime * 10 : ExplosionCollisionDuration); // delay
            square.GetComponent<Collider>().enabled = false; // disable explosion collision area
            Destroy(square, ExplosionCollisionDuration * 2); // give a time before destruct (if there is effect or animation to finish)
        }
    }
}

