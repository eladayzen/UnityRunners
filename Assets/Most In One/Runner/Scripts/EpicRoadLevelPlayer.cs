using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class EpicRoadLevelPlayer : MonoBehaviour
    {
        public void PlayAll()
        {
            MOST_Spawn[] sps = FindObjectsOfType<MOST_Spawn>();
            foreach (MOST_Spawn sp in sps) sp.EnableState(true);

            ForwardMovement[] fms = FindObjectsOfType<ForwardMovement>();
            foreach (ForwardMovement sp in fms) sp.Enabled = true;

            WalkEnemyManager[] ens = FindObjectsOfType<WalkEnemyManager>();
            foreach (WalkEnemyManager sp in ens) sp.StartMove = true;
        }
    }
}
