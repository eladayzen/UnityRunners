using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField, DisallowMultipleComponent]
    public class CharacterControl_Cubey : MonoBehaviour
    {
        [BigHeader("Control Settings")]
        [Tooltip("The Behavior controls the game/level state, to send the current level state to it")]
        [ReadOnly] public GridLevelData LevelSettings;

        [InnerHint("(Optional)"), Tooltip("UI Text used to display the current remain text")]
        public Text BlockRemainsText;
        [InnerHint("(Optional)"), Tooltip("in this behavior, a progress bar controller attached")]
        public GameObject ProgressBar;
        public int ProgressBarZeroPoint;

        [BigHeader("Move Settings")]
        [Tooltip("Character movement Speed")]
        [Min(.1f)] public float Speed;
        [Tooltip("Touch move strength to enable swipe")]
        [Min(1f)] public float SwipeSinstivity;
        [Tooltip("block box (no step at)")] public LayerMask BlockLayer;
        [Tooltip("Stop moving above it")] public LayerMask CheckPointLayer;

        [BigHeader("Events")]
        public UnityEvent OnTriggerBlock;
        public UnityEvent OnStop;

        [BigHeader("Animations")]
        [Tooltip("The list of all texture animations for all states\ncheck the sorting in ChangeEmoji()")]
        public GameObject[] EmojiList; // 0 for idle // 1 for Moving Right // 2 for Left // 3 for Up // 4 For Down // 5 For Win // 6 for Lose

        List<GameObject> _activeBlocks = new();
        Vector3 _nextPosition;
        string _direction; bool _move, _gameOver;
        int _remainBlocks;
        float _progBarStep;

        void Start()
        {
            LevelSettings = FindObjectOfType<GridLevelData>();
            transform.position = LevelSettings.StartCellObject.transform.position;
            _remainBlocks = LevelSettings.NumberOfEmptyCells + LevelSettings.NumberCountedObjects;
            if (BlockRemainsText) BlockRemainsText.text = _remainBlocks.ToString();
            _progBarStep = ProgressBarZeroPoint / _remainBlocks;
        }

        void Update()
        {
            if (_move)
            {
                transform.position = Vector3.MoveTowards(transform.position, _nextPosition, Speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, _nextPosition) < 0.05f) // reach the next point // Stop Character
                {
                    transform.position = _nextPosition;
                    _move = false;
                    if (!_gameOver)
                    {
                        ChangeEmoji("Idle");
                        OnStop?.Invoke();
                    }
                }
            }
        }

        public void InputDetection(Vector2 direction)
        {
            if (_move) return;
            TryMove(direction);
        }

        void TryMove(Vector2 direc)
        {
            Vector3 X_Z_Offset = new(direc.x * LevelSettings.CellWorldSize, 0, direc.y * LevelSettings.CellWorldSize); // next target step
            if (X_Z_Offset.magnitude == 0) return; // no valid direction, in case Vec3.zero
            _direction = direc.x == 1 ? "right" : direc.x == -1 ? "left" : direc.y == 1 ? "up" : "down";

            Ray ray = new(transform.position, X_Z_Offset.normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, 30, layerMask: BlockLayer + CheckPointLayer))
            {
                if (BlockLayer == (BlockLayer | (1 << hit.collider.gameObject.layer)))
                {
                    Vector3 pt = hit.point - transform.position;
                    if (X_Z_Offset.x != 0) pt.x -= pt.x % X_Z_Offset.x;
                    if (X_Z_Offset.z != 0) pt.z -= pt.z % X_Z_Offset.z;
                    _nextPosition = transform.position + pt;
                }
                else if (CheckPointLayer == (CheckPointLayer | (1 << hit.collider.gameObject.layer)))
                {
                    _nextPosition = hit.collider.gameObject.transform.position;
                    _nextPosition.y = transform.position.y;
                }
                if (_nextPosition != transform.position)
                {
                    _move = true;
                    ChangeEmoji("Moving");
                }
            }
        }

        public void BlockCreated() // this called from the block object when it's triggerd by character
        {
            _remainBlocks--;
            if (BlockRemainsText) BlockRemainsText.text = _remainBlocks.ToString();
            if (ProgressBar) ProgressBar.transform.localPosition += Vector3.right * _progBarStep;
            //LevelSettings.PercentageCompleted = 100 * _remainBlocks / (_activeBlocks.Count + _remainBlocks);
            OnTriggerBlock?.Invoke();
            if (_remainBlocks == 0 && !_gameOver) GameOver();
        }

        public void GameOver() // When game over (Reach end point)
        {
            _gameOver = true;
            if (_remainBlocks <= 0) // Win
            {
                ChangeEmoji("Win");
                FindObjectOfType<UniversalGameManager>().OnWin();
            }
            else // Lose
            {
                FindObjectOfType<UniversalGameManager>().OnLose();
                ChangeEmoji("Lost");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Score Object"))
            {
                if (_activeBlocks.Contains(other.gameObject)) return;
                if (other.gameObject == LevelSettings.EndCellObject)
                {
                    transform.position = other.gameObject.transform.position; // because when end block triggerd the movement system disabled
                    BlockCreated();
                    if (!_gameOver) GameOver();
                    _activeBlocks.Add(other.gameObject);
                }
                else if (other.gameObject != LevelSettings.StartCellObject) BlockCreated();
            }
        }
        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Score Object")) // when character leave the square... activate the block and move it
            {
                if (_activeBlocks.Contains(other.gameObject)) return;
                _activeBlocks.Add(other.gameObject);
            }
        }

        void ChangeEmoji(string state)
        {
            foreach (GameObject em in EmojiList) em.SetActive(false);
            if (state == "Moving") EmojiList[_direction == "right" ? 1 : _direction == "left" ? 2 : _direction == "up" ? 3 : 4].SetActive(true);
            else if (state == "Win") EmojiList[5].SetActive(true);
            else if (state == "Lost") EmojiList[6].SetActive(true);
            else EmojiList[0].SetActive(true);
        }
    }
}
