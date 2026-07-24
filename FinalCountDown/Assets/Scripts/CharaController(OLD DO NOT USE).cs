using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Character controller basé sur Rigidbody pour Unity 6, utilisant le package Input System.
/// À placer sur le GameObject du joueur, qui doit avoir un Rigidbody + CapsuleCollider.
///
/// PRÉREQUIS :
/// - Package "Input System" installé (Window > Package Manager)
/// - Dans Project Settings > Player > Active Input Handling : "Input System Package (New)" ou "Both"
///
/// Setup recommandé :
/// - Rigidbody : la rotation est gelée automatiquement par script
/// - CapsuleCollider : hauteur normale = position debout, réduite en position accroupie
/// - Assigner "groundMask" au(x) layer(s) du sol
/// - Assigner "cameraTransform" (ex: Main Camera) pour un déplacement relatif à la caméra
///
/// Aucun asset .inputactions n'est requis : les actions sont créées et bindées en code
/// (clavier/souris + manette). Tu peux remplacer ça par des InputActionReference vers
/// ton propre asset si tu préfères centraliser tes bindings.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RigidbodyCharacterController : MonoBehaviour
{
    [Header("Mouvement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float airAcceleration = 4f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Saut")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Gravité")]
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float fallGravityMultiplier = 3.5f;

    [Header("Crouch")]
    [SerializeField] private float crouchingHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private LayerMask ceilingCheckMask = ~0; // layers considérés comme "plafond" pour se relever

    [Header("Détection du sol")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundMask;

    [Header("Caméra")]
    [SerializeField] private Transform cameraTransform;

    // ---------- Rigidbody / Collider ----------
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private float standingHeight;
    private float originalCenterY;

    // ---------- Input System ----------
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private Vector2 moveInput;
    private bool jumpHeld;

    // ---------- État sol / saut ----------
    private bool isGrounded;
    private float lastGroundedTime;
    private float lastJumpPressedTime = -999f;
    private bool hasJumpedThisPress;

    // ---------- État crouch ----------
    private bool wantsToCrouch;
    private bool isCrouching;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        standingHeight = capsule.height;
        originalCenterY = capsule.center.y;

        if (groundCheck == null)
        {
            GameObject go = new GameObject("GroundCheck");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, -capsule.height / 2f, 0f);
            groundCheck = go.transform;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        SetupInputActions();
    }

    // ---------- INPUT SYSTEM SETUP ----------

    private void SetupInputActions()
    {
        // Déplacement (WASD / flèches / stick gauche manette)
        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");

        // Saut
        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        // Crouch (maintenu ; passe à InputActionType.Button + un bool "toggle" si tu préfères un appui unique)
        crouchAction = new InputAction("Crouch", InputActionType.Button);
        crouchAction.AddBinding("<Keyboard>/leftCtrl");
        crouchAction.AddBinding("<Keyboard>/c");
        crouchAction.AddBinding("<Gamepad>/buttonEast");

        jumpAction.performed += ctx => { jumpHeld = true; OnJumpPressed(); };
        jumpAction.canceled += ctx => jumpHeld = false;

        crouchAction.performed += ctx => wantsToCrouch = true;
        crouchAction.canceled += ctx => wantsToCrouch = false;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        crouchAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        crouchAction.Disable();
    }

    private void OnJumpPressed()
    {
        lastJumpPressedTime = Time.time;
    }

    // ---------- LOOP ----------

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        CheckGround();
        HandleCrouchTransition();
    }

    private void FixedUpdate()
    {
        Move();
        ApplyBetterGravity();
        HandleJump();
    }

    // ---------- SOL ----------

    private void CheckGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            hasJumpedThisPress = false;
        }
    }

    private bool CanUseCoyoteTime => Time.time - lastGroundedTime <= coyoteTime;

    // ---------- SAUT ----------

    private void HandleJump()
    {
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;

        // Pas de saut en étant accroupi (comportement classique) ; retire "&& !isCrouching" pour l'autoriser
        if (jumpBuffered && (isGrounded || CanUseCoyoteTime) && !hasJumpedThisPress && !isCrouching)
        {
            float jumpVelocity = Mathf.Sqrt(2f * GetGravityStrength() * jumpHeight);
            Vector3 velocity = rb.linearVelocity;
            velocity.y = jumpVelocity;
            rb.linearVelocity = velocity;

            hasJumpedThisPress = true;
            lastJumpPressedTime = -999f;
        }
    }

    // ---------- GRAVITÉ ----------

    private float GetGravityStrength()
    {
        return Mathf.Abs(Physics.gravity.y) * gravityMultiplier;
    }

    private void ApplyBetterGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (fallGravityMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (gravityMultiplier - 1f) * 2f, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    // ---------- MOUVEMENT ----------

    private void Move()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        Vector3 worldDir = RelativeToCamera(inputDir);
        float targetSpeed = isCrouching ? crouchSpeed : moveSpeed;
        Vector3 targetVelocity = worldDir * targetSpeed;

        float accel = isGrounded ? acceleration : airAcceleration;

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 newHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * Time.fixedDeltaTime * moveSpeed);

        rb.linearVelocity = new Vector3(newHorizontalVelocity.x, rb.linearVelocity.y, newHorizontalVelocity.z);

        if (worldDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(worldDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private Vector3 RelativeToCamera(Vector3 inputDir)
    {
        if (cameraTransform == null)
            return inputDir;

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return camForward * inputDir.z + camRight * inputDir.x;
    }

    // ---------- CROUCH ----------

    private void HandleCrouchTransition()
    {
        // Si on veut se relever, vérifie d'abord qu'il n'y a pas de plafond au-dessus
        bool canStand = !isCrouching || !CeilingAbove();

        isCrouching = wantsToCrouch || (isCrouching && !canStand);

        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float newHeight = Mathf.Lerp(capsule.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        // Ajuste le centre pour que le personnage s'accroupisse "vers le bas"
        // en gardant les pieds au même endroit.
        float heightDiff = standingHeight - newHeight;
        float newCenterY = originalCenterY - heightDiff / 2f;

        capsule.height = newHeight;
        capsule.center = new Vector3(capsule.center.x, newCenterY, capsule.center.z);
    }

    private bool CeilingAbove()
    {
        float radius = capsule.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * capsule.height; // depuis le haut de la capsule accroupie
        float checkDistance = standingHeight - capsule.height + 0.05f;

        return Physics.SphereCast(origin, radius, Vector3.up, out _, checkDistance, ceilingCheckMask, QueryTriggerInteraction.Ignore);
    }

    // ---------- DEBUG ----------

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}