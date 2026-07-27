using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [DisallowMultipleComponent, HideScriptField]
    public class ThreeRoadsController : MonoBehaviour
    {
        [Serializable]
        public struct RoadLane
        {
            [Tooltip("Friendly name for this lane")] public string Name;
            [Tooltip("World-space center X position of the lane")] public float CenterX;
            [Tooltip("World-space width between left and right rails")] public float Width;
            [Tooltip("Optional Y offset for visuals")] public float YOffset;

            public float LeftX => CenterX - (Width * 0.5f);
            public float RightX => CenterX + (Width * 0.5f);
        }

        // Game State
        [BigHeader("Game State")]

        [ReadOnly] public bool isPlaying;
        [ReadOnly] public float CurrentZ;
        [ReadOnly] public int Score;

        [ReadOnly, Tooltip("Exactly 3 entries; true if lane currently active (has a character) — Mid lane starts active")]
        public bool[] ActiveLanes = new bool[3] { false, true, false }; // start at MID

        [ReadOnly] public List<CharacterControl_TRoad> Characters = new();

        // Lanes / Characters / Flow
        [BigHeader("Lanes (3 roads)")]
        public RoadLane[] Lanes = new RoadLane[3]
        {
            new RoadLane { Name = "Left",  CenterX = -2.3f, Width = 1f, YOffset = 0 },
            new RoadLane { Name = "Mid",   CenterX =  0.0f, Width = 1f, YOffset = 0 },
            new RoadLane { Name = "Right", CenterX =  2.3f, Width = 1f, YOffset = 0 },
        };

        [BigHeader("Characters & Prefabs")]
        [InnerHint("(Prefab)")] public GameObject CharacterPrefab;
        [InnerHint("(Scene Object)")] public GameObject StartCharacter;
        [Min(0), Tooltip("Forward speed in units/second")] public float ForwardSpeed = 6f;

        [BigHeader("Score Record")]
        [Tooltip("Reference to the MOST_Database the data going to save in.")]
        public MOST_Database DatabaseHolder;

        [Tooltip("Name of the data entry in the database (supports IntData, FloatData).")]
        public string DataName;
        [Tooltip("Score rate per second @ speed=1 and 1 lane active")] public float ScoreRate = 10f;
        public bool AffectedByForwardSpeed;

        // Camera
        [BigHeader("Camera Follow")]
        public Transform CameraTransform;
        [Tooltip("Offset when 1 lane active")] public Vector3 CameraOffset1 = new(-1.4f, 5.5f, -7f);
        [Tooltip("Offset when 2 lanes active")] public Vector3 CameraOffset2 = new(0.0f, 6.0f, -8f);
        [Tooltip("Offset when 3 lanes active")] public Vector3 CameraOffset3 = new(0.0f, 6.5f, -9f);
        [Tooltip("Smoothing time (seconds) for camera damping")] public float CameraSmoothTime = 0.35f;

        [BigHeader("Events")] public UnityEvent OnGameStart; public UnityEvent OnGameOver;

        // Endless Runner Spawner
        [Serializable]
        public struct TrackPart
        {
            [InnerHint("(Prefab)"), Tooltip("Prefab of a track segment. Its forward length is taken from 'Length' if > 0, otherwise estimated from renderers' bounds (Z size).")]
            public GameObject Prefab;

            [Tooltip("Forward Z length of this segment (units). If <= 0, the system will estimate it from Renderer bounds.")]
            public float Length;

            [Tooltip("Relative chance to spawn this piece (weight). Higher = more frequent.")]
            public float Weight;

            [Tooltip("Optional position offset applied when placing this piece (usually (0,0,0)).")]
            public Vector3 SpawnOffset;
        }

        [BigHeader("Endless Spawner")]
        [Tooltip("Randomizable pool of track pieces used to build the endless path.")]
        public List<TrackPart> Parts = new();

        [Tooltip("How far ahead of the player (CurrentZ) to keep the track spawned (in units).")]
        public float SpawnAhead = 100f;

        [Tooltip("How far behind the player (CurrentZ) before a piece is despawned (in units).")]
        public float DespawnBehind = 40f;

        [Tooltip("Initial Z position where the very first piece starts.")]
        public float StartTrackZ = 0f;

        [Tooltip("Optional deterministic seed (0 = non-deterministic).")]
        public int RandomSeed = 0;

        [Tooltip("Maximum active spawned pieces kept live at once (safety cap).")]
        public int MaxActivePieces = 32;

        private struct SpawnedPiece
        {
            public GameObject Instance;
            public float StartZ;
            public float EndZ;
        }

        private readonly LinkedList<SpawnedPiece> _activePieces = new();
        private float _spawnCursorZ;
        private System.Random _rng;

        // Input state (button-like: press & release in same region)
        readonly Dictionary<int, int> _touchDownRegionByFinger = new(8);
        int? _mouseDownRegion = null;

        // Camera smoothing state
        Vector3 _offsetSmoothed;   // smoothed Y/Z offset preset (we ignore its X)
        Vector3 _offsetVel;        // velocity for SmoothDamp on offset
        float _camXSmoothed;       // smoothed X center (based on active lanes)
        float _camXVel;            // velocity for SmoothDamp on X
        float _scoreAsFloat;
        void Awake()
        {
            // Lanes
            if (Lanes == null || Lanes.Length != 3)
            {
                var arr = new RoadLane[3];
                for (int i = 0; i < 3; i++)
                    arr[i] = i < (Lanes?.Length ?? 0) ? Lanes[i] :
                             new RoadLane { CenterX = i == 0 ? -2.8f : (i == 1 ? 0f : 2.8f), Width = 1.2f };
                Lanes = arr;
            }
            for (int i = 0; i < 3; i++)
            {
                if (Lanes[i].Width <= 0f) { var lane = Lanes[i]; lane.Width = 0.01f; Lanes[i] = lane; }
            }

            // Game state
            CurrentZ = 0f; Score = 0; isPlaying = false;

            // Camera
            if (!CameraTransform && Camera.main) CameraTransform = Camera.main.transform;
            _offsetSmoothed = CameraOffset1;
            _offsetVel = Vector3.zero;
            _camXSmoothed = Lanes[1].CenterX;
            _camXVel = 0f;

            // Spawner
            _rng = (RandomSeed == 0) ? new System.Random(Guid.NewGuid().GetHashCode()) : new System.Random(RandomSeed);
            _spawnCursorZ = StartTrackZ;
            PrimeSpawnQueue(); // create enough pieces to cover initial ahead distance

            // Start character at mid
            if (StartCharacter != null)
            {
                var sc = StartCharacter.GetComponent<CharacterControl_TRoad>();
                if (!sc) sc = StartCharacter.AddComponent<CharacterControl_TRoad>();
                sc.Initialize(this, laneIndex: 1, startOnRightSide: false);
                if (!Characters.Contains(sc)) Characters.Add(sc);

                ActiveLanes[0] = false; ActiveLanes[1] = true; ActiveLanes[2] = false;
            }
        }

        void Update()
        {
            if (!isPlaying)
            {
                // Still allow input to be read only when playing per your earlier behavior.
                return;
            }

            HandlePointerInput();

            // Progress forward
            CurrentZ += ForwardSpeed * Time.deltaTime;

            // Score
            _scoreAsFloat += ScoreRate * Time.deltaTime * Mathf.Max(1, Mathf.Pow(ActiveLaneCount(), 2)) * (AffectedByForwardSpeed? ForwardSpeed:1);
            Score = (int)_scoreAsFloat;
            DatabaseHolder.Get<IntData>(DataName).Set(Score);

            // Characters sync Z
            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].SyncZ(CurrentZ);
            }

            // Endless spawn/despawn
            UpdateSpawner();

            // Camera
            UpdateCameraFollow();
        }

        // Public API
        public void StartPlaying()
        {
            if (isPlaying) return;
            isPlaying = true;
            foreach (var ch in Characters) ch?.SetRunning(true);
            OnGameStart?.Invoke();
        }

        public void Pause() => isPlaying = false;
        public void Resume() => isPlaying = true;
        public void SetSpeed(float s) => ForwardSpeed = Mathf.Max(0f, s);

        public int ActiveLaneCount()
        {
            int c = 0; for (int i = 0; i < 3; i++) if (ActiveLanes[i]) c++; return c;
        }

        public Vector3 GetLaneWorldPos(int laneIndex, bool rightSide, float z, float characterYOffset = 0f)
        {
            laneIndex = Mathf.Clamp(laneIndex, 0, 2);
            var lane = Lanes[laneIndex];
            float x = rightSide ? lane.RightX : lane.LeftX;
            return new Vector3(x, lane.YOffset + characterYOffset, z);
        }

        public bool SpawnCharacterInNextLane(bool startOnRight = false)
        {
            int lane = -1;
            for (int i = 0; i < 3; i++) if (!ActiveLanes[i]) { lane = i; break; }
            if (lane == -1) return false;
            if (!CharacterPrefab) { Debug.LogWarning("No CharacterPrefab set"); return false; }

            var go = Instantiate(CharacterPrefab);
            var ch = go.GetComponent<CharacterControl_TRoad>(); if (!ch) ch = go.AddComponent<CharacterControl_TRoad>();
            ch.Initialize(this, lane, startOnRight);

            ActiveLanes[lane] = true;
            Characters.Add(ch);
            Characters.Sort((a, b) => a.LaneIndex.CompareTo(b.LaneIndex));

            if (isPlaying) ch.SetRunning(true);
            return true;
        }

        public void RemoveCharacter(CharacterControl_TRoad ch)
        {
            if (!ch) return;
            int lane = ch.LaneIndex;
            Characters.Remove(ch);
            ActiveLanes[lane] = false;
            Destroy(ch);
            Destroy(ch.gameObject, 5);

            if (ActiveLaneCount() <= 0) GameOver();
        }

        public void GameOver()
        {
            if (!isPlaying && Characters.Count == 0) return;
            isPlaying = false;
            foreach (var ch in Characters) ch?.SetRunning(false);
            OnGameOver?.Invoke();
        }

        // Input
        List<CharacterControl_TRoad> GetActiveCharsSorted()
        {
            var list = new List<CharacterControl_TRoad>(3);
            for (int i = 0; i < Characters.Count; i++)
            {
                var ch = Characters[i];
                if (ch && ActiveLanes[ch.LaneIndex]) list.Add(ch);
            }
            list.Sort((a, b) => a.LaneIndex.CompareTo(b.LaneIndex));
            return list;
        }

        int MapScreenXToRegionIndex(float screenX, int n)
        {
            float w = Mathf.Max(1f, Screen.width);
            float region = w / Mathf.Max(1, n);
            return Mathf.Clamp(Mathf.FloorToInt(screenX / region), 0, Mathf.Max(1, n) - 1);
        }

        void ToggleRegionIfValid(int regionIndex, List<CharacterControl_TRoad> activeChars)
        {
            if (regionIndex < 0 || regionIndex >= activeChars.Count) return;
            activeChars[regionIndex].ToggleSide();
        }

        void HandlePointerInput()
        {
            var activeChars = GetActiveCharsSorted();
            int n = activeChars.Count;
            if (n == 0) { _mouseDownRegion = null; _touchDownRegionByFinger.Clear(); return; }

            if (MOSTInputCompat.GetMouseButtonDown(0))
            {
                int region = MapScreenXToRegionIndex(MOSTInputCompat.MousePosition.x, n);
                _mouseDownRegion = region;
            }

            if (MOSTInputCompat.GetMouseButtonUp(0) && _mouseDownRegion.HasValue)
            {
                int upRegion = MapScreenXToRegionIndex(MOSTInputCompat.MousePosition.x, n);
                if (upRegion == _mouseDownRegion.Value)
                {
                    ToggleRegionIfValid(upRegion, activeChars);
                    _mouseDownRegion = null;
                    return;
                }
                else _mouseDownRegion = null;
            }

            foreach (var touch in MOSTInputCompat.Touches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        {
                            int downRegion = MapScreenXToRegionIndex(touch.position.x, n);
                            _touchDownRegionByFinger[touch.fingerId] = downRegion;
                            break;
                        }

                    case TouchPhase.Ended:
                        {
                            if (_touchDownRegionByFinger.TryGetValue(touch.fingerId, out int downRegion))
                            {
                                int upRegion = MapScreenXToRegionIndex(touch.position.x, n);
                                if (upRegion == downRegion)
                                    ToggleRegionIfValid(upRegion, activeChars);

                                _touchDownRegionByFinger.Remove(touch.fingerId);
                            }
                            break;
                        }

                    case TouchPhase.Canceled:
                        {
                            _touchDownRegionByFinger.Remove(touch.fingerId);
                            break;
                        }
                }
            }
        }

        // Endless Spawner Logic
        void PrimeSpawnQueue()
        {
            // Spawn initial pieces to cover ahead distance
            int safety = 0;
            while (_spawnCursorZ < CurrentZ + SpawnAhead && safety++ < MaxActivePieces)
            {
                if (!TrySpawnNextPiece()) break;
            }
        }

        void UpdateSpawner()
        {
            // Spawn forward
            int safetySpawn = 0;
            while (_spawnCursorZ < CurrentZ + SpawnAhead && safetySpawn++ < MaxActivePieces)
            {
                if (!TrySpawnNextPiece()) break;
            }

            // Despawn behind
            while (_activePieces.Count > 0)
            {
                var first = _activePieces.First.Value;
                if (first.EndZ < CurrentZ - DespawnBehind)
                {
                    if (first.Instance) Destroy(first.Instance);
                    _activePieces.RemoveFirst();
                }
                else break;
            }
        }

        bool TrySpawnNextPiece()
        {
            if (Parts == null || Parts.Count == 0) return false;
            if (_activePieces.Count >= MaxActivePieces) return false;

            // Weighted random selection
            var idx = PickWeightedIndex();
            if (idx < 0) return false;

            var def = Parts[idx];
            if (!def.Prefab) return false;

            float length = def.Length > 0f ? def.Length : EstimatePrefabLength(def.Prefab);
            if (length <= 0.01f) length = 1f; // fallback

            Vector3 pos = new Vector3(0f, 0f, _spawnCursorZ) + def.SpawnOffset;
            var inst = Instantiate(def.Prefab, pos, Quaternion.identity);

            _activePieces.AddLast(new SpawnedPiece
            {
                Instance = inst,
                StartZ = _spawnCursorZ,
                EndZ = _spawnCursorZ + length
            });

            _spawnCursorZ += length;
            return true;
        }

        int PickWeightedIndex()
        {
            float total = 0f;
            for (int i = 0; i < Parts.Count; i++)
            {
                float w = Mathf.Max(0f, Parts[i].Weight);
                total += w;
            }
            if (total <= 0f) return -1;

            double r = _rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < Parts.Count; i++)
            {
                acc += Mathf.Max(0f, Parts[i].Weight);
                if (r <= acc) return i;
            }
            return Parts.Count - 1;
        }

        float EstimatePrefabLength(GameObject prefab)
        {
            bool any = false;

            // Try renderer bounds
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds? all = null;
                foreach (var r in renderers)
                {
                    if (!r) continue;
                    all = all.HasValue ? Encapsulate(all.Value, r.bounds) : r.bounds;
                }
                if (all.HasValue)
                {
                    return all.Value.size.z;
                }
                any = renderers.Length > 0;
            }

            // Fallback: try collider bounds
            var cols = prefab.GetComponentsInChildren<Collider>();
            if (cols != null && cols.Length > 0)
            {
                Bounds? all = null;
                foreach (var c in cols)
                {
                    if (!c) continue;
                    all = all.HasValue ? Encapsulate(all.Value, c.bounds) : c.bounds;
                }
                if (all.HasValue)
                {
                    return all.Value.size.z;
                }
                any = cols.Length > 0;
            }

            return any ? 1f : 1f;
        }

        Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        // Camera
        void UpdateCameraFollow()
        {
            if (!CameraTransform) return;

            bool leftActive = ActiveLanes.Length > 0 && ActiveLanes[0];
            bool midActive = ActiveLanes.Length > 1 && ActiveLanes[1];
            bool rightActive = ActiveLanes.Length > 2 && ActiveLanes[2];
            int ac = ActiveLaneCount();

            // Framing preset (treat Left+Right without Mid as 3-active)
            bool treatAsThree = leftActive && rightActive && !midActive;
            Vector3 targetOff;
            if (treatAsThree) targetOff = CameraOffset3;
            else if (ac <= 1) targetOff = CameraOffset1;
            else if (ac == 2) targetOff = CameraOffset2;
            else targetOff = CameraOffset3;

            // Smooth Y/Z offset (ignore X)
            _offsetSmoothed = new Vector3(
                0f,
                Mathf.SmoothDamp(_offsetSmoothed.y, targetOff.y, ref _offsetVel.y, CameraSmoothTime),
                Mathf.SmoothDamp(_offsetSmoothed.z, targetOff.z, ref _offsetVel.z, CameraSmoothTime)
            );

            // Determine camera X purely from ACTIVE lanes, then smooth
            float targetCamX;
            if (treatAsThree)
            {
                targetCamX = Lanes[1].CenterX; // behave like all three lanes are active
            }
            else
            {
                float sum = 0f; int cnt = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (!ActiveLanes[i]) continue;
                    sum += Lanes[i].CenterX; cnt++;
                }
                targetCamX = (cnt == 0) ? Lanes[1].CenterX : (sum / cnt);
            }
            _camXSmoothed = Mathf.SmoothDamp(_camXSmoothed, targetCamX, ref _camXVel, CameraSmoothTime);

            // Optional Y anchor from first character (keeps height consistent)
            float yAnchor = 0f;
            if (Characters.Count > 0 && Characters[0]) yAnchor = Characters[0].transform.position.y;

            float x = _camXSmoothed;
            float y = yAnchor + _offsetSmoothed.y;
            float z = CurrentZ + _offsetSmoothed.z;

            CameraTransform.position = new Vector3(x, y, z);
        }
    }
}
