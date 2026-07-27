using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class DynamicWallControl : MonoBehaviour
    {
        [Tooltip("The start health of the wall")]
        [GUIColor("green")][Min(0)] public float Health = 100;

        [Tooltip("how strong the wall is when takes damage\n it used to control" +
            "the impact strength so that the weak impacts not affect\n like minimum impact strength to affect")]
        [Min(0)]public float Strength = 30;

        [Tooltip("pecentage of cutoff if the impact is stronger than wall Strength")]
        [Range(0, 1)] public float StrengthCutOff = 1;

        [Tooltip("the Flixibility between all other blocks and damage spreading")]
        [Range(0.01f, 1)] public float Flixibility = .25f;
        [Tooltip("receiving damage or affected by other objects multiplyer")]
        [Min(0)] public float ActionMultiplier = 1;
        [Tooltip("reaction strength when breaked")]
        [Min(0)] public float ReactionMultiplier = 1;
        [Tooltip("if enabled, the wall will take damgage from non-Physic objects")]
        public bool AffectedByNon_Physical;
        Vector3 _breakForce;

        void Start()
        {
            GetComponent<Rigidbody>().mass /= Flixibility;
        }
        void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody)
            {
                float impact = collision.relativeVelocity.sqrMagnitude * ActionMultiplier * collision.rigidbody.mass;
                if (impact > Strength) Health -= impact - StrengthCutOff * Strength;
                if (!collision.gameObject.name.Contains("Brick")) _breakForce = collision.relativeVelocity;
            }
            else if (AffectedByNon_Physical)
            {
                float impact = collision.relativeVelocity.sqrMagnitude * ActionMultiplier;
                if (impact > Strength) Health -= impact - StrengthCutOff * Strength;
            }

            if (Health <= 0)
            {
                foreach (Transform Brick in transform)
                {
                    Rigidbody rb = Brick.gameObject.AddComponent<Rigidbody>(); Brick.parent = null;
                    Vector3 vel = GetComponent<Rigidbody>().GetPointVelocity(Brick.position);
                    rb.linearVelocity = vel + _breakForce * (ReactionMultiplier - 1);
                    rb.AddTorque(GetComponent<Rigidbody>().angularVelocity, ForceMode.VelocityChange);
                }
                Destroy(gameObject);
            }
            else GetComponent<Rigidbody>().linearVelocity *= Flixibility;
        }
    }
}