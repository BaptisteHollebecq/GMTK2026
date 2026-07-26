using UnityEngine;
using DG.Tweening;
using System.Collections;

public class vitre : MonoBehaviour
{
    public float minVelocityBreak = 1f;
    public GameObject vitreBase;
    public GameObject vitreBreak;

    private Vector3 contactPoint;
    private Vector3 impactDirection;

    private void OnCollisionEnter(Collision collision)
    {

        int IncomingLayer = LayerMask.NameToLayer("Grabable");
        if (collision.gameObject.layer == IncomingLayer)
        {
            Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
            Debug.Log(collision.GetContact(0).impulse);
            if (rb.linearVelocity.magnitude > minVelocityBreak)
            {
                GetComponent<Collider>().enabled = false;
                vitreBase.gameObject.SetActive(false);
                vitreBreak.gameObject.SetActive(true);
                //appliquer des degats
                //PlayerLife -= DamagePerMass.Evaluate(rb.mass)
                //controller.Ragdoll(true, RagdollRecoveryTime, RagdollMinVelocity);
            }
        }
    }
}
