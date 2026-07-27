using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [DisallowMultipleComponent, HideScriptField]
    public class CharacterControl_TRoad : MonoBehaviour
    {
        [BigHeader("Runtime")]
        [ReadOnly, Tooltip("Zero-based lane index this character occupies: 0 = Left, 1 = Mid, 2 = Right.")]
        public int LaneIndex;
        [ReadOnly, Tooltip("Current side within the lane: false = Left rail, true = Right rail.")]
        public bool OnRightSide;

        [BigHeader("Movement")]
        [Tooltip("Horizontal move speed (units/second) used when switching between the lane’s left/right rails.")]
        public float SideSpeed = 7.5f;
        [Tooltip("Extra vertical offset added to the lane’s Y when positioning this character (visual fine-tuning).")]
        public float CharacterYOffset = 0f;
        [Tooltip("If enabled, the character’s Z position continuously follows the controller’s CurrentZ value.")]
        public bool LockZToController = true;

        [BigHeader("Animations")]
        [InnerHint("(Optional)"),ReadOnlyIf(nameof(UseModelList)), Tooltip("Optional Animator to drive running/defeat states. Leave null to manage animations yourself.")]
        public Animator AnimationControl;

        [Tooltip("(Optional) Animator parameter names used by AnimationControl: running bool and defeat bool.")]
        public string OnMove_Bool, OnDefeat_Bool;

        [BigHeader("Layers and Tags (OnTrigger)")]
        [Tooltip("Layer that removes a character when triggered")]
        public LayerMask EnemyLayer;
        [Tooltip("Tags that removes a character when triggered")]
        public string[] EnemyTags;

        [Tooltip("Layer that adds a new character when triggered")]
        public LayerMask AddCharLayer;
        [Tooltip("Tags that adds a new character when triggered")]
        public string[] AddCharTags;

        [BigHeader("Model")]
        public bool UseModelList;
        [Tooltip("Each model must have an Animator")]
        public GameObject[] ModelsList;

        ThreeRoadsController _controller;
        bool _running;

        public void Initialize(ThreeRoadsController controller, int laneIndex, bool startOnRightSide)
        {
            if (UseModelList)
            {
                foreach (GameObject model in ModelsList) model.SetActive(false);
                int t = Random.Range(0, ModelsList.Length - 1);
                ModelsList[t].SetActive(true);
                AnimationControl = ModelsList[t].GetComponent<Animator>();
            }
            _controller = controller; LaneIndex = Mathf.Clamp(laneIndex, 0, 2); OnRightSide = startOnRightSide; SetRunning(false);
            Vector3 pos = _controller.GetLaneWorldPos(LaneIndex, OnRightSide, _controller.CurrentZ, CharacterYOffset); transform.position = pos;
        }

        public void SetRunning(bool run) { _running = run; if (AnimationControl) AnimationControl.SetBool(OnMove_Bool, run); }
        public void SyncZ(float z) { if (!LockZToController) return; var p = transform.position; p.z = z; transform.position = p; }
        public void ToggleSide() { OnRightSide = !OnRightSide; }

        void Update()
        {
            if (_controller == null || !_running) return;
            Vector3 target = _controller.GetLaneWorldPos(LaneIndex, OnRightSide, LockZToController ? _controller.CurrentZ : transform.position.z, CharacterYOffset);
            transform.position = Vector3.MoveTowards(transform.position, target, SideSpeed * Time.deltaTime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_controller == null) return;

            bool enemyMatch = MatchesLayer(other.gameObject.layer, EnemyLayer) || MatchesAnyTag(other.gameObject, EnemyTags);
            bool addCharMatch = MatchesLayer(other.gameObject.layer, AddCharLayer) || MatchesAnyTag(other.gameObject, AddCharTags);

            if (enemyMatch)
            {
                if (AnimationControl && !string.IsNullOrEmpty(OnDefeat_Bool))
                    AnimationControl.SetBool(OnDefeat_Bool, true);

                _controller.RemoveCharacter(this);

                var col = GetComponent<Collider>();
                if (col) col.enabled = false;

                return;
            }

            if (addCharMatch) _controller.SpawnCharacterInNextLane();
        }

        static bool MatchesLayer(int layer, LayerMask mask)
        {
            if (mask.value == 0) return false;
            return (mask.value & (1 << layer)) != 0;
        }

        static bool MatchesAnyTag(GameObject go, string[] tags)
        {
            if (tags == null || tags.Length == 0) return false;
            string t = go.tag;
            for (int i = 0; i < tags.Length; i++)
            {
                var wanted = tags[i];
                if (!string.IsNullOrEmpty(wanted) && t == wanted) return true;
            }
            return false;
        }
    }
}
