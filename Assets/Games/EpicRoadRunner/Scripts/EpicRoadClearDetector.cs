using System.Collections;
using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Ends the run as soon as the level is actually cleared - nothing left
    // alive and nothing left heading toward the player - instead of waiting
    // for the finish line to physically travel into the character.
    //
    // The finish line still works and is left in place as a fallback: if this
    // never reaches zero (a stray enemy that somehow neither dies nor passes),
    // the endline still ends the run.
    //
    // ---------------------------------------------------------------------
    // WHY THIS DOES NOT READ HEALTH. An earlier version of this idea polled
    // MOST_Damage.IsDefeated() and produced instant false wins. Two reasons,
    // both still true:
    //   1. MOST_Damage.Health is 0 on a freshly instantiated object and only
    //      becomes MaxHealth inside its own Start(), a frame later - so on the
    //      first tick every enemy reads as already dead.
    //   2. A destroyed MOST_Damage reference also reads as defeated, so
    //      anything that removes an enemy for non-combat reasons silently
    //      completes the level.
    // This version never looks at health at all. It only asks whether the
    // object still exists, is active, and is still a threat by position -
    // which is true regardless of initialisation order.
    // ---------------------------------------------------------------------
    public class EpicRoadClearDetector : MonoBehaviour
    {
        [SerializeField] UniversalGameManager manager;
        [SerializeField] CharacterControl_ShootRunner character;

        [Tooltip("Seconds between clear checks. Kept short so the level ends promptly " +
                 "once the last enemy dies rather than lingering on empty road.")]
        [SerializeField] float checkInterval = 0.2f;

        [Tooltip("Delay after the run starts before checking at all.")]
        [SerializeField] float graceSeconds = 1.5f;

        [Tooltip("A gate is still 'incoming' only while it is at least this far ahead of the player.")]
        [SerializeField] float aheadMargin = 1f;

        [Tooltip("An enemy this far behind the player is treated as gone, so a stray " +
                 "enemy that never engaged cannot stall the level forever.")]
        [SerializeField] float behindCutoff = 15f;

        bool _fired;
        bool _sawContent;

        // Wired into UniversalGameManager.OnStartPlay so it begins with the run.
        public void Arm()
        {
            _fired = false;
            _sawContent = false;
            StopAllCoroutines();
            StartCoroutine(Watch());
        }

        IEnumerator Watch()
        {
            yield return new WaitForSeconds(graceSeconds);

            while (!_fired)
            {
                int remaining = CountRemaining();

                // Only ever allowed to fire after content has actually been
                // seen, so a level that is still spawning cannot instant-win.
                if (remaining > 0) _sawContent = true;
                else if (_sawContent) { Trigger(); yield break; }

                yield return new WaitForSeconds(checkInterval);
            }
        }

        int CountRemaining()
        {
            float playerZ = character ? character.transform.position.z : 0f;
            int remaining = 0;

            // Enemies count wherever they are - once they aggro they chase the
            // player, so their position is not a reliable "dealt with" signal.
            // The only exception is one left far behind, see behindCutoff.
            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;
                if (enemy.transform.position.z < playerZ - behindCutoff) continue;
                remaining++;
            }

            // Gates/barrels never die - they are either collected or they
            // scroll past. Either way they stop being "incoming".
            foreach (var gate in FindObjectsByType<MOST_Gate>(FindObjectsSortMode.None))
            {
                if (!gate.gameObject.activeInHierarchy) continue;
                if (gate.IsCollected) continue;
                if (gate.transform.position.z <= playerZ + aheadMargin) continue;
                remaining++;
            }

            return remaining;
        }

        void Trigger()
        {
            if (_fired) return;

            // The finish line or a death may have already ended this run;
            // CharacterControl_ShootRunner.Deactivate() clears IsActive.
            if (character && !character.IsActive) return;

            _fired = true;
            if (character) character.Deactivate();
            if (manager) manager.OnWin();
        }
    }
}
