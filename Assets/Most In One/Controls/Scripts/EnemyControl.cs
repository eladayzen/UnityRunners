using System.Collections;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    // This behavior will become the main AI enemy Control...
    // Ai Navigation, scanning, smart Aim, dodge, Fog of war and a lot more incoming
    // for now it's just a *Base* / showcase for how to create Enemy with shoot/and throw abilites
    public class EnemyControl : MonoBehaviour
    {
        public MOST_Aim AimSource;
        public MOST_ProjectileGenerator ThrowSource;
        public Animator Anim;
        public float ThrowCycleTime;

        float _counter;

        void Update()
        {
            if (ThrowSource && AimSource.OnRangeEnemyList.Count > 0 && AimSource.Enable)
            {
                _counter += Time.deltaTime;
                if (_counter > ThrowCycleTime)
                {
                    StartCoroutine(Throw());
                    AimSource.EnableState(false);
                }
            }
        }
        IEnumerator Throw()
        {
            Vector3 target = AimSource.OnRangeEnemyList[0].transform.position; 
            Anim.SetBool("Throw", true);
            yield return new WaitForSeconds(.15f);
            Anim.SetBool("Throw", false);
            ThrowSource.SpawnProjectile(target);
            yield return new WaitForSeconds(.45f);
            _counter = 0;
            AimSource.EnableState(true);
        }
    }
}