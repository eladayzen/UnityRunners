using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class ExplosionLine : MonoBehaviour
    {
        /// <summary>
        /// the partical explosion scale system can simply modified
        /// goto RescaleExplosionArea method and manually setup your custom edit
        /// the method takes one partical system and scale it, so you can set a condition for your custom
        /// partical system to select it and do what you seek
        /// </summary>
        [BigHeader("Main Settings")]
        [Tooltip("The system that will spawn Line Explosion (and this behavior will take the stored spawn objects in spawn system)")]
        public MOST_Spawn SpawnMain;

        [Tooltip("The horizontal collider (damage) area\nwill be scaled depending explosion setup")]
        public BoxCollider ExploisonEffectAreaH;

        [Tooltip("The vertical collider (damage) area\nwill be scaled depending explosion setup")]
        public BoxCollider ExploisonEffectAreaV;

        [Tooltip("objects layers that will block line explosion from expanding")]
        public LayerMask obstacleLayers;

        [Tooltip("scale of each square explosion (horizontal and  vertical)")]
        public Vector2 GridSize;

        [Tooltip("themax scale cal line go (horizontal and  vertical)")]
        public Vector2 MaxScale;

        [Tooltip("Delay before explosion starts")]
        public float ExplosionStartDelay;

        [Tooltip("The time that collision (damage) area will be active after explosion")]
        public float DamageAreaActiveTime;

        [BigHeader("Events")]
        [Tooltip("Events list when explosion")]
        public UnityEvent OnExplosion = new();

        [BigHeader("Visuals Effects")]
        [Tooltip("this partical and all childs particals if not sub emmitted will be scaled with damage area too\n the affected partical settings:\n" +
            "1- Emmision Count\n2- Shape Scale on z axis\n the default scale of z axis must be equal the grid.x scale" +
            "\n3-LifeTime: will be scaled depending on Damage Area Active Time")]
        public ParticleSystem ExplosionHorizontal;

        [Tooltip("this partical and all childs particals if not sub emmitted  will be scaled with damage area too\n the affected partical settings:\n" +
            "1- Emmision Count\n2- Shape Scale on z axis\n the default scale of z axis must be equal the grid.y scale" +
            "\n3-LifeTime: will be scaled depending on Damage Area Active Time")]
        public ParticleSystem ExplosionVertictal;

        [Tooltip("The flashed game object (name will be used to find it in spawned lines)\n(will be scaled horizontally as well, and must have renderr material)")]
        public GameObject HorizontalWarning;

        [Tooltip("The flashed game object (name will be used to find it in spawned lines)\n(will be scaled vertically as well, and must have renderr material)")]
        public GameObject VertictalWarning;

        [Tooltip("Sound will play for each flash warining")]
        public AudioSource WarninigSound;

        [Tooltip("Sound will play in explosion")]
        public AudioSource ExplosionSound;

        [BigHeader("Flash Control")]
        [Tooltip("main Warning object color")] public Color MainColor;
        [Tooltip("Warning object color flash (lerp from main color to this)")] public Color FlashColor;
        [Tooltip("max time for each flash")] public float MaxFlashTime = 1f;
        [Tooltip("minimum time for each flash")] public float MinFlashTime = .07f;
        [Tooltip("after this time (in seconds) the flash time will accelerate")] public float StartReduction = 3f;
        [Tooltip("acceleration of flash speed")] public float ReductionSpeed = 4f;

        List<Vector3> _maxRight = new();
        List<Vector3> _maxLeft = new();
        List<Vector3> _maxForward = new();
        List<Vector3> _maxBack = new();
        List<GameObject> _lineList = new();
        List<ParticleSystem> _horizontalExplosion = new();
        List<ParticleSystem> _vertialExplosion = new();
        List<BoxCollider> _effectAreaH = new();
        List<BoxCollider> _effectAreaV = new();
        List<GameObject> _horizontalWarnining = new();
        List<GameObject> _vertialWarning = new();

        float _currentTime, _remainTime, _counter, _lerpTime;

        public void ActivateNewExplosion() // Start a new explosion cycle
        {
            _lineList = SpawnMain.CurrentSpawnObjects; // get the last spawn objects from spawn system
            SetupAmbitLists(_lineList); // setup the new lists data and explosion control
            _remainTime = ExplosionStartDelay; // reset remaining time
            StartCoroutine(FlashWarning()); // enable flash
        }
        void SetupAmbitLists(List<GameObject> lineList)
        {
            ClearAllLists();
            for (int i = 0; i < lineList.Count; i++)
            {
                Ray rayRight = new(lineList[i].transform.position, Vector3.right);
                Physics.Raycast(rayRight.origin, rayRight.direction, out RaycastHit hit, MaxScale.x / 2, obstacleLayers);
                _maxRight.Add((hit.collider ? hit.collider.gameObject.transform.position :
                    lineList[i].transform.position + MaxScale.x * Vector3.right / 2) - Vector3.right * GridSize.x);

                Ray rayLeft = new(lineList[i].transform.position, Vector3.left);
                Physics.Raycast(rayLeft.origin, rayLeft.direction, out hit, MaxScale.x / 2, obstacleLayers);
                _maxLeft.Add((hit.collider ? hit.collider.gameObject.transform.position :
                    lineList[i].transform.position + MaxScale.x * Vector3.left / 2) - Vector3.left * GridSize.x);

                Ray rayForward = new(lineList[i].transform.position, Vector3.forward);
                Physics.Raycast(rayForward.origin, rayForward.direction, out hit, MaxScale.y / 2, obstacleLayers);
                _maxForward.Add((hit.collider ? hit.collider.gameObject.transform.position :
                    lineList[i].transform.position + MaxScale.y * Vector3.forward / 2) - Vector3.forward * GridSize.y);

                Ray rayBack = new(lineList[i].transform.position, Vector3.back);
                Physics.Raycast(rayBack.origin, rayBack.direction, out hit, MaxScale.y / 2, obstacleLayers);
                _maxBack.Add((hit.collider ? hit.collider.gameObject.transform.position :
                    lineList[i].transform.position + MaxScale.y * Vector3.back / 2) - Vector3.back * GridSize.y);

                _horizontalWarnining.Add(lineList[i].transform.Find(HorizontalWarning.name).gameObject);
                _vertialWarning.Add(lineList[i].transform.Find(VertictalWarning.name).gameObject);

                _horizontalWarnining[i].transform.localScale += Vector3.right * Vector3.Distance(_maxRight[i], _maxLeft[i]);
                _vertialWarning[i].transform.localScale += Vector3.forward * Vector3.Distance(_maxForward[i], _maxBack[i]);

                _horizontalWarnining[i].transform.position = (_maxRight[i] + _maxLeft[i]) / 2;
                _vertialWarning[i].transform.position = (_maxForward[i] + _maxBack[i]) / 2;

                _horizontalExplosion.Add(lineList[i].transform.Find(ExplosionHorizontal.gameObject.name).GetComponent<ParticleSystem>());
                _vertialExplosion.Add(lineList[i].transform.Find(ExplosionVertictal.gameObject.name).GetComponent<ParticleSystem>());

                _effectAreaH.Add(lineList[i].transform.Find(ExploisonEffectAreaH.gameObject.name).GetComponent<BoxCollider>());
                _effectAreaV.Add(lineList[i].transform.Find(ExploisonEffectAreaV.gameObject.name).GetComponent<BoxCollider>());
            }
        }
        void ClearAllLists() // for a new Explosion
        {
            _maxRight.Clear(); _maxLeft.Clear(); _maxForward.Clear(); _maxBack.Clear();
            _horizontalExplosion.Clear(); _vertialExplosion.Clear();
            _effectAreaH.Clear(); _effectAreaV.Clear();
            _horizontalWarnining.Clear(); _vertialWarning.Clear();
        }
        void RescaleExplosionArea(ParticleSystem pt, Vector3 pos, float scale)
        {
            pt.gameObject.SetActive(true);
            pt.transform.position = pos;

            ParticleSystem.MainModule module = pt.main;
            module.startLifetime = DamageAreaActiveTime;

            ParticleSystem.Burst emission = pt.emission.GetBurst(0);
            emission.count = (int)(emission.maxCount * scale);
            pt.emission.SetBurst(0, emission);

            ParticleSystem.ShapeModule shapeH = pt.shape;
            shapeH.scale = new Vector3(shapeH.scale.x, shapeH.scale.y, scale);
        }

        IEnumerator FlashWarning()
        {
            _counter = 0;
            WarninigSound.Play();
            _currentTime = _remainTime > StartReduction ? MaxFlashTime : Mathf.Max(MinFlashTime, _currentTime - _currentTime / ReductionSpeed);
            _lerpTime = Mathf.Max(_currentTime / 4, MinFlashTime / 2);
            while (_counter < _lerpTime)
            {
                yield return new WaitForSeconds(Time.deltaTime);
                _counter += Time.deltaTime;
                for (int i = 0; i < _lineList.Count; i++)
                {
                    _horizontalWarnining[i].GetComponent<Renderer>().material.color = Color.Lerp(MainColor, FlashColor, _counter / _lerpTime);
                    _vertialWarning[i].GetComponent<Renderer>().material.color = Color.Lerp(MainColor, FlashColor, _counter / _lerpTime);
                }
            }
            _counter = 0;
            while (_counter < _lerpTime)
            {
                yield return new WaitForSeconds(Time.deltaTime);
                _counter += Time.deltaTime;
                for (int i = 0; i < _lineList.Count; i++)
                {
                    _horizontalWarnining[i].GetComponent<Renderer>().material.color = Color.Lerp(FlashColor, MainColor, _counter / _lerpTime);
                    _vertialWarning[i].GetComponent<Renderer>().material.color = Color.Lerp(FlashColor, MainColor, _counter / _lerpTime);
                }
            }
            if (_lerpTime > MinFlashTime / 2) yield return new WaitForSeconds(_currentTime - _lerpTime * 2);
            _remainTime -= _currentTime;
            if (_remainTime <= 0) StartCoroutine(Explode());
            else StartCoroutine(FlashWarning());
        }

        IEnumerator Explode()
        {
            WarninigSound.Stop();
            ExplosionSound.Play();
            for (int i = 0; i < _lineList.Count; i++)
            {
                RescaleExplosionArea(_horizontalExplosion[i], _horizontalWarnining[i].transform.position, Vector3.Distance(_maxRight[i], _maxLeft[i]));
                foreach (Transform pt in _horizontalExplosion[i].gameObject.transform)
                {
                    bool isSubEmit = false;
                    if (pt.GetComponent<ParticleSystem>())
                    {
                        for (int j = 0; j < _horizontalExplosion[i].subEmitters.subEmittersCount; j++)
                            if (_horizontalExplosion[i].subEmitters.GetSubEmitterSystem(j) == pt.GetComponent<ParticleSystem>()) isSubEmit = true;
                    }
                    if (!isSubEmit) RescaleExplosionArea(pt.GetComponent<ParticleSystem>(),
                        _horizontalWarnining[i].transform.position, Vector3.Distance(_maxRight[i], _maxLeft[i]));
                }

                RescaleExplosionArea(_vertialExplosion[i], _vertialWarning[i].transform.position, Vector3.Distance(_maxForward[i], _maxBack[i]));
                foreach (Transform pt in _vertialExplosion[i].gameObject.transform)
                {
                    bool isSubEmit = false;
                    if (pt.GetComponent<ParticleSystem>())
                    {
                        for (int j = 0; j < _vertialExplosion[i].subEmitters.subEmittersCount; j++)
                            if (_vertialExplosion[i].subEmitters.GetSubEmitterSystem(j) == pt.GetComponent<ParticleSystem>()) isSubEmit = true;
                    }
                    if (!isSubEmit) RescaleExplosionArea(pt.GetComponent<ParticleSystem>(),
                        _horizontalWarnining[i].transform.position, Vector3.Distance(_maxForward[i], _maxBack[i]));
                }

                _effectAreaH[i].size = new(_effectAreaH[i].size.x, _effectAreaH[i].size.y, Vector3.Distance(_maxRight[i], _maxLeft[i]));
                _effectAreaV[i].size = new(_effectAreaV[i].size.x, _effectAreaV[i].size.y, Vector3.Distance(_maxForward[i], _maxBack[i]));

                _horizontalWarnining[i].SetActive(false);
                _vertialWarning[i].SetActive(false);
            }

            OnExplosion.Invoke();

            yield return new WaitForSeconds(DamageAreaActiveTime);

            for (int i = 0; i < _lineList.Count; i++)
            {
                _effectAreaH[i].enabled = false;
                _effectAreaV[i].enabled = false;
                Destroy(_lineList[i], 3);
            }
        }
    }
}