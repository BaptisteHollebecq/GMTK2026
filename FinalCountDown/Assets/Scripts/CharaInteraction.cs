using UnityEngine;
using UnityEngine.InputSystem.XR;

public class CharaInteraction : MonoBehaviour
{
    public KeyCode UseObject;
    public string UsableTag = "Usable";

    [Header("Références")]
    public Camera ViewCamera;

    [Header("Holding")]
    [SerializeField] private float grabDistance = 2f;       // Distance max pour attraper
    [SerializeField] private float holdDistance = 2.5f;     // Distance à laquelle l'objet est maintenu devant le joueur

    [Header("Grabbing Properties")]
    [SerializeField] private LayerMask GrabMask;
    [SerializeField] private float baseMoveForce = 150f;    // Force de base appliquée
    [SerializeField] private float baseDamping = 8f;        // Amortissement de base (réduit les oscillations)
    [SerializeField] private float maxVelocity = 12f;       // Vitesse max de l'objet tenu
    [SerializeField] private AnimationCurve massToStrength = AnimationCurve.Linear(1f, 1f, 50f, 0.1f);
    // ↑ Courbe : en X la masse de l'objet, en Y un multiplicateur de force (1 = facile, proche de 0 = très dur)

    [Header("Cam Rotation")]
    [SerializeField] private bool rotateWithCamera = true;
    [SerializeField] private float rotationLerpSpeed = 6f;

    [Header("Throw")]
    [SerializeField] private float throwForce = 12f;

    [Header("Collision Properties")]
    public AnimationCurve VelocityPerMass;
    public AnimationCurve DamagePerMass;
    public float RagdollMintime = 1;
    public float RagdollRecoveryTime = 1;
    public float RagdollMinVelocity = .3f;

    private CharaController controller;
    private Rigidbody heldBody;
    private float heldObjectMass;
    private float currentHoldDistance;
    private Quaternion grabbedRotationOffset;
    private Vector3 hitPoint;

    private void Awake()
    {
        controller = GetComponent<CharaController>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (heldBody == null)
                TryGrab();
            else
                Release(false);
        }

        if (Input.GetMouseButtonDown(1) && heldBody != null)
        {
            Release(true); // clic droit = lancer l'objet
        }

        if (Input.GetKeyDown(UseObject))
        {
            if (heldBody == null || !heldBody.gameObject.CompareTag(UsableTag))
                return;
            else
            {
                heldBody.GetComponent<UsableObject>().Use();
            }
        }
    }

    private void FixedUpdate()
    {
        if (heldBody != null)
            MoveHeldObject();
    }

    private void TryGrab()
    {
        Ray ray = ViewCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, grabDistance, GrabMask))
        {
            Rigidbody rb = hit.rigidbody;
            if (rb == null) return;

            hitPoint = hit.rigidbody.transform.InverseTransformPoint(hit.point);
            heldBody = rb;
            heldObjectMass = rb.mass;
            controller.SpeedMalus = rb.mass;
            currentHoldDistance = holdDistance;

            rb.linearDamping = 0f; // on gère nous-mêmes l'amortissement
            rb.angularVelocity = Vector3.zero;

            grabbedRotationOffset = Quaternion.Inverse(ViewCamera.transform.rotation) * rb.rotation;
        }
    }

    private void MoveHeldObject()
    {
        //Debug.Log(heldBody.angularVelocity);

        Vector3 targetPos = ViewCamera.transform.position + ViewCamera.transform.forward * currentHoldDistance;
        Vector3 toTarget = targetPos - heldBody.transform.TransformPoint(hitPoint);

        // Multiplicateur basé sur le poids : un objet lourd répond beaucoup moins vite
        float strength = Mathf.Clamp(massToStrength.Evaluate(heldObjectMass), 0.05f, 1f);

        // Force ressort (proportionnelle à la distance) + amortissement (anti-oscillation)
        Vector3 springForce = toTarget * baseMoveForce * strength;
        Vector3 dampingForce = -heldBody.linearVelocity * baseDamping * strength;

        heldBody.AddForce(springForce + dampingForce, ForceMode.Force);

        // On limite la vitesse max, réduite pour les objets lourds
        float velCap = maxVelocity * Mathf.Clamp01(0.3f + strength);
        if (heldBody.linearVelocity.magnitude > velCap)
            heldBody.linearVelocity = heldBody.linearVelocity.normalized * velCap;

        // Rotation : suit la caméra mais avec un lag proportionnel au poids
        /*if (rotateWithCamera)
        {
            Quaternion targetRot = ViewCamera.transform.rotation * grabbedRotationOffset;
            float rotSpeed = rotationLerpSpeed * strength;
            heldBody.MoveRotation(Quaternion.Slerp(heldBody.rotation, targetRot, Time.fixedDeltaTime * rotSpeed));
        }*/

        if (toTarget.magnitude > grabDistance * 1f)
            Release(false);
    }

    private void Release(bool throwIt)
    {
        if (heldBody == null) return;

        if (throwIt)
        {
            // Le lancer est lui aussi pénalisé par le poids : un objet lourd part moins loin
            float strength = Mathf.Clamp(massToStrength.Evaluate(heldObjectMass), 0.05f, 1f);
            heldBody.AddForce(ViewCamera.transform.forward * throwForce * strength, ForceMode.Impulse);
        }
        else
        {
            heldBody.angularVelocity = Vector3.zero;
            //heldBody.linearVelocity = Vector3.zero;
        }

        heldBody = null;
        controller.SpeedMalus = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (ViewCamera == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(ViewCamera.transform.position,
            ViewCamera.transform.position + ViewCamera.transform.forward * grabDistance);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (heldBody!= null && collision.gameObject == heldBody.gameObject)
            return;

        int IncomingLayer = LayerMask.NameToLayer("Grabable");
        if (collision.gameObject.layer != IncomingLayer)
        {
            //Check velocity (maybe Y velocity) And apply Damage
            Debug.Log("Collision avec random");
        }
        else
        {
            Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();

            if (rb.linearVelocity.magnitude > VelocityPerMass.Evaluate(rb.mass))
            {
                //appliquer des degats
                //PlayerLife -= DamagePerMass.Evaluate(rb.mass)
                controller.Ragdoll(true, RagdollRecoveryTime, RagdollMinVelocity);
                controller.PushPlayer(collision.GetContact(0).point, collision.GetContact(0).impulse);
            }
        }
    }

}
