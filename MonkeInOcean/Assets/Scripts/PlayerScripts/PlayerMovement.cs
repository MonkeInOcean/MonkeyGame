using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float walkSpeed = 4f;
	[SerializeField] private float sprintSpeed = 8f;

	[Header("Swimming")]
	[SerializeField] private float swimSpeedSubmerged = 3.5f;
	[SerializeField] private float swimSpeedSurface = 5.5f;
	[SerializeField] private float swimAcceleration = 12f;
	[SerializeField] private float swimDeceleration = 8f;
	[SerializeField] private LayerMask waterMask;

	[SerializeField] private float surfaceThreshold = 0.3f;
	
	[SerializeField] private Transform cameraTransform;
	[SerializeField] private float swimTurnSpeed = 8f;
	[SerializeField] private float swimDrag = 2.5f;
	[SerializeField] private float surfaceBoostMultiplier = 1.6f;
	[SerializeField] private float verticalSpeed = 0.8f;

	[Header("Jump")]
	[SerializeField] private float jumpForce = 5f;
	[SerializeField] private float groundOffset = 0.1f;
	[SerializeField] private LayerMask groundMask;
	[SerializeField] private float jumpHeight = 1.5f;
	[SerializeField] private float gravity = -9.81f;

	[Header("Attack")]
	[SerializeField] private float attackCooldown = 0.6f;

	[Header("References")]
	[SerializeField] private Animator animator;

	[Header("Particles")]
	[SerializeField] private ParticleSystem runParticle;
	[SerializeField] private ParticleSystem jumpParticle;

	private Rigidbody rb;
	private PlayerInputActions inputs;

	private Vector2 moveInput;
	private bool isSprinting;
	private bool isGrounded;
	private bool isSwimming;
	private bool isAtSurface;
	private float attackTimer;

	// swim state
	private Vector3 swimVelocity;

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
		rb.useGravity = false;

		inputs = new PlayerInputActions();

		runParticle.Stop();
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
		{
			HandleSwim();
		}
		else
		{
			HandleGroundMovement();
			ApplyGravity();
		}
	}

	// ─────────────────────────────────────────
	// Ground movement
	// ─────────────────────────────────────────
	private void HandleGroundMovement()
	{
		float speed = isSprinting ? sprintSpeed : walkSpeed;

		Vector3 move = transform.right * moveInput.x
					 + transform.forward * moveInput.y;

		Vector3 targetVelocity = move * speed;
		targetVelocity.y = rb.linearVelocity.y;

		rb.linearVelocity = targetVelocity;
	}

	// ─────────────────────────────────────────
	// Swim movement — omnidirectional with momentum
	// ─────────────────────────────────────────
	private void HandleSwim()
	{
		// 1. CAMERA-RELATIVE DIRECTION (core Subnautica feel)
		Vector3 camForward = cameraTransform.forward;
		Vector3 camRight = cameraTransform.right;

		// flatten camera vectors so looking up/down doesn't break movement
		camForward.y = 0f;
		camRight.y = 0f;
		camForward.Normalize();
		camRight.Normalize();

		Vector3 moveDir =
			camRight * moveInput.x +
			camForward * moveInput.y;

		// 2. vertical control (continuous, not impulse)
		if (inputs.Player.Jump.IsPressed())
			moveDir += Vector3.up * verticalSpeed;
		else if (!isAtSurface)
			moveDir += Vector3.down * 2.5f; // gentle sink

		// 3. speed tuning
		float baseSpeed = isAtSurface ? swimSpeedSurface : swimSpeedSubmerged;

		if (isAtSurface)
			baseSpeed *= surfaceBoostMultiplier;

		// 4. target velocity (NO normalization killing analog input)
		Vector3 targetVelocity = moveDir * baseSpeed;

		// 5. SMOOTH ACCELERATION (Subnautica-like inertia)
		float accel = (moveDir.sqrMagnitude > 0.01f)
			? swimAcceleration
			: swimDeceleration;

		swimVelocity = Vector3.Lerp(
			swimVelocity,
			targetVelocity,
			accel * Time.fixedDeltaTime
		);

		swimVelocity = Vector3.ClampMagnitude(swimVelocity, 12f);

		// 6. APPLY DRAG (important for underwater feel)
		swimVelocity = Vector3.Lerp(
			swimVelocity,
			Vector3.zero,
			swimDrag * Time.fixedDeltaTime * (moveDir.sqrMagnitude < 0.01f ? 1f : 0f)
		);

		// 7. APPLY FINAL VELOCITY
		rb.linearVelocity = swimVelocity;
	}

	// ─────────────────────────────────────────
	// Jump
	// ─────────────────────────────────────────
	private void OnJump(InputAction.CallbackContext ctx)
	{
		if (!isGrounded || isSwimming)
			return;

		Vector3 velocity = rb.linearVelocity;
		velocity.y = 0f;
		rb.linearVelocity = velocity;

		ParticleSystem p = Instantiate(jumpParticle, transform.position + Vector3.down * 0.5f, Quaternion.identity);
		p.Play();

		Destroy(p.gameObject, p.main.duration + p.main.startLifetime.constantMax);

		float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

		rb.AddForce(
			Vector3.up * jumpVelocity,
			ForceMode.VelocityChange);
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

	private void ApplyGravity()
	{
		if (isSwimming)
			return;

		rb.AddForce(
			Vector3.up * gravity,
			ForceMode.Acceleration);
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

		// reset swim velocity when exiting water
		if (!isSwimming)
			swimVelocity = Vector3.zero;

		rb.useGravity = !isSwimming;
	}

	// ─────────────────────────────────────────
	// Animator
	// ─────────────────────────────────────────
	private void UpdateAnimator()
	{
		Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

		float speedNormalized = Mathf.Clamp01(horizontalVel.magnitude / sprintSpeed) * 2;

		if (speedNormalized > 1.5f && isGrounded)
		{
			if (!runParticle.isPlaying)
				runParticle.Play();
		}
		else
		{
			if (runParticle.isPlaying)
				runParticle.Stop();
		}

		animator.SetFloat(HashSpeed, speedNormalized, 0.05f, Time.deltaTime);
		animator.SetBool(HashGrounded, isGrounded);
		animator.SetBool(HashSwimming, isSwimming);
		animator.SetBool(HashAtSurface, isAtSurface);
	}
}