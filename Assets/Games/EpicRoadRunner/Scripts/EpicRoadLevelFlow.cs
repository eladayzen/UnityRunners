using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Drives the whole session flow for a single scene attempt:
    // auto-start countdown, then on win/lose a short countdown into a full
    // scene reload with a freshly-picked level variant. Reloading the scene
    // is the reset mechanism - CharacterControl_ShootRunner has no public
    // way to reset its internal state (private end-trigger flag, crowd
    // count, etc.), so re-loading guarantees a clean slate every attempt.
    public class EpicRoadLevelFlow : MonoBehaviour
    {
        [SerializeField] CharacterControl_ShootRunner character;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] GameObject countdownRoot;
        [SerializeField] GameObject winUI;
        [SerializeField] GameObject loseUI;
        [SerializeField] GameObject[] levelVariants;
        [SerializeField] Transform levelSpawnPoint;

        [SerializeField] int startCountdownSeconds = 3;
        [SerializeField] int nextLevelCountdownSeconds = 5;
        [SerializeField] float endScreenHoldSeconds = 1.5f;

        [SerializeField] float enemyClearCheckInterval = 0.5f;

        static bool hasStartedOnce;
        GameObject _currentLevel;
        bool _ended;

        void Awake()
        {
            character.OnWinEvent.AddListener(() => TriggerEnd(winUI));
            character.OnLoseEvent.AddListener(() => TriggerEnd(loseUI));
        }

        void Start()
        {
            SpawnRandomLevel();
            StartCoroutine(BeginRun());
        }

        void SpawnRandomLevel()
        {
            if (_currentLevel) Destroy(_currentLevel);
            var prefab = levelVariants[Random.Range(0, levelVariants.Length)];
            Vector3 pos = levelSpawnPoint ? levelSpawnPoint.position : Vector3.zero;
            _currentLevel = Instantiate(prefab, pos, Quaternion.identity);
        }

        IEnumerator BeginRun()
        {
            int seconds = hasStartedOnce ? nextLevelCountdownSeconds : startCountdownSeconds;
            hasStartedOnce = true;
            yield return Countdown(seconds);
            character.Activate();
            ActivateLevelContent();

            // Barrels are drive-through pickups (no MOST_Damage - shooting them
            // is cosmetic, they don't die). Only enemies carry MOST_Damage, so
            // "nothing left to shoot" means every enemy's Health has hit zero.
            // Guarded on there having been at least one enemy to begin with, so
            // an enemy-free level doesn't instantly "end" before it's played.
            var enemies = _currentLevel.GetComponentsInChildren<MOST_Damage>(includeInactive: true);
            if (enemies.Length > 0)
                StartCoroutine(WatchForAllEnemiesDefeated(enemies));
        }

        IEnumerator WatchForAllEnemiesDefeated(MOST_Damage[] enemies)
        {
            while (!_ended)
            {
                bool anyAlive = false;
                foreach (var enemy in enemies)
                {
                    if (enemy && !enemy.IsDefeated()) { anyAlive = true; break; }
                }
                if (!anyAlive)
                {
                    TriggerEnd(winUI);
                    yield break;
                }
                yield return new WaitForSeconds(enemyClearCheckInterval);
            }
        }

        void TriggerEnd(GameObject panel)
        {
            if (_ended) return;
            _ended = true;
            StartCoroutine(EndOfLevel(panel));
        }

        // Generated level prefabs don't carry an EpicRoadLevelPlayer component
        // (only the original hand-placed level had one) - this replicates its
        // PlayAll() exactly, scoped to the freshly-spawned level, so barrels/
        // gates/enemies actually move instead of sitting still.
        void ActivateLevelContent()
        {
            foreach (var fm in _currentLevel.GetComponentsInChildren<ForwardMovement>())
                fm.Enabled = true;
            foreach (var spawn in _currentLevel.GetComponentsInChildren<MOST_Spawn>())
                spawn.EnableState(true);
            foreach (var walker in _currentLevel.GetComponentsInChildren<WalkEnemyManager>())
                walker.StartMove = true;
        }

        IEnumerator EndOfLevel(GameObject panel)
        {
            panel.SetActive(true);
            yield return new WaitForSeconds(endScreenHoldSeconds);
            panel.SetActive(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        IEnumerator Countdown(int seconds)
        {
            countdownRoot.SetActive(true);
            for (int i = seconds; i > 0; i--)
            {
                countdownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }
            countdownRoot.SetActive(false);
        }
    }
}
