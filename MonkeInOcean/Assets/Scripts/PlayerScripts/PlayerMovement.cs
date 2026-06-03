using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float walkSpeed = 4f;
	[SerializeField] private float sprintSpeed = 8f;
	[SerializeField] private float swimSpeed = 2.5f;

	[Header("Jump")]
	[SerializeField] private float jumpForce = 5f;
	[SerializeField] private float groundOffset = 0.1f;
	[SerializeField] private LayerMask groundMask;

	[Header("Swimming")]
	[SerializeField] private LayerMask waterMask;
	[SerializeField] private float surfaceThreshold = 0.3f;

	[Header("Attack")]
	[SerializeField] private float attackCooldown = 0.6f;

	[Header("References")]
	[SerializeField] private Animator animator;

	private Rigidbody rb;
	private PlayerInputActions inputs;

	private Vector2 moveInput;
	private bool isSprinting;
	private bool isGrounded;
	private bool isSwimming;
	private bool isAtSurface;
	private float attackTimer;

	// animator hashes
	private static readonly int HashSpeed = Animator.StringToHash("Speed");
	private static readonly int HashGrounded = Animator.StringToHash("IsGrounded");
	private static readonly int HashSwimming = Animator.StringToHash("IsSwimming");
	private static readonly int HashAtSurface = Animator.StringToHash("IsAtSurface");
	private static readonly int HashAttack = Animator.StringToHash("Attack");

	// ─────────────────────────────────────────
	// Lifecycle
	// ─────────────────────────────────────────
	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		inputs = new PlayerInputActions();
	}

	private void OnEnable()
	{
		inputs.Player.Enable();
		inputs.Player.Attack.performed += OnAttack;
		inputs.Player.Jump.performed += OnJump;
	}

	private void OnDisable()
	{
		inputs.Player.Attack.performed -= OnAttack;
		inputs.Player.Jump.performed -= OnJump;
		inputs.Player.Disable();
	}

	private void Update()
	{
		moveInput = inputs.Player.Move.ReadValue<Vector2>();
		isSprinting = inputs.Player.Sprint.IsPressed();
		attackTimer -= Time.deltaTime;

		CheckGround();
		CheckWater();
		UpdateAnimator();
	}

	private void FixedUpdate()
	{
		if (isSwimming)
			HandleSwim();
		else
			HandleGroundMovement();
	}

	// ─────────────────────────────────────────
	// Ground movement
	// ─────────────────────────────────────────
	private void HandleGroundMovement()
	{
		float speed = isSprinting ? sprintSpeed : walkSpeed;

		Vector3 move = transform.right * moveInput.x
					 + transform.forward * moveInput.y;

		// set velocity directly — tight and responsive
		Vector3 targetVelocity = move * speed;
		targetVelocity.y = rb.linearVelocity.y; // preserve gravity/jump Y

		rb.linearVelocity = targetVelocity;
	}

	// ─────────────────────────────────────────
	// Swim movement
	// ─────────────────────────────────────────
	private void HandleSwim()
	{
		Vector3 move = transform.right * moveInput.x
					 + transform.forward * moveInput.y;

		if (isAtSurface && inputs.Player.Jump.IsPressed())
			move += Vector3.up;
		else if (!isAtSurface)
			move += Vector3.down * 0.3f;

		rb.linearVelocity = move * swimSpeed;
	}

	// ─────────────────────────────────────────
	// Jump
	// ─────────────────────────────────────────
	private void OnJump(InputAction.CallbackContext ctx)
	{
		if (!isGrounded || isSwimming) return;

		rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
		rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
	}

	// ─────────────────────────────────────────
	// Attack
	// ─────────────────────────────────────────
	private void OnAttack(InputAction.CallbackContext ctx)
	{
		if (attackTimer > 0f) return;

		attackTimer = attackCooldown;
		animator.SetTrigger(HashAttack);
	}

	// ─────────────────────────────────────────
	// Checks
	// ─────────────────────────────────────────
	private void CheckGround()
	{
		Vector3 spherePos = transform.position + Vector3.down * groundOffset;

		isGrounded = Physics.CheckSphere(
			spherePos,
			0.2f,
			groundMask
		);
	}

	private void CheckWater()
	{
		bool inWater = Physics.CheckSphere(
			transform.position + Vector3.up * 0.5f,
			0.3f,
			waterMask
		);

		isAtSurface = inWater && !Physics.CheckSphere(
			transform.position + Vector3.up * 1.2f,
			surfaceThreshold,
			waterMask
		);

		isSwimming = inWater;

		// disable gravity when swimming so Rigidbody doesn't fight the swim
		rb.useGravity = !isSwimming;
	}

	// ─────────────────────────────────────────
	// Animator
	// ─────────────────────────────────────────
	private void UpdateAnimator()
	{
		Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

		// normalize to 0-1 range so blend tree thresholds are clean
		float speedNormalized = Mathf.Clamp01(horizontalVel.magnitude / sprintSpeed);

		animator.SetFloat(HashSpeed, speedNormalized, 0.05f, Time.deltaTime);
		animator.SetBool(HashGrounded, isGrounded);
		animator.SetBool(HashSwimming, isSwimming);
		animator.SetBool(HashAtSurface, isAtSurface);
	}
}