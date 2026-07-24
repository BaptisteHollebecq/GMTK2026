using UnityEngine;

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

    [Header("References")]
    public Transform ViewCamera;

    private Rigidbody body;
    private float verticalRotation = 0f;
    private Vector3 MovementVector = new Vector3();
    private bool isGrounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Update()
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
        var targetVelocity = InputVector * MovementSpeed;

        Vector3.SmoothDamp(MovementVector, targetVelocity, ref MovementVector, MovementDampening);

        //------------- Jumps

        isGrounded = Physics.Raycast(transform.position, Vector2.down, 1.1f);

        if (Input.GetKeyDown(Jump))
        {
            if (isGrounded)
            {
                Debug.Log("ground detected");
                body.AddForce(0, JumpPower, 0, ForceMode.Impulse);
            }
        }

        //-------------Gravity Amplifier

        if (!isGrounded)
            body.AddForce(0, -GravityAmplifier, 0, ForceMode.Acceleration);


    }

    private void FixedUpdate()
    {
        Vector3 newVelocity = new Vector3(MovementVector.x, body.linearVelocity.y, MovementVector.z);
        body.linearVelocity = newVelocity;
    }

}
