using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Gates/barrels/bullets only get destroyed as a combat outcome
    // (MOST_Gate.DestroyGate() on a depleted barrel, ForwardMovement's own
    // BlockLayers hit). Nothing despawns the ones that survive to reach the
    // player, so they just keep scrolling on through and past the
    // character. This trigger, placed just behind the player, cleans up
    // whatever slips through so it stops being visible instead of
    // appearing to clip through the character.
    //
    // ---------------------------------------------------------------------
    // TWO WAYS THIS SCRIPT CAN END EVERY RUN A FEW SECONDS IN. Both have been
    // hit for real. Read before editing.
    //
    // 1. THE TARGET MUST BE THE PROP, NEVER transform.root.
    //    EpicRoadLevelFlow instantiates "Level #N" as ONE container GameObject
    //    holding ~26 nested prop prefabs, and that container carries no
    //    components of its own. So for any prop that touches this trigger,
    //    other.transform.root IS THE ENTIRE LEVEL - and it passes a
    //    GetComponentInChildren<ForwardMovement>() test, because some
    //    descendant of it moves. Destroying it wiped the whole level the
    //    instant the first prop crossed the line, which nulled every enemy at
    //    once and tripped the win check below. Resolve the nearest ancestor
    //    that actually OWNS the movement instead.
    //
    // 2. NEVER DESTROY A LIVING ENEMY.
    //    EpicRoadLevelFlow.WatchForAllEnemiesDefeated holds an array of
    //    MOST_Damage and clears the level when none are alive, testing
    //    `enemy && !enemy.IsDefeated()`. A missing reference reads as
    //    defeated - which is CORRECT, since MOST_Damage with DefeatType ==
    //    Destroy really does destroy itself on death. That makes deleting a
    //    live enemy indistinguishable from killing it, and it silently
    //    completes the level. Filtering on ForwardMovement alone is not
    //    sufficient protection: an enemy prop can carry both components, so
    //    this checks the health of whatever it is about to remove instead.
    //
    // Fixing only one of the two leaves the symptom fully intact, because
    // trap 1 alone is enough to null every enemy in the level at once.
    // ---------------------------------------------------------------------
    [RequireComponent(typeof(Collider))]
    public class EpicRoadDespawnZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            // Nearest ancestor (or self) actually carrying the movement - see
            // trap 1. GetComponentInParent walks UP and stops at the prop;
            // transform.root walks all the way up to the level container.
            var mover = other.GetComponentInParent<ForwardMovement>();
            if (!mover) return;

            GameObject prop = mover.gameObject;

            // Belt and braces on trap 1: a level container is parentless, so
            // anything without a parent is not a prop and is never removed,
            // whatever happens to hang beneath it.
            if (prop.transform.parent == null) return;

            // Trap 2: leave anything still alive alone, so a null MOST_Damage
            // can only ever mean "killed in combat".
            foreach (var dmg in prop.GetComponentsInChildren<MOST_Damage>(true))
            {
                if (dmg && !dmg.IsDefeated()) return;
            }

            Destroy(prop);
        }
    }
}
