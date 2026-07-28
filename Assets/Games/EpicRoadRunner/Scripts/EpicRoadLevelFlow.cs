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
            // A level-transition reload already ran its countdown on the end
            // screen before reloading - only the very first Start() this play
            // session needs its own countdown here.
            if (!hasStartedOnce)
            {
                hasStartedOnce = true;
                yield return Countdown(startCountdownSeconds, "Starting in {0}");
            }
            character.Activate();
            ActivateLevelContent();

            // Enemies-only, deliberately: the level's content scrolls toward a
            // stationary character (see ActivateLevelContent), so whatever was
            // placed farthest out takes real time to arrive regardless of what
            // we're waiting on. Requiring every pickup too (not just enemies)
            // was tried and reverted - it meant waiting for the single
            // farthest-out barrel to scroll all the way in, which reads as a
            // dead "empty road" stretch. Ending on enemies alone means some
            // distant barrels may occasionally get skipped, but that's the
            // better trade-off for pacing.
            var enemies = _currentLevel.GetComponentsInChildren<MOST_Damage>(includeInactive: true);
            if (enemies.Length > 0)
                StartCoroutine(WatchForAllEnemiesDefeated(enemies));
        }

        IEnumerator WatchForAllEnemiesDefeated(MOST_Damage[] enemies)
        {
            while (!_ended)
            {
                // Wait a beat before every check, including the first - a
                // freshly-Instantiated MOST_Damage hasn't run its own Start()
                // yet (that's what sets Health to MaxHealth; the field itself
                // defaults to 0), so checking on the same frame the level
                // spawns would wrongly read every enemy as already dead.
                yield return new WaitForSeconds(enemyClearCheckInterval);

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
            // Recap stays visible the whole time - countdown appears
            // alongside it, not over it, so it's readable while counting down.
            panel.SetActive(true);
            yield return Countdown(nextLevelCountdownSeconds, "Next level in {0}");
            panel.SetActive(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        IEnumerator Countdown(int seconds, string format)
        {
            countdownRoot.SetActive(true);
            for (int i = seconds; i > 0; i--)
            {
                countdownText.text = string.Format(format, i);
                yield return new WaitForSeconds(1f);
            }
            countdownRoot.SetActive(false);
        }
    }
}
