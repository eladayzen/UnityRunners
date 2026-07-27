using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class ForwardMovement : MonoBehaviour
    {
        [Tooltip("Define the direction of the movement\nor rotate the object toward the movement direction")]
        [ReadOnly] public Vector3 Target; // Define the direction of the object when spawn or use rotation to calculate the direction...

        [BigHeader("Settings")]
        public bool Enabled = true;
        public float Speed;

        [BigHeader("Reflection Settings")]
        public bool EnableBounce;

        [Tooltip("Layers that will make the object reflect on collision")]
        public LayerMask ReflectLayer;
        [Min(0)] public int MaxReflections;

        [BigHeader("Collision Settings")]
        [Tooltip("Layers that will destroy the object when hit it")]
        public LayerMask BlockLayers;
        public GameObject HitPartical;

        [BigHeader("Animations")]
        public Animation OnMoveAnimations;

        void Start()
        {
            if (Target == Vector3.zero) Target = transform.TransformDirection(Vector3.forward);
            transform.LookAt(Target.normalized + transform.position);
        }
        void Update()
        {
            if(Enabled)
            {
                transform.position += Speed * Time.deltaTime * Target;
                if (OnMoveAnimations && !OnMoveAnimations.isPlaying) OnMoveAnimations.Play();
            }else if(OnMoveAnimations && OnMoveAnimations.isPlaying) OnMoveAnimations.Stop();
        }
        private void OnTriggerEnter(Collider obj)
        {
            if (ReflectLayer == (ReflectLayer | (1 << obj.gameObject.layer))) // Reflect Layer
            {
                if (!EnableBounce) return;
                Ray ray = new(transform.position + transform.TransformDirection(Vector3.back) * 2, Target);
                if (Physics.SphereCast(ray.origin,1, ray.direction, out RaycastHit hit, 3, ReflectLayer))
                {
                    transform.position = hit.point;
                    Target = Vector3.Reflect(Target, hit.normal);
                    transform.LookAt(Target + transform.position);
                }
                if (--MaxReflections < 0)
                {
                    if (HitPartical) Destroy(Instantiate(HitPartical, obj.ClosestPointOnBounds(transform.position), Quaternion.Euler(0, transform.eulerAngles.y + 180, 0)), 3);
                    Destroy(gameObject);
                }
            }
            else if (BlockLayers == (BlockLayers | (1 << obj.gameObject.layer))) // Block Layer
            {
                if (HitPartical) Destroy(Instantiate(HitPartical, obj.ClosestPointOnBounds(transform.position), Quaternion.Euler(0, transform.eulerAngles.y + 180, 0)), 3);
                Destroy(gameObject);
            }
        }
    }
}