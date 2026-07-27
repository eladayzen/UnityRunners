using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_Grab : MonoBehaviour
    {
        [BigHeader("Enable State")] // Behavior enables
        [Tooltip("A read only check if the system is working/Active or not\nyou can use EnableState(bool) to control")]
        [ReadOnly] public bool Enable; //A Quick readonly Check if this behavior is enabled or not

        [Tooltip("Enable on Start, or you can call EnableState(true) to enable this behavior")]
        public bool EnableOnStart; // main behavior checker

        [BigHeader("Control Settings")] 
        [Tooltip("the target object will be used in rotation")]
        public GameObject RotateModel; // the target will be used in rotation

        [Tooltip("the screen directions is  dependent on main camera Y axis rotation\n" +
        "On enable... the input direction will be fix to fit the screen directions")]
        public bool FixDirectionOnCameraAngle;

        [BigHeader("Grab Settings")]
        [Tooltip("Layer Mask for grabbable items")]
        public LayerMask GrabbableLayer; // Layer for grabbable items

        [Tooltip("The position of the item on grab state")]
        public Transform HoldPosition;

        [Tooltip("The forward throw force")]
        public float ThrowForce = 10f; // Force applied when throwing the item

        [BigHeader("Aim Ray Settings")]
        [Tooltip("the start point of ray is ShootPoint transform")]
        public bool EnableAimRay;

        [Tooltip("The start point of the ray")]
        public Transform StartPosition;

        [Tooltip("The material of ray")]
        public Material RayMaterial; // Material for the ray

        [Tooltip("For reflection ray renderer, add layer objects that will make ray reflect if collided with")]
        public LayerMask ReflectedLayers;
        [Tooltip("Layer objects that will force ray to stop / block it's path")]
        public LayerMask BlockLayers;
        [Tooltip("Number of reflections if max length not exceeded")]
        public int NumberOfReflections;
        [Tooltip("Max ray length")]
        public float MaxLenght;

        [Tooltip("Width of the ray")]
        public float RayWidth = 1f;

        [BigHeader("Effects")]
        public GameObject OnThrowEffect;

        [BigHeader("Events Section")]
        [Tooltip("Events when grab an item")]
        public UnityEvent OnGrab = new(); // Events when grab an item

        [Tooltip("Events when release an item")]
        public UnityEvent OnThrow = new(); // Events when release an item

        List<GameObject> _rayObject = new(); // GameObject representing the ray
        GameObject _grabbedObject; // Reference to the currently grabbed object
        Rigidbody _grabbedObjectRb; // Rigidbody of the grabbed object
        Vector2 _shootDirection;
        bool _throw;

        void Start()
        {
            EnableState(EnableOnStart); // apply EnableOnStart
        }

        public void EnableState(bool enable) // main behavior enable and disable controller
        {
            Enable = enable; // adjust this method as it fit to your behavior mechanism
            if (enable) // On Enable... do
            {

            }
            else // On Disable... do
            {
                OnInputReleased(false);
            }
        }

        void Update()
        {
            if (Enable) // main enable 
            {
                if (_grabbedObject) CreateRay();
            }
        }

        void CreateRay()
        {
            if (!_throw) return;
            Clear();

            float remainingLength = MaxLenght;
            Vector3 start = StartPosition.position;
            Vector3 currentDirec = new(_shootDirection.x, 0, _shootDirection.y);
            Vector3[] extraVertices = new Vector3[NumberOfReflections];

            for (int i = 0; i < NumberOfReflections; i++) // Loop for each reflection
            {
                // Create a new ray GameObject
                GameObject newRay = new("AimRay " + _rayObject.Count.ToString());
                _rayObject.Add(newRay);
                newRay.transform.position = start;

                // Add required components
                MeshFilter meshFilter = newRay.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = newRay.AddComponent<MeshRenderer>();
                meshRenderer.material = RayMaterial;

                // Initialize the mesh
                Mesh mesh = new();
                meshFilter.mesh = mesh;

                // Calculate the ray's end point
                Vector3 endPoint;
                Ray ray = new(start, currentDirec);
                Vector3 perpendicular = Vector3.Cross(currentDirec, Vector3.up).normalized * RayWidth;

                Quaternion direc = Quaternion.Euler(new Vector3(0, Mathf.Atan2(currentDirec.x, currentDirec.z) * 180 / Mathf.PI, 0));

                if (Physics.BoxCast(ray.origin, new Vector3(1, 0.01f, 0.01f) * RayWidth, ray.direction, out RaycastHit hit, direc, remainingLength, ReflectedLayers + BlockLayers))
                {
                    endPoint = ray.direction * hit.distance;
                    remainingLength -= hit.distance;

                    // Update the ray's starting point and direction for the next reflection
                    start = ray.origin + endPoint;

                    extraVertices[i] = hit.point - start;

                    Physics.SphereCast(ray.origin, RayWidth, ray.direction, out RaycastHit reflect, remainingLength + hit.distance, ReflectedLayers + BlockLayers);
                    currentDirec = Vector3.Reflect(ray.direction, reflect.normal);

                    // If the hit object is in the BlockLayers, stop the ray
                    if (BlockLayers == (BlockLayers | (1 << hit.collider.gameObject.layer))) break;
                }
                else
                {
                    endPoint = ray.direction * remainingLength;
                    remainingLength = 0;
                }

                // Define the vertices for the ray mesh
                Vector3[] vertices;
                if (i > 0)
                {
                    //foreach (Vector3 vec in extraVertices) print(vec);
                    vertices = new Vector3[5];
                    vertices[0] = perpendicular; // Bottom-left
                    vertices[1] = extraVertices[i - 1]; // Extra vertex from previous reflection
                    vertices[2] = -perpendicular; // Bottom-right
                    vertices[3] = endPoint + perpendicular; // Top-left
                    vertices[4] = endPoint - perpendicular; // Top-right
                }
                else
                {
                    vertices = new Vector3[4];
                    vertices[0] = perpendicular; // Bottom-left
                    vertices[1] = -perpendicular; // Bottom-right
                    vertices[2] = endPoint + perpendicular; // Top-left
                    vertices[3] = endPoint - perpendicular; // Top-right
                }

                // Define the triangles for the ray mesh
                int[] triangles;
                if (i > 0)
                {
                    triangles = new int[9]
                    {
                    0, 3, 2, // Triangle 1
                    2, 3, 4,  // Triangle 2
                    0, 2, 1  // Triangle 2
                    };
                }
                else
                {
                    // For 4 vertices, define 2 triangles (6 indices)
                    triangles = new int[6]
                    {
                    0, 2, 1, // Triangle 1
                    1, 2, 3  // Triangle 2
                    };
                }

                // Assign vertices and triangles to the mesh
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();

                // Stop if the remaining length is exhausted
                if (remainingLength <= 0) break;
            }
        }

        void Clear()
        {
            foreach (GameObject ray in _rayObject) Destroy(ray);
            _rayObject.Clear();
        }

        void ThrowItem()
        {
            if (_grabbedObject != null)
            {
                // Release the item
                _grabbedObject.transform.SetParent(null);
                _grabbedObject.transform.position = new Vector3(transform.position.x, _grabbedObject.transform.position.y, transform.position.z);

                _grabbedObjectRb.constraints = RigidbodyConstraints.None;

                _grabbedObjectRb.AddForce(new Vector3(_shootDirection.x, 0, _shootDirection.y) * ThrowForce, ForceMode.Impulse);
                StartCoroutine(ReturnCollision());

                Destroy(Instantiate(OnThrowEffect, _grabbedObject.transform.position, Quaternion.identity), 5);

                OnThrow?.Invoke();
            }
        }
        void OnTriggerEnter(Collider collision)
        {   
            if (GrabbableLayer == (GrabbableLayer | (1 << collision.gameObject.layer)))
            {
                _grabbedObject = collision.gameObject;
                _grabbedObjectRb = _grabbedObject.GetComponent<Rigidbody>();

                _grabbedObjectRb.constraints = RigidbodyConstraints.FreezeAll;

                _grabbedObject.transform.SetParent(HoldPosition);
                _grabbedObject.transform.localPosition = Vector3.zero;

                OnGrab?.Invoke();
            }
        }

        IEnumerator ReturnCollision()
        {
            yield return new WaitForSeconds(.1f);
            _grabbedObject = null;
            _grabbedObjectRb = null;
        }

        #region Input Getter
        /// <summary>
        /// the difference between OnInputDetected() and OnExceedMinDelta(Vector2 direction) on this behavior is
        /// if you want to start moving at the start of touching screen or after touch and move finger on sceen
        /// when touching the screen without moving the direction will be zero until move in screen so
        /// that OnInputDetected() caculate the current direction (rotation) and move at this direction
        /// and OnExceedMinDelta(Vector2 direction) noves charcter after touch and move on screen and you
        /// can set the minimum distanc has to be  move on sceen to enable this method using MinDelta attribute 
        /// go to controller behavior of this system and select which method you want to use
        /// set OnInputDetected() at on OnInputDetected event or OnEcxeedMinDelta(Vector2 direction) on OnExceedMinDelta event
        /// </summary>
        public void OnInputDetected()  // enable shoot and set direction to current rotation
        {
            _throw = true;  // reset all shoot data

            // calculate direction depending on current rotation
            _shootDirection = new Vector2(RotateModel.transform.TransformDirection(Vector3.forward).x, RotateModel.transform.TransformDirection(Vector3.forward).z);
        }

        public void OnExceedMinRange(Vector2 direction) // enable shoot and set direction to input direction
        {
            _throw = true;

            if (FixDirectionOnCameraAngle)
            {
                // fix angle direction depending on camera angle
                float angle = (Camera.main.transform.eulerAngles.y * Mathf.PI / 180) + Mathf.Atan2(direction.x, direction.y); 
                _shootDirection = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // setup the new fixed direction
            }
            else _shootDirection = direction; // or set direction = input direction
        }

        public void OnInputUpdated(Vector2 direction) // call it every time updating the direction
        {
            if (FixDirectionOnCameraAngle)
            {
                // fix angle direction depending on camera angle
                float angle = (Camera.main.transform.eulerAngles.y * Mathf.PI / 180) + Mathf.Atan2(direction.x, direction.y);
                _shootDirection = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // setup the new fixed direction
            }
            else _shootDirection = direction; // or set direction = input direction
        }

        public void OnInputReleased([Optional] bool exceeded) // exceeded used if you using delta scaler or the most controller, check Most-Controller
        {
            Clear();
            _throw = false; // diable shoot

            if(exceeded)
            {
                RotateModel.transform.LookAt(new Vector3(_shootDirection.x, 0, _shootDirection.y) + transform.position); // look at the shoot direction
                ThrowItem();
            }
        }
        #endregion
    }
}