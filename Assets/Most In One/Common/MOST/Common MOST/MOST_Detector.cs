using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_Detector : MonoBehaviour
    {
        public enum AreaShape { Sphere, Cube }

        [BigHeader("Detector Area Setting")]
        [Tooltip("The detecting area shape, Enable Debug to draw it")]
        public AreaShape Shape;

        [Tooltip("offset from the main object")]
        public Vector3 Center;

        [Tooltip("The Scale of the area")]
        public Vector3 Scale = Vector3.one;

        [BigHeader("Detector Targets")]
        [Tooltip("Layers that detecor will sense (must have a collider)")]
        public LayerMask TargetLayers;

        [Tooltip("Tags that detecor will sense (must have a collider)")]
        public string[] TargetTags;

        [Line(1, .99f, spacing: 6)]
        [Tooltip("if the detected obj ect is behind BlockLayers object or BlockLayers objects is" +
            " between the detector and detected object like walls... the system will ignore this detected object but you can access it from ignored list")]
        public bool ActiveOnlyOnSite;

        [Tooltip("layers of the block objects")]
        public LayerMask BlockLayers;

        [Tooltip("Using MOST_Damage, if the target has MOST_Damage system")]
        public bool IgnoreDefeatedTargets;

        [BigHeader("Collected Data")]
        [Tooltip("Current number of detected objects")]
        [ReadOnly] public int NumberOfDetectedObjects;

        [Tooltip("Current list of objects that already detected but blocked by BlockLayers objects")]
        [ReadOnly] public List<GameObject> InactiveObjects;

        [Tooltip("Current list of objects that detected")]
        [ReadOnly] public List<GameObject> DetectedObjects;

        [Tooltip("if enable: DetectedObjects list will be sorted depending on the closest object to the main object (detector)")]
        public bool SortByTheClosest; // if disable the sorting will set by detecting time

        [Tooltip("This object will take the same position of (marking) the closest Detected object")]
        [InnerHint("(Optional)")]public GameObject TargetCircle;

        [BigHeader("Rotation Settings")]
        [Tooltip("Look at the closeset detected object")]
        public bool LookAtTheClosest;

        [Tooltip("the target object will be used in rotation... Make Sure that no other behavior controls the rotation as well")]
        [ReadOnlyIf(nameof(LookAtTheClosest), false)]
        [Required("(Require If)")]
        public GameObject RotateModel;

        [Tooltip("Look at the closeset detected object")]
        [ReadOnlyIf(nameof(LookAtTheClosest), false)] [Min(0)]public float RotationSpeed;

        [BigHeader("Events Section")]
        [Tooltip("Event when a new object got detected\n(send this object)")]
        public UnityEvent<GameObject> OnObjectEnterEvent = new();
        [Tooltip("Event when a detected object leave the detector area or no longer detected\n(send this object)")]
        public UnityEvent<GameObject> OnObjectExitEvent = new();
        [Tooltip("Event when the empty detected list got a new detected object")]
        public UnityEvent OnDetectedListFill = new();
        [Tooltip("Event when the detected list got empty")]
        public UnityEvent OnDetectedListEmpty = new();

        [BigHeader("Data Send\" Section")]
        [Tooltip("Continusly send the current detected objects list")]
        public UnityEvent<GameObject[]> SendObjects = new();

        [Tooltip("Continusly send the current global positions of detected obejcts list")]
        public UnityEvent<Vector3[]> SendPositions = new();

        [Tooltip("delay between each event trigger\nOf course 0 means Time.deltaTime")]
        [GUIColor("cyan")]
        [Min(0)] public float DelayBetweenEachSend;

        GameObject _detector;
        Collider _trigger;
        Rigidbody _rigid;
        List<Vector3> positions = new();

        void Start()
        {
            _detector = new("Detector for(" + gameObject.name + ")");
            _trigger = Shape == AreaShape.Sphere ? _detector.AddComponent<SphereCollider>() : _detector.AddComponent<BoxCollider>();

            _rigid = _detector.AddComponent<Rigidbody>();
            _rigid.useGravity = false;
            _rigid.isKinematic = true;

            _trigger.isTrigger = true;

            _detector.transform.localScale = Scale * (Shape == AreaShape.Sphere ? 2 : 1);
            _detector.AddComponent<Detector_Core>().Initialize(this);

            _detector.layer = 31;

            if (TargetCircle) TargetCircle = Instantiate(TargetCircle, transform.position, TargetCircle.transform.rotation);
            StartCoroutine(ContinuesCall());
        }

        void Update()
        {
            _detector.transform.position = transform.position + Center;
            ListManage();
            if(LookAtTheClosest && DetectedObjects.Count >= 1 && DetectedObjects[0])
            {
                Vector2 direc = new Vector2(DetectedObjects[0].transform.position.x - RotateModel.transform.position.x,
                     DetectedObjects[0].transform.position.z - RotateModel.transform.position.z).normalized;
                RotateModel.transform.rotation = Quaternion.Slerp(RotateModel.transform.rotation,
                    Quaternion.Euler(180 * Mathf.Atan2(direc.x, direc.y) * Vector3.up / Mathf.PI), RotationSpeed * Time.deltaTime);
            }
        }

        #region Detector functions
        public void OnTriggerEnterReciver(Collider other)
        {
            bool compareTag = false;
            // Compare the collider tag to each tag in DamageTags
            foreach (string tag in TargetTags) if (other.gameObject.CompareTag(tag)) compareTag = true;
            if (compareTag || (TargetLayers == (TargetLayers | (1 << other.gameObject.layer))))
            {
                if (IgnoreDefeatedTargets && other.gameObject.GetComponent<MOST_Damage>() && other.gameObject.GetComponent<MOST_Damage>().IsDefeated()) return;
                if (!ActiveOnlyOnSite || !RayCheck(other.gameObject.transform.position))
                {
                    DetectedObjects.Add(other.gameObject); NumberOfDetectedObjects++;
                    OnObjectEnterEvent.Invoke(other.gameObject);
                }
                else if(ActiveOnlyOnSite) InactiveObjects.Add(other.gameObject);
            }
        }
        public void OnTriggerExitReciver(Collider other)
        {
            bool compareTag = false;
            // Compare the collider tag to each tag in DamageTags
            foreach (string tag in TargetTags) if (other.gameObject.CompareTag(tag)) compareTag = true;
            if (compareTag || (TargetLayers == (TargetLayers | (1 << other.gameObject.layer))))
            {
                if (InactiveObjects.Contains(other.gameObject)) InactiveObjects.Remove(other.gameObject);
                else
                {
                    NumberOfDetectedObjects--;
                    DetectedObjects.Remove(other.gameObject);
                    OnObjectExitEvent.Invoke(other.gameObject);
                }
            }
        }
        #endregion

        #region Detector Date manager
        bool RayCheck(Vector3 pos)
        {
            Ray ray = new(transform.position, (pos - transform.position).normalized);
            return Physics.Raycast(ray, Vector3.Distance(transform.position, pos), BlockLayers);
        }

        void ListManage()
        {
            int num = NumberOfDetectedObjects;

            DetectedObjects.RemoveAll(item => item == null);
            InactiveObjects.RemoveAll(item => item == null);

            DetectedObjects.RemoveAll(item => item.activeSelf == false);
            InactiveObjects.RemoveAll(item => item.activeSelf == false);

            if (IgnoreDefeatedTargets)
            {
                DetectedObjects.RemoveAll(item => item.GetComponent<MOST_Damage>() && item.GetComponent<MOST_Damage>().IsDefeated());
            }

            if (ActiveOnlyOnSite)
            {
                foreach (GameObject obj in InactiveObjects)
                {
                    if (!RayCheck(obj.transform.position))
                    {
                        DetectedObjects.Add(obj);
                        InactiveObjects.Remove(obj);
                        break;
                    }
                }
                foreach (GameObject obj in DetectedObjects)
                {
                    if (RayCheck(obj.transform.position))
                    {
                        DetectedObjects.Remove(obj);
                        InactiveObjects.Add(obj);
                        break;
                    }
                }
            }

            if (SortByTheClosest && DetectedObjects.Count > 1) DetectedObjects = DetectedObjects.OrderBy(val => Vector3.Distance
                (transform.position, val.transform.position)).ToList();
            if (TargetCircle)
            {
                TargetCircle.SetActive(DetectedObjects.Count > 0); // activate it
                if (DetectedObjects.Count > 0) TargetCircle.transform.position = DetectedObjects[0].transform.position;
            }
            NumberOfDetectedObjects = DetectedObjects.Count;

            if(num != NumberOfDetectedObjects)
            {
                if (num == 0) OnDetectedListFill.Invoke();
                else if (NumberOfDetectedObjects == 0) OnDetectedListEmpty.Invoke();
            }
        }

        IEnumerator ContinuesCall()
        {
            yield return new WaitForSeconds(Mathf.Max(Time.deltaTime, DelayBetweenEachSend));
            if (DetectedObjects.Count > 0)
            {
                SendObjects?.Invoke(DetectedObjects.ToArray());
                positions.Clear();
                foreach (GameObject obj in DetectedObjects) positions.Add(obj.transform.position);
                SendPositions?.Invoke(positions.ToArray());
            }
            StartCoroutine(ContinuesCall());
        }
        #endregion

        #region Public Methods

        public List<GameObject> GetAllDetectedObjects()
        {
            return DetectedObjects;
        }

        public List<GameObject> GetAllInactivedObjects()
        {
            return InactiveObjects;
        }

        public int GetNumberOfDetectedObjects()
        {
            return NumberOfDetectedObjects;
        }

        public GameObject GetTheClosestObject()
        {
            if (DetectedObjects.Count == 0) return null;
            else if (SortByTheClosest || DetectedObjects.Count == 1) return DetectedObjects[0];
            else
            {
                List<GameObject> clone = new(DetectedObjects);
                clone = clone.OrderBy(val => Vector3.Distance (transform.position, val.transform.position)).ToList();
                return clone[0];
            }
        }

        public List<Vector3> GetAllActiveObjectsPositions()
        {
            return positions;
        }

        /// <summary>
        /// Update the detector scale
        /// </summary>
        /// <param name="scale"></param>
        public void SetDetectorScale(Vector3 scale)
        {
            _detector.transform.localScale = scale;
        }

        /// <summary>
        /// the center acts as offset from the parent object (detector holder)
        /// </summary>
        /// <param name="point"></param>
        public void SetDetectorCenterPoint(Vector3 point)
        {
            Center = point;
        }

        #endregion

        #region Debug Setup
        [BigHeader("Debug Settings")]
        public bool EnableDebug = true;
        public Color EdgeColor = Color.black;

        void OnDrawGizmos()
        {
            if (EnableDebug)
                switch (Shape)
                {
                    case AreaShape.Cube:
                        DrawCube();
                        break;
                    case AreaShape.Sphere:
                        DrawSphere();
                        break;
                }
        }
        void DrawCube()
        {
            Vector3 p1 = Center + transform.position + transform.rotation * new Vector3(-Scale.x / 2, -Scale.y / 2, -Scale.z / 2);
            Vector3 p2 = Center + transform.position + transform.rotation * new Vector3(Scale.x / 2, -Scale.y / 2, -Scale.z / 2);
            Vector3 p3 = Center + transform.position + transform.rotation * new Vector3(Scale.x / 2, Scale.y / 2, -Scale.z / 2);
            Vector3 p4 = Center + transform.position + transform.rotation * new Vector3(-Scale.x / 2, Scale.y / 2, -Scale.z / 2);
            Vector3 p5 = Center + transform.position + transform.rotation * new Vector3(-Scale.x / 2, -Scale.y / 2, Scale.z / 2);
            Vector3 p6 = Center + transform.position + transform.rotation * new Vector3(Scale.x / 2, -Scale.y / 2, Scale.z / 2);
            Vector3 p7 = Center + transform.position + transform.rotation * new Vector3(Scale.x / 2, Scale.y / 2, Scale.z / 2);
            Vector3 p8 = Center + transform.position + transform.rotation * new Vector3(-Scale.x / 2, Scale.y / 2, Scale.z / 2);

            // Draw the cube
            Debug.DrawLine(p1, p2, EdgeColor);
            Debug.DrawLine(p2, p3, EdgeColor);
            Debug.DrawLine(p3, p4, EdgeColor);
            Debug.DrawLine(p4, p1, EdgeColor);
            Debug.DrawLine(p5, p6, EdgeColor);
            Debug.DrawLine(p6, p7, EdgeColor);
            Debug.DrawLine(p7, p8, EdgeColor);
            Debug.DrawLine(p8, p5, EdgeColor);
            Debug.DrawLine(p1, p5, EdgeColor);
            Debug.DrawLine(p2, p6, EdgeColor);
            Debug.DrawLine(p3, p7, EdgeColor);
            Debug.DrawLine(p4, p8, EdgeColor);
        }
        void DrawSphere()
        {
            int segments = 32; // Number of segments to approximate a circle
            for (int i = 0; i < segments; i++)
            {
                float theta1 = i * 2 * Mathf.PI / segments; // Angle for the current segment
                float theta2 = (i + 1) * 2 * Mathf.PI / segments; // Angle for the next segment

                // XY Plane (Z = 0)
                Vector3 xyCircle1 = new(Mathf.Cos(theta1) * Scale.x, Mathf.Sin(theta1) * Scale.y, 0);
                Vector3 xyCircle2 = new(Mathf.Cos(theta2) * Scale.x, Mathf.Sin(theta2) * Scale.y, 0);

                // XZ Plane (Y = 0)
                Vector3 xzCircle1 = new(Mathf.Cos(theta1) * Scale.x, 0, Mathf.Sin(theta1) * Scale.z);
                Vector3 xzCircle2 = new(Mathf.Cos(theta2) * Scale.x, 0, Mathf.Sin(theta2) * Scale.z);

                // YZ Plane (X = 0)
                Vector3 yzCircle1 = new(0, Mathf.Cos(theta1) * Scale.y, Mathf.Sin(theta1) * Scale.z);
                Vector3 yzCircle2 = new(0, Mathf.Cos(theta2) * Scale.y, Mathf.Sin(theta2) * Scale.z);

                // Draw the circles
                Debug.DrawLine(Center + transform.position + transform.rotation * xyCircle1, Center + transform.position + transform.rotation * xyCircle2, EdgeColor); // XY Plane
                Debug.DrawLine(Center + transform.position + transform.rotation * xzCircle1, Center + transform.position + transform.rotation * xzCircle2, EdgeColor); // XZ Plane
                Debug.DrawLine(Center + transform.position + transform.rotation * yzCircle1, Center + transform.position + transform.rotation * yzCircle2, EdgeColor); // YZ Plane
            }
        }
        #endregion
    }
}

