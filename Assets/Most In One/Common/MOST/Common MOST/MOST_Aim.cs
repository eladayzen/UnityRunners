using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class MOST_Aim : MonoBehaviour
    {
        public enum AimType { NoRay, Throw, DirectSquare, SemiCircle, DirectRay }
        public enum DrawingType { FullCircle, Outline }
        public enum RotationSettings { NoRotation, ApplyOnDirectionUpdate, ApplyOnRelease }
        public enum RotationType { InstantLookAt, WithSpeed }

        [BigHeader("Enable State")]
        [Tooltip("Read-only status indicating whether this behavior is currently enabled.\nUse EnableState(bool) to toggle.")]
        [ReadOnly] public bool Enable;

        [Tooltip("Enable this behavior at Start(). You can also enable/disable later via EnableState(true/false).")]
        public bool EnableOnStart = true;

        [BigHeader("Rotation Settings")]
        [Tooltip("When to apply rotation to RotateModel: never, while aiming (direction updates), or on release.")]
        public RotationSettings RotationState;

        [HideIfAny(nameof(RotationState), RotationSettings.ApplyOnDirectionUpdate, false)]
        [Tooltip("How to rotate the model: look instantly at the target or rotate with a speed.")]
        public RotationType RotationWay;

        [Tooltip("Target object whose rotation will be driven by the aim system.")]
        [HideIfAny(nameof(RotationState), RotationSettings.NoRotation, true)]
        [Required("(Require if)")] public GameObject RotateModel;

        [HideIfAll(nameof(RotationState), RotationSettings.ApplyOnDirectionUpdate, false, nameof(RotationWay), RotationType.WithSpeed, false)]
        [Min(0), Tooltip("Degrees-per-second factor used when RotationWay == WithSpeed.")]
        public float RotateSpeed;

        [Tooltip("Optional movement system reference used to coordinate rotation/animation control (e.g., disable this rotation while dashing).")]
        [HideIfAny(nameof(RotationState), RotationSettings.NoRotation, true)]
        [GUIColor("green"), InnerHint("(Optional)")]
        public MOST_FreeMovement MoveSystem;

        [Tooltip("If enabled, input directions are aligned to the main camera's Y-axis when interpreted.")]
        [HideIfAny(nameof(RotationState), RotationSettings.NoRotation, true)]
        public bool FixDirectionOnCameraAngle = true;

        [BigHeader("AutoAim Settings")]
        [GUIColor("cyan"), Tooltip("Enable/disable Auto-Aim. You can also control it via AutoAimControl(bool).")]
        public bool EnableAutoAim;

        [ReadOnlyIf(nameof(EnableAutoAim), false)] public bool AutoSend = true;

        [HelpBox("Auto-Aim targets the first object in OnRangeEnemyList and invokes OnAimming(Vector2)." +
            " MOST_Detector can populate and sort this list automatically; otherwise, you can fill it manually.", HelpBoxKind.Info)]
        [Tooltip("Detector that maintains the in-range targets list for Auto-Aim. Optional—populate OnRangeEnemyList yourself if not used.")]
        [ReadOnlyIf(nameof(EnableAutoAim),false),InnerHint("(Require If)")] public MOST_Detector AimDetector;

        [Tooltip("Reference point used for aim calculations (often the projectile spawn).")]
        [ReadOnlyIf(nameof(EnableAutoAim), false),InnerHint("(Require If)")] public Transform CalculationPoint;

        [Tooltip("Current set of in-range targets considered for Auto-Aim.")]
        [HideIfAny(MOSTEdit.RuntimeOnly), ReadOnly] public List<GameObject> OnRangeEnemyList = new();

        [BigHeader("Drawing Settings")]
        [GUIColor("cyan"), Tooltip("Aiming/drawing mode: Throw parabola, direct ray, reflective square path, semi-circle, etc.")]
        public AimType Type;

        [Line(1, .99f, spacing: 6)]
        [Tooltip("Start position for rays/paths.")]
        [HideIfAny(nameof(Type), AimType.NoRay, true)]
        [Required] public Transform StartPosition;

        [Tooltip("Layers that cause reflective bounces for reflective modes.")]
        [HideIfAll(nameof(Type), AimType.DirectSquare, false, nameof(Type), AimType.DirectRay, false)]
        public LayerMask ReflectedLayers;

        [Tooltip("Layers that block and terminate the ray/path.")]
        [HideIfAny(nameof(Type), AimType.NoRay, true, nameof(Type), AimType.Throw, true)]
        public LayerMask BlockLayers;

        [Tooltip("Maximum number of reflections (if remaining length permits).")]
        [HideIfAll(nameof(Type), AimType.DirectSquare,false, nameof(Type), AimType.DirectRay, false)]
        [Min(0)] public int NumberOfReflections;

        [Tooltip("Maximum ray length (world units).")]
        [HideIfAll(nameof(Type), AimType.DirectSquare,false, nameof(Type), AimType.DirectRay, false)]
        [Min(0)] public float MaxLenght = 10;

        [Tooltip("Maximum distance from the start point to the target for Throw mode.")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        [Min(0)] public float MaxDistance = 10;

        [Tooltip("Target Y position used for the Throw arc’s landing point.")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        public float TargetInYAxis = .01f;

        [Tooltip("Maximum arc height (Y) above the start for Throw mode.")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        [Min(0)] public float MaxHight = 8;

        [Tooltip("If enabled, scales MaxHight by input magnitude (deltaScale).")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        public bool DynamicHight = true;

        [Tooltip("Material used for ray/shape rendering.")]
        [HideIfAll(nameof(Type), AimType.SemiCircle, true, nameof(DrawType), DrawingType.Outline, true)]
        [HideIfAny(nameof(Type), AimType.NoRay, true)]
        [Line(1, .99f, spacing: 6)]
        [Required] public Material RayMaterial;

        [Tooltip("Color used for ray/shape rendering.")]
        [HideIfAny(nameof(Type), AimType.SemiCircle, false, nameof(DrawType), DrawingType.Outline, false)]
        [Required] public Color RayColor = Color.white;

        [Tooltip("Drawing style for SemiCircle: a filled mesh (FullCircle) or textured line (LineTexture).")]
        [HideIfAny(nameof(Type), AimType.SemiCircle, false)]
        public DrawingType DrawType;

        [Tooltip("Width curve for LineRenderer when using Throw mode.")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        public AnimationCurve DrawWidth = AnimationCurve.EaseInOut(0f, 1f, 1f, 1f);

        [Tooltip("Number of segments used to approximate shapes/curves.")]
        [HideIfAll(nameof(Type), AimType.Throw,false, nameof(Type), AimType.SemiCircle, false)]
        [Min(1)] public int DrawSegments = 18;

        [Tooltip("Optional marker shown at the end/impact point (e.g., sprite or prefab).")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        public GameObject HitPointSpot;

        [Tooltip("Line width for ray renderers (world units).")]
        [HideIfAll(nameof(Type), AimType.DirectSquare,false, nameof(Type), AimType.DirectRay, false)]
        [Min(0)] public float RayWidth = 1f;

        [Tooltip("Outer radius for SemiCircle shapes.")]
        [HideIfAny(nameof(Type), AimType.SemiCircle, false)]
        [Min(0)] public float Radius = 5f;

        [Tooltip("Arc angle (degrees) for SemiCircle (0–360).")]
        [HideIfAny(nameof(Type), AimType.SemiCircle, false)]
        [Range(0, 360)] public float AngleScale = 180f;

        [Tooltip("Inner radius cutout for ring-style SemiCircle (0 = solid).")]
        [HideIfAny(nameof(Type), AimType.SemiCircle, false)]
        [Min(0)] public float InnerCut = 2;

        [Tooltip("LineRenderer width for LineTexture drawing on SemiCircle.")]
        [HideIfAny(nameof(Type), AimType.SemiCircle, false, nameof(DrawType), DrawingType.Outline, false)]
        [Min(0)] public float TextureScale = 2;

        [HideIfAny(nameof(Type), AimType.SemiCircle, false, nameof(DrawType), DrawingType.Outline, false)]
        public float SideOffsetDistance = 0f;

        [BigHeader("Delay Section")]
        [Min(0), Tooltip("Delay before the first OnAimming event fires after aiming starts.")]
        public float StartAimDelay;

        [Min(0), Tooltip("Minimum time between successive OnAimming events while aiming.")]
        public float DelayBetweenOnAimCalls;

        [Min(0), Tooltip("Delay after release before OnRelease/OnReleaseThrow is fired.")]
        public float OnReleaseDelay;

        [BigHeader("Animation Settings")]
        [Tooltip("If enabled, set animator parameters during aim/release.")]
        public bool EnableAnimationControl;

        [Tooltip("(Optional) Animator used for aim/release parameter updates.")]
        public Animator AnimationControl;

        [Tooltip("(Optional) Animator parameter names: On-Aim bool, On-Release bool, and Aim-Direction float.")]
        public string OnAim_Bool, OnRelease_Bool, AimDirection_Float;

        [BigHeader("Events Section")]
        [Tooltip("Fired when aiming updates. Vector2 is the aim direction (XZ plane).")]
        public UnityEvent<Vector2> OnAimming = new();

        [Tooltip("Fired on release/stop for non-Throw modes. Vector2 is the final aim direction (XZ plane).")]
        [HideIfAny(nameof(Type), AimType.Throw, true)]
        public UnityEvent<Vector2> OnRelease = new();

        [Tooltip("Fired on release for Throw mode. Args: heightScale, computedHeight, hitPoint.")]
        [HideIfAny(nameof(Type), AimType.Throw, false)]
        public UnityEvent<float, float, Vector3> OnReleaseThrow = new();

        GameObject _hitSpot;
        Vector3 _controlPoint, _hitPoint;
        float _hight, _aimAngle, _aimDelta = -1;
        bool _tmpDisabled, _enableRay, _inputDetected, _autoAimDetected, _exeededOnce;
        Vector2 _direction, _rotDirection;
        List<GameObject> _rayObject = new(); // GameObject representing the ray

        Mesh _mesh;
        MeshFilter _mf;
        MeshRenderer _mr;
        Transform _meshRoot;

        const int RadialSegments = 1;

        bool[] _blockedOuterAngles;
        bool[] _blockedInnerAngles;

        void Start()
        {
            EnableState(EnableOnStart);
        }

        void Update()
        {   
            if (Enable)
            {
                CreateRay();
                if (EnableAutoAim && !_inputDetected && AutoSend) OnAutoAimDetected();
                RotationControl();
            }
        }

        public void AutoAimControl(bool enable)
        {
            EnableAutoAim = enable;
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

        #region Ray Control
        public void CreateRay()
        {
            if (!_enableRay) return;

            if (Type == AimType.DirectRay)
            {
                if (_rayObject.Count != 1 || !_rayObject[0].GetComponent<LineRenderer>())
                {
                    Clear();
                    GameObject newRayA = new("AimRay1"); newRayA.transform.position = StartPosition.position;
                    _rayObject.Add(newRayA); newRayA.transform.parent = transform;

                    LineRenderer a = newRayA.AddComponent<LineRenderer>();
                    a.useWorldSpace = true;
                    a.material = RayMaterial;
                }
                LineRenderer drawer = _rayObject[0].GetComponent<LineRenderer>();
                drawer.startWidth = drawer.endWidth = RayWidth;

                Vector3 currentDirec = new (RotateModel.transform.TransformDirection(Vector3.forward).x,
                    0, RotateModel.transform.TransformDirection(Vector3.forward).z);
                Ray ray = new(StartPosition.position, currentDirec); // setup the ray starting from ShootPoint transform

                drawer.positionCount = 1; // reset lineRenderer points
                drawer.SetPosition(0, StartPosition.position);
                float remainingLength = MaxLenght;
                for (int i = 0; i < NumberOfReflections; i++) // loop for each reflection
                {
                    drawer.positionCount += 1; // add a new point
                                               // check raycast // if hit ReflectedLayers or BlockLayers
                    if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, remainingLength, ReflectedLayers + BlockLayers))
                    {
                        drawer.SetPosition(drawer.positionCount - 1, hit.point); // set line next point = hit point
                        remainingLength -= Vector3.Distance(ray.origin, hit.point); // cut the distance from remaining length
                        ray = new Ray(hit.point, Vector3.Reflect(ray.direction, hit.normal)); // setup the next ray direction
                        if (BlockLayers == (BlockLayers | (1 << hit.collider.gameObject.layer))) break; // if block layer: stop ray at hit point
                    }
                    // if not blocked by any ReflectedLayers or BlockLayers objects...
                    else drawer.SetPosition(drawer.positionCount - 1, ray.origin + ray.direction * remainingLength);
                }
            }
            else if (Type == AimType.DirectSquare)
            {
                Clear();
                float remainingLength = MaxLenght;
                Vector3 start = StartPosition.position;
                Vector3 currentDirec = new(_direction.x, 0, _direction.y);
                Vector3[] extraVertices = new Vector3[NumberOfReflections + 1];

                for (int i = 0; i < NumberOfReflections + 1; i++) // Loop for each reflection
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
            else if (Type == AimType.Throw)
            {
                if (_direction.magnitude != 0) // if there is an input
                {
                    if (_rayObject.Count != 1 || !_rayObject[0].GetComponent<LineRenderer>())
                    {
                        Clear();
                        GameObject newRayA = new("AimRay1"); newRayA.transform.position = StartPosition.position;
                        _rayObject.Add(newRayA); newRayA.transform.parent = transform;

                        LineRenderer a = newRayA.AddComponent<LineRenderer>();
                        a.useWorldSpace = true;
                        a.material = RayMaterial;
                        a.widthCurve = DrawWidth;

                        _hitSpot = Instantiate(HitPointSpot, transform.position, HitPointSpot.transform.rotation); // create hit spot
                        _hitSpot.SetActive(false);
                    }
                    else
                    {
                        _rayObject[0].GetComponent<LineRenderer>().widthCurve = DrawWidth;
                    }
                    LineRenderer drawer = _rayObject[0].GetComponent<LineRenderer>();
                    // get the distance depending on joystick delta scale * direction = the local target point
                    Vector2 dist = MaxDistance * _direction;

                    // now setup the throw drawer data
                    _hitPoint = new Vector3(StartPosition.position.x + dist.x, TargetInYAxis, StartPosition.position.z + dist.y);
                    _hight = (DynamicHight ? _direction.magnitude : 1) * MaxHight * 2; // setup hight
                    _controlPoint = (StartPosition.position + _hitPoint) / 2 + _hight * Vector3.up; // the control jump point (used to calculate throw path)

                    drawer.positionCount = 1; // start drawing by clean Drawer points
                    drawer.SetPosition(0, StartPosition.position); // first point
                    for (int i = 1; i < DrawSegments + 1; i++)
                    {
                        float delta = (float)i / DrawSegments;
                        Vector3 m1 = Vector3.Lerp(StartPosition.position, _controlPoint, delta);
                        Vector3 m2 = Vector3.Lerp(_controlPoint, _hitPoint, delta);

                        drawer.positionCount += 1; // add new point
                        drawer.SetPosition(drawer.positionCount - 1, Vector3.Lerp(m1, m2, delta)); // set the new point position
                    }
                    if (_hitSpot)
                    {
                        _hitSpot.SetActive(true); // enable hit spot
                        _hitSpot.transform.position = _hitPoint; // and set position
                    }
                }
            }
            else if (Type == AimType.SemiCircle)
            {
                if (DrawType == DrawingType.FullCircle)
                {
                    Clear();
                    GameObject newRay = new("AimRay0");
                    _rayObject.Add(newRay);
                    newRay.transform.position = StartPosition.position;

                    MeshFilter meshFilter = newRay.AddComponent<MeshFilter>();
                    MeshRenderer meshRenderer = newRay.AddComponent<MeshRenderer>();
                    meshRenderer.material = RayMaterial;

                    // Calculate the angle step for each segment
                    float angleStep = AngleScale / DrawSegments;
                    float currentAngle = -AngleScale / 2f;
                    float rotationAngle = Mathf.Atan2(-_direction.y, _direction.x) * Mathf.Rad2Deg;

                    // Inner cut radius (clamped between 0 and outer radius)
                    float innerRadius = Mathf.Clamp(InnerCut, 0, Radius * 0.99f);

                    // Create vertices (inner + outer rings)
                    Vector3[] vertices = new Vector3[(DrawSegments + 1) * 2]; // Inner and outer for each segment
                    int[] triangles = new int[DrawSegments * 6]; // Two triangles per segment

                    for (int i = 0; i <= DrawSegments; i++)
                    {
                        float angleRad = Mathf.Deg2Rad * currentAngle;
                        Vector3 dir = Quaternion.Euler(0, rotationAngle, 0) *
                                     new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad));

                        // Outer point
                        Vector3 outerPoint = dir * Radius;
                        if (Physics.Linecast(newRay.transform.position, outerPoint + newRay.transform.position,
                            out RaycastHit hit, BlockLayers))
                        {
                            outerPoint = dir * hit.distance;
                        }

                        // Inner point (scaled by InnerCut)
                        Vector3 innerPoint = dir * innerRadius;

                        vertices[i * 2] = innerPoint;
                        vertices[i * 2 + 1] = outerPoint;
                        currentAngle += angleStep;
                    }

                    // Create triangles (connecting inner and outer rings)
                    for (int i = 0; i < DrawSegments; i++)
                    {
                        int baseIndex = i * 6;
                        int currentOuter = i * 2 + 1;
                        int nextOuter = (i + 1) * 2 + 1;
                        int currentInner = i * 2;
                        int nextInner = (i + 1) * 2;

                        // First triangle (outer -> inner -> next outer)
                        triangles[baseIndex] = currentOuter;
                        triangles[baseIndex + 1] = currentInner;
                        triangles[baseIndex + 2] = nextOuter;

                        // Second triangle (next outer -> inner -> next inner)
                        triangles[baseIndex + 3] = nextOuter;
                        triangles[baseIndex + 4] = currentInner;
                        triangles[baseIndex + 5] = nextInner;
                    }

                    Mesh mesh = new()
                    {
                        vertices = vertices,
                        triangles = triangles
                    };
                    mesh.RecalculateNormals();
                    meshFilter.mesh = mesh;
                }
                else
                {
                    EnsureMeshSemi();
                    _mesh.Clear();

                    if (Radius <= 0f || AngleScale <= 0f || DrawSegments < 1 || TextureScale <= 0f)
                        return;

                    float outerRadius = Radius;
                    float innerRadius = Mathf.Clamp(InnerCut, 0f, outerRadius - 0.0001f);
                    bool hasInnerCut = innerRadius > 0f;
                    int angleCount = DrawSegments + 1;

                    var verts = new List<Vector3>(angleCount * 6);
                    var cols = new List<Color>(angleCount * 6);
                    var tris = new List<int>(DrawSegments * 12);

                    _meshRoot.position = transform.position;
                    _meshRoot.rotation = Quaternion.Euler(new Vector3(0, Mathf.Atan2(-_direction.y, _direction.x) * Mathf.Rad2Deg, 0));

                    Vector3 centerWorld = _meshRoot.position;
                    Quaternion occRot = _meshRoot.rotation;

                    float angleStepRad = (AngleScale * Mathf.Deg2Rad) / DrawSegments;
                    float startAngleRad = -(AngleScale * 0.5f) * Mathf.Deg2Rad;
                    float endAngleRad = startAngleRad + AngleScale * Mathf.Deg2Rad;

                    float[] outerPerAngle = new float[angleCount];
                    float[] innerPerAngle = hasInnerCut ? new float[angleCount] : null;

                    _blockedOuterAngles = new bool[angleCount];
                    _blockedInnerAngles = hasInnerCut ? new bool[angleCount] : null;

                    for (int i = 0; i < angleCount; i++)
                    {
                        float angRad = startAngleRad + angleStepRad * i;
                        Vector3 dirLocal = new(Mathf.Cos(angRad), 0f, Mathf.Sin(angRad));
                        Vector3 dirWorld = occRot * dirLocal;

                        // OUTER band cast
                        float maxROuter = outerRadius;
                        bool blockedOuter = false;

                        if (BlockLayers.value != 0)
                        {
                            if (Physics.Raycast(centerWorld, dirWorld, out RaycastHit hitOuter, outerRadius, BlockLayers))
                            {
                                blockedOuter = true;
                                maxROuter = hitOuter.distance;
                            }
                        }

                        _blockedOuterAngles[i] = blockedOuter;
                        outerPerAngle[i] = Mathf.Max(innerRadius, maxROuter);

                        // INNER band cast
                        if (hasInnerCut)
                        {
                            float maxRInner = outerRadius;
                            bool blockedInner = false;

                            if (BlockLayers.value != 0)
                            {
                                if (Physics.Raycast(centerWorld, dirWorld, out RaycastHit hitInner, innerRadius, BlockLayers))
                                {
                                    blockedInner = true;
                                    maxRInner = hitInner.distance;
                                }
                            }

                            _blockedInnerAngles[i] = blockedInner;
                            innerPerAngle[i] = Mathf.Max(innerRadius, maxRInner);
                        }
                    }

                    float bandWidth = Mathf.Max(TextureScale, 0.0001f);
                    int bandSegments = RadialSegments;

                    AddSemiArcBand(
                        outerBand: true,
                        verts, cols, tris,
                        outerPerAngle, innerRadius,
                        bandWidth, bandSegments,
                        startAngleRad, angleStepRad
                    );

                    if (hasInnerCut)
                    {
                        AddSemiArcBand(
                            outerBand: false,
                            verts, cols, tris,
                            innerPerAngle, innerRadius,
                            bandWidth, bandSegments,
                            startAngleRad, angleStepRad
                        );
                    }

                    if (AngleScale < 359.9f)
                    {
                        AddSemiSideFan(true, verts, cols, tris, outerPerAngle, innerRadius,
                                   bandWidth, bandSegments, startAngleRad, endAngleRad);
                        AddSemiSideFan(false, verts, cols, tris, outerPerAngle, innerRadius,
                                   bandWidth, bandSegments, startAngleRad, endAngleRad);
                    }

                    _mesh.SetVertices(verts);
                    _mesh.SetColors(cols);
                    _mesh.SetTriangles(tris, 0);

                    var normals = new Vector3[verts.Count];
                    for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
                    _mesh.SetNormals(normals);
                    _mesh.RecalculateBounds();
                }
            }
        }

        public void Clear()
        {
            foreach (GameObject ray in _rayObject) Destroy(ray);
            _rayObject.Clear();
            if (_hitSpot) Destroy(_hitSpot);
            if (_meshRoot && _meshRoot.gameObject.activeSelf) _meshRoot.gameObject.SetActive(false);
        }
        #endregion

        #region Actions
        public void DisableOutput(float time)
        {
            if (time > 0) StartCoroutine(TempDisable(time));
        }

        IEnumerator TempDisable(float time)
        {
            _tmpDisabled = true;
            yield return new WaitForSeconds(time);
            _tmpDisabled = false;
        }

        void RotationControl() // controlling animations, FXs and SFXs
        {
            //AnimationControl.SetBool(Run_Bool, MoveSystem.IsMoving); // set move animation depending on the integrated movement system
            //AnimationControl.SetBool(Aim_Bool, _shoot);
            if (_inputDetected) _aimAngle = Mathf.Atan2(_direction.x, _direction.y) * 180 / Mathf.PI;

            if (MoveSystem && RotationState.Equals(RotationSettings.ApplyOnDirectionUpdate)) MoveSystem.EnableRotation = !_enableRay && !_autoAimDetected; // if shoot enabled stop rotation of the integrated movement system
                                                                                          //MoveSystem.EnableAnimationControl = !_shoot; // if shoot enabled stop Animation Control of the integrated movement system
                                                                                          // all these integrated movement system disabling to overwrite it by shoot behavior
            if ((MoveSystem && MoveSystem.IsDashing) || _tmpDisabled) return;             // set the movement system function that disable shoot (in this example rolling disable shoot control)

            if (MoveSystem && MoveSystem.IsMoving) // if Moving
            {
                float moveAngle = _aimAngle; // modify the target rotation to fit the move and shoot combined
                if (moveAngle < 0) moveAngle += 360; // the aim & move animation is a blend tree contains 8 animations
                if(AimDirection_Float != string.Empty) AnimationControl.SetFloat(AimDirection_Float, moveAngle);
            }
            if ((_inputDetected && RotationState.Equals(RotationSettings.ApplyOnDirectionUpdate)) || _autoAimDetected) // if Animing
            {
                if (RotationWay == RotationType.WithSpeed)
                    RotateModel.transform.rotation = Quaternion.Slerp(RotateModel.transform.rotation,
                        Quaternion.Euler(new Vector3(0, _aimAngle, 0)), RotateSpeed * Time.deltaTime);
                else
                {
                    if (_inputDetected) RotateModel.transform.LookAt(new Vector3(_direction.x, 0, _direction.y) + RotateModel.transform.position);
                    else if (_autoAimDetected)
                    {
                        RotateModel.transform.rotation = Quaternion.Slerp(RotateModel.transform.rotation,
                        Quaternion.Euler(new Vector3(0, _aimAngle, 0)), RotateSpeed * Time.deltaTime);
                    }
                }
                _rotDirection = new(RotateModel.transform.TransformDirection(Vector3.forward).x,
                     RotateModel.transform.TransformDirection(Vector3.forward).z);
            }
        }

        void OnAutoAimDetected()
        {
            // Add Conditions of other scripts that can control the auto aim system here
            // this example... if character dash/roll? disable auto aim... also you can use AutoAimControl(bool) function to enable/disable auto aim 
            if ((MoveSystem && MoveSystem.IsDashing) || _tmpDisabled) return; // set the movement system function that disable shoot (in this example rolling disable shoot control)

            if (AimDetector) OnRangeEnemyList = AimDetector.DetectedObjects;
            else Debug.LogWarning("there is no detector for the auto aim system, you have to update the On Range List to find the target of the auto aim");

            OnRangeEnemyList.RemoveAll(item => item == null);
            if (OnRangeEnemyList.Count > 0)
            {
                _autoAimDetected = true;
                if (EnableAnimationControl) if (OnAim_Bool != string.Empty) AnimationControl.SetBool(OnAim_Bool, true);
                if (_aimDelta == -1) _aimDelta = StartAimDelay;
                _aimDelta -= Time.deltaTime;

                Vector3 autoAimTarget = OnRangeEnemyList[0].transform.position - CalculationPoint.position;
                autoAimTarget.y = 0; autoAimTarget = autoAimTarget.normalized; // this update only to generate the target angle
                _aimAngle = Mathf.Atan2(autoAimTarget.x, autoAimTarget.z) * 180 / Mathf.PI; // update aim angle (vector direction as angle)
                autoAimTarget = RotateModel.transform.TransformDirection(Vector3.forward);

                if (_aimDelta <= 0) // if all duration exceeded
                {
                    OnAimming?.Invoke(new Vector2(autoAimTarget.x, autoAimTarget.z));
                    _aimDelta = Mathf.Max(DelayBetweenOnAimCalls, Time.deltaTime);// reset aim delta
                }
            }
            else
            {
                _aimDelta = -1;
                _autoAimDetected = false;
                if (EnableAnimationControl) if (OnAim_Bool != string.Empty) AnimationControl.SetBool(OnAim_Bool, false);
            }
        }

        public void AutoAim()
        {
            if (!_autoAimDetected) StartCoroutine(AutoAimTimer());
        }

        IEnumerator AutoAimTimer()
        {
            // Add Conditions of other scripts that can control the auto aim system here
            // this example... if character dash/roll? disable auto aim... also you can use AutoAimControl(bool) function to enable/disable auto aim 
            if ((MoveSystem && MoveSystem.IsDashing) || _tmpDisabled) yield break; // set the movement system function that disable shoot (in this example rolling disable shoot control)

            if (AimDetector) OnRangeEnemyList = AimDetector.DetectedObjects;
            else Debug.LogWarning("there is no detector for the auto aim system, you have to update the On Range List to find the target of the auto aim");

            OnRangeEnemyList.RemoveAll(item => item == null);
            if (OnRangeEnemyList.Count > 0)
            {
                _autoAimDetected = true;
                if (EnableAnimationControl) if (OnRelease_Bool != string.Empty) AnimationControl.SetBool(OnRelease_Bool , true);
                Vector3 autoAimTarget = OnRangeEnemyList[0].transform.position - CalculationPoint.position;
                autoAimTarget.y = 0; autoAimTarget = autoAimTarget.normalized; // this update only to generate the target angle
                _aimAngle = Mathf.Atan2(autoAimTarget.x, autoAimTarget.z) * 180 / Mathf.PI; // update aim angle (vector direction as angle)

                if (RotationState.Equals(RotationSettings.ApplyOnRelease))
                    RotateModel.transform.LookAt(new Vector3(autoAimTarget.x, 0, autoAimTarget.z) + RotateModel.transform.position); // look at the shoot direction

                autoAimTarget = RotateModel.transform.TransformDirection(Vector3.forward);

                yield return new WaitForSeconds(OnReleaseDelay);
                if (EnableAnimationControl) if (OnRelease_Bool != string.Empty) AnimationControl.SetBool(OnRelease_Bool, false);
                _autoAimDetected = false;
                OnRelease?.Invoke(new Vector2(autoAimTarget.x, autoAimTarget.z));
                   // _aimDelta = Mathf.Max(DelayBetweenOnAimCalls, Time.deltaTime);// reset aim delta
            }
        }

        IEnumerator OnStartAim()
        {
            if (_tmpDisabled) yield break;
            if (!_enableRay)
            {
                if (EnableAnimationControl) if (OnAim_Bool != string.Empty) AnimationControl.SetBool(OnAim_Bool, true);
                yield return new WaitForSeconds(StartAimDelay);
                if (!Enable || !_inputDetected) yield break;
                _enableRay = true;
                OnAimming?.Invoke(RotationState == RotationSettings.ApplyOnDirectionUpdate? _rotDirection : _direction);
                StartCoroutine(OnStartAim());
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(DelayBetweenOnAimCalls, Time.deltaTime));
                if (!Enable || !_inputDetected) yield break;
                OnAimming?.Invoke(RotationState == RotationSettings.ApplyOnDirectionUpdate ? _rotDirection : _direction);
                StartCoroutine(OnStartAim());
            }
        }

        IEnumerator OnReleaseControl(bool exceeded)
        {
            if (_tmpDisabled) yield break;
            if (EnableAnimationControl) if (OnAim_Bool != string.Empty) AnimationControl.SetBool(OnAim_Bool, false);
            if (exceeded)
            {
                if (EnableAnimationControl) if (OnRelease_Bool != string.Empty) AnimationControl.SetBool(OnRelease_Bool, true);
                if (RotationState.Equals(RotationSettings.ApplyOnRelease))
                {
                    if (MoveSystem && MoveSystem.IsDashing)
                    {

                    }
                    else
                    {
                        if (MoveSystem) MoveSystem.EnableRotation = false;
                        RotateModel.transform.LookAt(new Vector3(_direction.x, 0, _direction.y) + RotateModel.transform.position); // look at the shoot direction
                    }
                }
            }

            yield return new WaitForSeconds(OnReleaseDelay);
            if (EnableAnimationControl) if (OnRelease_Bool != string.Empty) AnimationControl.SetBool(OnRelease_Bool, false);

            if (exceeded)
            {
                if (Type != AimType.Throw) OnRelease?.Invoke(RotationState == RotationSettings.ApplyOnDirectionUpdate ? _rotDirection : _direction);
                else OnReleaseThrow?.Invoke(DynamicHight ? _direction.magnitude : 1, _hight, _hitPoint);
            }
            yield return new WaitForSeconds(.07f); // extra reset delay
            if (MoveSystem) MoveSystem.EnableRotation = true;
        }
        #endregion

        #region Input Getter
        /// the difference between OnInputDetected() and OnExceedMinDelta(Vector2 direction) on this behavior is
        /// if you want to start moving at the start of touching screen or after touch and move finger on sceen
        /// when touching the screen without moving the direction will be zero until move in screen so
        /// that OnInputDetected() caculate the current direction (rotation) and move at this direction
        /// and OnExceedMinDelta(Vector2 direction) noves charcter after touch and move on screen and you
        /// can set the minimum distanc has to be  move on sceen to enable this method using MinDelta attribute 
        /// go to controller behavior of this system and select which method you want to use
        /// set OnInputDetected() at on OnInputDetected event or OnEcxeedMinDelta(Vector2 direction) on OnExceedMinDelta event
        ///
        public void OnInputDetected()  // enable shoot and set direction to current rotation
        {
            _inputDetected = true;
            if (!_enableRay) StartCoroutine(OnStartAim());

            // calculate direction depending on current rotation
            _direction = new Vector2(RotateModel.transform.TransformDirection(Vector3.forward).x, RotateModel.transform.TransformDirection(Vector3.forward).z);
        }

        public void OnExceedMinRange(Vector2 direction) // enable shoot and set direction to input direction
        {
            _exeededOnce = true;
            _inputDetected = true;
            if (!_enableRay) StartCoroutine(OnStartAim());

            if (FixDirectionOnCameraAngle)
            {
                // fix angle direction depending on camera angle
                float angle = (Camera.main.transform.eulerAngles.y * Mathf.PI / 180) + Mathf.Atan2(direction.x, direction.y);
                _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // setup the new fixed direction
            }
            else _direction = direction; // or set direction = input direction
        }

        public void OnRawValueUpdated(Vector2 direction) // call it every time updating the joystick delta 
        {
            //direction = direction.normalized;
            if (direction.magnitude != 0) // apply only vallied direction // for stop moving use OnInputReleased()
            {
                if (FixDirectionOnCameraAngle)
                {
                    float angle = Camera.main.transform.eulerAngles.y * Mathf.PI / 180
                         + Mathf.Atan2(direction.x, direction.y); // fix angle direction depending on camera angle
                    _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * direction.magnitude; // setup the new fixed direction
                }
                else _direction = direction; // or set direction = input direction
            }
        }

        public void OnInputUpdated(Vector2 direction) // call it every time updating the direction
        {
            if (FixDirectionOnCameraAngle)
            {
                // fix angle direction depending on camera angle
                float angle = (Camera.main.transform.eulerAngles.y * Mathf.PI / 180) + Mathf.Atan2(direction.x, direction.y);
                _direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); // setup the new fixed direction
            }
            else _direction = direction; // or set direction = input direction
        }

        public void OnInputReleased([Optional] bool exceeded) // exceeded used if you using delta scaler or the most controller, check Most-Controller
        {
            _inputDetected = false;
            if (!exceeded && !_exeededOnce && EnableAutoAim && !AutoSend)
            {
                AutoAim();
            }
            else
            {
                StopAllCoroutines();
                StartCoroutine(OnReleaseControl(exceeded && _enableRay));
            }
            _exeededOnce = false;
            _enableRay = false; // disable aimming
            Clear();
        }
        #endregion

        #region Semi-Circle Builder
        void EnsureMeshSemi()
        {
            if (_meshRoot != null && !_meshRoot.gameObject.activeSelf)
            {
                _meshRoot.gameObject.SetActive(true);
                return;
            }

            if (_mesh != null && _mf != null && _mr != null && _meshRoot != null)
                return;

            var child = new GameObject("RadialRayMesh");
            _meshRoot = child.transform;
            _meshRoot.SetParent(transform, false);
            _meshRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _meshRoot.localScale = Vector3.one;

            _mf = child.AddComponent<MeshFilter>();
            _mr = child.AddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "RadialRayGradientMesh" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;

            if (_mr.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                _mr.sharedMaterial = new Material(shader) { color = Color.white };
            }
        }

        void AddSemiArcBand(
            bool outerBand,
            List<Vector3> verts,
            List<Color> cols,
            List<int> tris,
            float[] perAngle,
            float innerRadius,
            float bandWidthWorld,
            int bandSegments,
            float startAngleRad,
            float angleStepRad)
        {
            int angleSegments = perAngle.Length - 1;
            int baseIndex = verts.Count;
            int stride = bandSegments + 1;

            for (int i = 0; i <= angleSegments; i++)
            {
                float maxR = perAngle[i];
                float available = maxR - innerRadius;
                float band = Mathf.Min(bandWidthWorld, Mathf.Max(available, 0f));

                float ang = startAngleRad + angleStepRad * i;
                Vector3 dir = new(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

                for (int j = 0; j <= bandSegments; j++)
                {
                    float t = bandSegments > 0 ? j / (float)bandSegments : 0f;
                    float d = t * band;
                    float r = outerBand ? maxR - d : innerRadius + d;

                    verts.Add(dir * r);

                    float u = band > 0f ? 1f - (d / band) : 1f; // 1 at edge, 0 at inner band border
                    Color c = RayColor;
                    c.a *= Mathf.Clamp01(u);
                    cols.Add(c);
                }
            }

            for (int i = 0; i < angleSegments; i++)
            {
                bool[] blocked = outerBand ? _blockedOuterAngles : _blockedInnerAngles;

                if (blocked != null && (blocked[i] || blocked[i + 1]))
                    continue;

                for (int j = 0; j < bandSegments; j++)
                {
                    int i0 = baseIndex + i * stride + j;
                    int i1 = baseIndex + (i + 1) * stride + j;
                    int i2 = baseIndex + i * stride + (j + 1);
                    int i3 = baseIndex + (i + 1) * stride + (j + 1);

                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i2); tris.Add(i1); tris.Add(i3);
                }
            }
        }

        void AddSemiSideFan(
            bool isLeft,
            List<Vector3> verts,
            List<Color> cols,
            List<int> tris,
            float[] outerPerAngle,
            float innerRadius,
            float bandWidthWorld,
            int radialSegments,
            float startAngleRad,
            float endAngleRad)
        {
            float maxR = outerPerAngle[isLeft ? 0 : outerPerAngle.Length - 1];
            if ((maxR - innerRadius) <= 0f || bandWidthWorld <= 0f)
                return;

            float sideAngle = isLeft ? startAngleRad : endAngleRad;
            Vector2 sideDir2 = new(Mathf.Cos(sideAngle), Mathf.Sin(sideAngle));
            sideDir2.Normalize();

            float midAngle = (startAngleRad + endAngleRad) * 0.5f;
            Vector2 midDir2 = new(Mathf.Cos(midAngle), Mathf.Sin(midAngle));

            Vector2 candN1 = new(-sideDir2.y, sideDir2.x);
            Vector2 candN2 = new(sideDir2.y, -sideDir2.x);

            Vector2 inward2 = Vector2.Dot(candN1, midDir2 - sideDir2) > 0f ? candN1 : candN2;
            inward2.Normalize();

            int radialSteps = Mathf.Max(1, radialSegments);
            int thickSteps = 1;
            int baseIndex = verts.Count;
            int stride = thickSteps + 1;

            for (int i = 0; i <= radialSteps; i++)
            {
                float r = Mathf.Lerp(innerRadius, maxR, (float)i / radialSteps);

                float localWidth = r < maxR
                    ? Mathf.Min(bandWidthWorld, Mathf.Sqrt(Mathf.Max(maxR * maxR - r * r, 0f)))
                    : bandWidthWorld;

                for (int j = 0; j <= thickSteps; j++)
                {
                    float tThickness = (float)j / thickSteps;
                    Vector2 p2 = sideDir2 * r + localWidth * tThickness * inward2;

                    float len = p2.magnitude;
                    if (innerRadius > 0f && len < innerRadius && len > 0f)
                        p2 *= innerRadius / len;
                    if (len > maxR && len > 0f)
                        p2 *= maxR / len;

                    verts.Add(new(p2.x, 0f, p2.y));

                    float u = 1f - tThickness;
                    Color c = RayColor;
                    c.a *= Mathf.Clamp01(u);
                    cols.Add(c);
                }
            }

            if (SideOffsetDistance != 0f && ((radialSteps + 1) * (thickSteps + 1)) >= 4)
            {
                Vector3 dir = (verts[baseIndex + 3] - verts[baseIndex + 1]).normalized;
                verts[baseIndex + 1] = verts[baseIndex + 1] + dir * SideOffsetDistance;
            }

            for (int i = 0; i < radialSteps; i++)
            {
                for (int j = 0; j < thickSteps; j++)
                {
                    int i0 = baseIndex + i * stride + j;
                    int i1 = baseIndex + (i + 1) * stride + j;
                    int i2 = baseIndex + i * stride + (j + 1);
                    int i3 = baseIndex + (i + 1) * stride + (j + 1);

                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i2); tris.Add(i1); tris.Add(i3);
                }
            }
        }
        #endregion
    }
}
