using UnityEngine;
using UnityEngine.UI;

namespace RunnerPac.EpicRoadRunner
{
    // Time.timeScale = 0 freezes gameplay AND every WaitForSeconds-based
    // coroutine (countdowns, level-clear polling) for free, so pausing
    // doesn't need to touch any other system. Lives as its own top-level
    // Canvas sibling so it renders/receives clicks above every other panel,
    // including the win/lose end screen.
    public class EpicRoadPauseController : MonoBehaviour
    {
        [SerializeField] Button pauseButton;
        [SerializeField] Button resumeButton;
        [SerializeField] GameObject pauseOverlay;

        void Awake()
        {
            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
        }

        void Pause()
        {
            Time.timeScale = 0f;
            pauseOverlay.SetActive(true);
        }

        void Resume()
        {
            Time.timeScale = 1f;
            pauseOverlay.SetActive(false);
        }

        void OnDestroy()
        {
            // Guard against the scene-reload flow leaving playback stuck at
            // 0 if it happens to be reloaded while paused.
            Time.timeScale = 1f;
        }
    }
}
