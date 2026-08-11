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

        // Enemies only. Barrels and gates deliberately do NOT hold the level open.
        //
        // They used to, and it made the end drag badly: the squad shoots far
        // ahead, so the last enemy often dies while it is still tens of units
        // away. Everything between the player and that kill - every barrel still
        // scrolling in - then had to physically reach the player before the level
        // would end, which on a long level is many seconds of empty road.
        //
        // Leftover barrels are just pickups you chose to skip. Once nothing can
        // hurt you, the level is over.
        int CountRemaining()
        {
            float playerZ = character ? character.transform.position.z : 0f;
            int remaining = 0;

            foreach (var enemy in FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None))
            {
                if (!enemy.gameObject.activeInHierarchy) continue;

                // Already dead, just not cleaned up yet. MOST_Damage waits
                // DelayBeforeApply (1.75s on these prefabs) after death before it
                // deactivates or destroys the object, so a corpse stays
                // active-in-hierarchy that whole time. Counting those added ~1.75s
                // of empty road after the final kill.
                //
                // Two signals, because neither alone is reliable:
                //   Health <= 0    - true for the normal path, where the squad shoots
                //                    an enemy down. Safe to read here (not at spawn):
                //                    Arm() runs on OnStartPlay and the first check is
                //                    graceSeconds later, long after every
                //                    MOST_Damage.Start() has set Health to MaxHealth.
                //   !StartMove     - MOST_Damage.OnDefeat is wired on these prefabs to
                //                    WalkEnemyManager.DestroyChild(), which clears
                //                    StartMove. This catches deaths that never touch
                //                    health, e.g. a direct InstantKill(), which sets an
                //                    internal flag and leaves Health at max.
                var damage = enemy.GetComponent<MOST_Damage>();
                if (damage != null && damage.IsDefeated()) continue;
                if (!enemy.StartMove) continue;

                // Only enemies still IN FRONT of the squad count.
                //
                // Enemies die solely to bullets, which spawn on the squad and travel
                // forward. Anything level with or behind the squad can therefore never
                // be shot - and aggro'd enemies chase the player, so they end up sitting
                // exactly there. Those became permanently-alive blockers that held the
                // level open until the finish line arrived, which is the long wait on
                // the enemy-heavy levels. They are no longer incoming, so they no longer
                // count. If one is still grinding the squad down, that resolves as a
                // loss on its own.
                if (enemy.transform.position.z <= playerZ + aheadMargin) continue;
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
