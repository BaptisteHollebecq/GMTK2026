using UnityEngine;
using DG.Tweening;
using System.Collections;

public class CharaController : MonoBehaviour
{

    [Header("Inputs Keys")]
    public KeyCode Forward;
    public KeyCode Back;
    public KeyCode Left;
    public KeyCode Right;
    public KeyCode Jump;
    public KeyCode Crouch;

    [Header("Movement Values")]
    public float MouseSensitivity = 2f;
    public float CameraMaxAngle = 60f;
    public float MovementSpeed = 12f;
    public float MovementDampening = .1f;
    public float JumpPower = 10f;
    public float GravityAmplifier = 1f;
    public float CrouchAnimTime = .15f;

    [Header("References")]
    public Transform ViewCamera;

    [HideInInspector]
    public float SpeedMalus = 0;
    [HideInInspector]
    public bool isRagdoll = false;


    private Rigidbody body;
    private CapsuleCollider capsule;
    private float verticalRotation = 0f;
    private Vector3 MovementVector = new Vector3();
    private float RagdollMinTime = 1;
    private float RagdollRecoveryTime = 1f;
    private float RagdollMinVelocity = 1;
    private bool isRecovering = false;
    private bool canRecover = false;


    private bool isGrounded = true;
    private bool isCrouched = false;


    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
            isRagdoll = !isRagdoll;

        if (!isRagdoll)
        {
            //------------- Horizontal Rotation

            float mouseX = Input.GetAxis("Mouse X") * MouseSensitivity;
            transform.Rotate(Vector3.up, mouseX);

            //------------- Vertical Camera Rotation

            float mouseY = Input.GetAxis("Mouse Y") * MouseSensitivity;
            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -CameraMaxAngle, CameraMaxAngle);

            ViewCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

            //-------------- Get the Movement Inputs

            Vector3 InputVector = Vector3.zero;
            if (Input.GetKey(Forward))
                InputVector += transform.forward;
            if (Input.GetKey(Back))
                InputVector += -transform.forward;
            if (Input.GetKey(Left))
                InputVector += -transform.right;
            if (Input.GetKey(Right))
                InputVector += transform.right;

            InputVector.Normalize();
            var targetVelocity = InputVector * Mathf.Max((MovementSpeed - SpeedMalus), 1);

            Vector3.SmoothDamp(MovementVector, targetVelocity, ref MovementVector, MovementDampening);

            //------------- Jumps

            isGrounded = Physics.Raycast(transform.position, Vector2.down, 1.1f);

            if (Input.GetKeyDown(Jump))
            {
                if (isGrounded)
                {
                    body.AddForce(0, JumpPower, 0, ForceMode.Impulse);
                }
            }

            //-------------Gravity Amplifier

            if (!isGrounded)
                body.AddForce(0, -GravityAmplifier, 0, ForceMode.Acceleration);

            //------------ Crouch

            if (Input.GetKeyDown(Crouch))
            {
                capsule.center = new Vector3(0, -.5f, 0);
                capsule.height = 1;
                ViewCamera.DOLocalMove(new Vector3(0, -.35f, 0), CrouchAnimTime);
                isCrouched = true;
            }
            if (Input.GetKeyUp(Crouch))
            {
                isCrouched = false;
                UnCrouch();
            }
            UnCrouch();
        }
        else
        {
            if (body.angularVelocity.magnitude < RagdollMinVelocity && !isRecovering && canRecover)
                StartCoroutine(RecoveryTime());
        }

    }

    private void FixedUpdate()
    {
        if (!isRagdoll)
        {
            Vector3 newVelocity = new Vector3(MovementVector.x, body.linearVelocity.y, MovementVector.z);
            body.linearVelocity = newVelocity;
        }
    }

    private IEnumerator RecoveryTime()
    {
        isRecovering = true;
        yield return new WaitForSeconds(RagdollRecoveryTime);

        Ragdoll(false);
    }

    private IEnumerator MinimumRagdollTime()
    {
        canRecover = false;
        yield return new WaitForSeconds(RagdollMinTime);
        canRecover = true;
    }

    private void UnCrouch()
    {
        if (!isCrouched)
        {
            if (CanGetUp())
            {
                capsule.center = new Vector3(0, 0, 0);
                capsule.height = 2;
                ViewCamera.DOLocalMove(new Vector3(0, .65f, 0), CrouchAnimTime);
                
            }

        }
    }

    private bool CanGetUp()
    {
        if (!Physics.Raycast(transform.position + new Vector3(-.5f, 0, 0), -Vector2.down, 1.1f) &&
            !Physics.Raycast(transform.position + new Vector3(.5f, 0, 0), -Vector2.down, 1.1f) &&
            !Physics.Raycast(transform.position + new Vector3(0, 0, -.5f), -Vector2.down, 1.1f) &&
            !Physics.Raycast(transform.position + new Vector3(0, 0, .5f), -Vector2.down, 1.1f))
        {
            return true;
        }
        return false;
    }

    public void Ragdoll(bool activate, float time = 1, float velocity = 1, float MinTime = 1)
    {
        if (activate)
        {
            RagdollRecoveryTime = time;
            RagdollMinVelocity = velocity;
            RagdollMinTime = MinTime;
            isRagdoll = true;
            body.freezeRotation = false;

            StartCoroutine(MinimumRagdollTime());
        }
        else
        {
            canRecover = false;
            isRecovering = false;
            isRagdoll = false;
            transform.rotation = Quaternion.identity;
            body.freezeRotation = true;
        }
    }

    public void PushPlayer(Vector3 pos, Vector3 push)
    {
        body.AddForceAtPosition(push, pos, ForceMode.Impulse);
    }

    public float GetVerticalVelocity()
    {
        return body.linearVelocity.y;
    }

}
