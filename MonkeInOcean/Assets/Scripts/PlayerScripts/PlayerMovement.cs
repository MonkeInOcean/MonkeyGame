using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float walkSpeed = 4f;
	[SerializeField] private float sprintSpeed = 8f;
	[SerializeField] private float underwaterWalkSpeed = 1.5f;

	[Header("Swimming")]
	[SerializeField] private float swimSpeedSubmerged = 3.5f;
	[SerializeField] private float swimSpeedSurface = 5.5f;
	[SerializeField] private float swimAcceleration = 12f;
	[SerializeField] private float swimDeceleration = 8f;
	[SerializeField] private float surfaceBoostMultiplier = 1.6f;
	[SerializeField] private float verticalSpeed = 2f;
	[SerializeField] private float sinkForce = 2.5f;

	[Header("Surface Bob")]
	[SerializeField] private float bobAmplitude = 0.04f;
	[SerializeField] private float bobFrequency = 0.8f;

	[Header("Water")]
	// Single source of truth for the water line — see Ocean.WaterLevel.
	private float waterSurfaceY => Ocean.WaterLevel.SurfaceY;
	[SerializeField] private float eyeLevelOffset = 2.2f;

	[Header("Jump")]
	[SerializeField] private float jumpHeight = 1.5f;
	[SerializeField] private float gravity = -9.81f;
	[SerializeField] private float groundOffset = 0.1f;
	[SerializeField] private LayerMask groundMask;

	[Header("Stamina")]
	[SerializeField] private float maxStamina = 100f;
	[SerializeField] private float staminaDrainRate = 20f;       // per second while sprinting
	[SerializeField] private float staminaRegenRate = 12f;       // per second when not sprinting
	[SerializeField] private float sprintRecoverThreshold = 20f; // must reach this % before sprint re-enables

	[Header("Attack")]
	[SerializeField] private float attackCooldown = 0.6f;

	[Header("References")]
	[SerializeField] private Animator animator;
	[SerializeField] private Transform cameraTransform;
	[SerializeField] private PlayerStats playerStats;
	[SerializeField] private LifeProcess lifeProcess;

	[Header("Particles")]
	[SerializeField] private ParticleSystem runParticle;
	[SerializeField] private ParticleSystem jumpParticle;

	[Header("Swim Dive Control")]
	[SerializeField] private CamController camController;
	[SerializeField] private float lookDownDiveAngle = 35f;
	[SerializeField] private float surfaceSettleSpeed = 6f;

	private bool wasSwimmingPrev;

	private Rigidbody rb;
	private PlayerInputActions inputs;

	private Vector2 moveInput;
	private bool isSprinting;
	private bool isGrounded;
	private bool isUndergroundGrounded;

	public bool isSwimming;
	public bool isAtSurface;
	public bool isSubmerged;

	public float CurrentStamina { get; private set; }
	public float GetStaminaPercent() => CurrentStamina / maxStamina;
	public event System.Action<float> OnStaminaChanged;
	private bool staminaExhausted;

	private float attackTimer;
	private Vector3 swimVelocity;
	private float bobTimer;

	private static readonly int HashSpeed = Animator.StringToHash("Speed");
	private static readonly int HashGrounded = Animator.StringToHash("IsGrounded");
	private static readonly int HashSwimming = Animator.StringToHash("IsSwimming");
	private static readonly int HashAtSurface = Animator.StringToHash("IsAtSurface");
	private static readonly int HashAttack = Animator.StringToHash("Attack");

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		rb.useGravity = false;
		if (lifeProcess == null) lifeProcess = GetComponent<LifeProcess>();
		inputs = new PlayerInputActions();
		runParticle.Stop();
		CurrentStamina = maxStamina;
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

	// mouse-look rotation now lives entirely in CamController
	private void Update()
	{
		bool blocked = InventoryController.GameplayBlocked
			|| (lifeProcess != null && lifeProcess.IsPooping);

		moveInput = blocked ? Vector2.zero : inputs.Player.Move.ReadValue<Vector2>();
		bool wantSprint = !blocked && inputs.Player.Sprint.IsPressed() && moveInput.sqrMagnitude > 0.01f;
		isSprinting = wantSprint && !staminaExhausted && CurrentStamina > 0f;
		UpdateStamina();
		attackTimer -= Time.deltaTime;

		UpdateWaterState();
		CheckGround();
		HandleOxygen();
		UpdateAnimator();
	}

	private void FixedUpdate()
	{
		if (isSwimming)
		{
			if (isUndergroundGrounded)
				HandleUnderwaterWalking();
			else if (isAtSurface)
				HandleSurface();
			else
				HandleSubmerged();
		}
		else
		{
			HandleGroundMovement();
			ApplyGravity();
		}
	}

	private void UpdateWaterState()
	{
		float feetY = transform.position.y;
		float eyeY = transform.position.y + eyeLevelOffset;

		isSwimming = feetY < waterSurfaceY;

		// preserve fall speed instead of discarding it the instant we enter water
		if (isSwimming && !wasSwimmingPrev)
			swimVelocity = rb.linearVelocity;

		bool geoAtSurface = isSubmerged
			? eyeY >= waterSurfaceY + 5f
			: eyeY >= waterSurfaceY + 4.5f;

		bool wantsToDive = inputs.Player.Dive.IsPressed()
			|| (camController.CurrentPitch > lookDownDiveAngle && moveInput.y > 0.1f);

		// while already calmly floating, only leave surface on explicit intent; a fresh fall ignores the gate
		if (isSwimming && isAtSurface && wasSwimmingPrev && !wantsToDive)
			isAtSurface = true;
		else
			isAtSurface = isSwimming && geoAtSurface;

		isSubmerged = isSwimming && !isAtSurface;

		isUndergroundGrounded = isSwimming && Physics.CheckSphere(
			transform.position + Vector3.down * groundOffset, 0.2f, groundMask
		);

		rb.useGravity = !isSwimming;

		if (!isSwimming)
		{
			swimVelocity = Vector3.zero;
			bobTimer = 0f;
		}

		wasSwimmingPrev = isSwimming;
	}

	public bool IsSprinting => isSprinting && moveInput.sqrMagnitude > 0.01f;

	public float WalkSpeed
	{
		get => walkSpeed;
		set => walkSpeed = value;
	}

	public float SprintSpeed
	{
		get => sprintSpeed;
		set => sprintSpeed = value;
	}

	private void HandleGroundMovement()
	{
		float speed = isSprinting ? sprintSpeed : walkSpeed;
		Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
		Vector3 targetVelocity = move * speed;
		targetVelocity.y = rb.linearVelocity.y;
		rb.linearVelocity = targetVelocity;
	}

	private void HandleUnderwaterWalking()
	{
		Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
		Vector3 targetVelocity = move * underwaterWalkSpeed;
		targetVelocity.y = rb.linearVelocity.y;
		rb.linearVelocity = targetVelocity;
	}

	private void HandleSurface()
	{
		bobTimer += Time.fixedDeltaTime;
		float bobOffset = Mathf.Sin(bobTimer * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
		float targetY = (waterSurfaceY - eyeLevelOffset) + bobOffset;

		Vector3 camForward = cameraTransform.forward;
		Vector3 camRight = cameraTransform.right;
		camForward.y = 0f;
		camRight.y = 0f;
		camForward.Normalize();
		camRight.Normalize();

		Vector3 horizontalMove = camRight * moveInput.x + camForward * moveInput.y;

		float speed = swimSpeedSurface * surfaceBoostMultiplier;
		swimVelocity = Vector3.Lerp(swimVelocity, horizontalMove * speed, swimAcceleration * Time.fixedDeltaTime);

		Vector3 newPos = rb.position + new Vector3(swimVelocity.x, 0f, swimVelocity.z) * Time.fixedDeltaTime;
		newPos.y = Mathf.MoveTowards(rb.position.y, targetY, surfaceSettleSpeed * Time.fixedDeltaTime);

		rb.MovePosition(newPos);
		rb.linearVelocity = Vector3.zero;
	}

	private void HandleSubmerged()
	{
		Vector3 moveDir = cameraTransform.forward * moveInput.y + cameraTransform.right * moveInput.x;

		if (inputs.Player.Jump.IsPressed())
			moveDir += Vector3.up * verticalSpeed;

		if (inputs.Player.Dive.IsPressed())
			moveDir += Vector3.down * verticalSpeed;

		Vector3 targetVelocity = moveDir * swimSpeedSubmerged;

		float accel = moveDir.sqrMagnitude > 0.01f ? swimAcceleration : swimDeceleration;

		swimVelocity = Vector3.Lerp(swimVelocity, targetVelocity, accel * Time.fixedDeltaTime);
		swimVelocity = Vector3.ClampMagnitude(swimVelocity, 12f);

		float oxygenRatio = playerStats.GetOxygenPercent();
		float currentSink = Mathf.Lerp(sinkForce * 1.5f, sinkForce * 0.3f, oxygenRatio);

		// Only sink while the head is actually underwater; at or above the surface
		// the player floats and breathes instead of being dragged back down.
		bool headUnderwater = (rb.position.y + eyeLevelOffset) < waterSurfaceY;
		if (headUnderwater)
			swimVelocity.y -= currentSink * Time.fixedDeltaTime;

		rb.linearVelocity = swimVelocity;
	}

	private void UpdateStamina()
	{
		if (isSprinting)
			CurrentStamina = Mathf.Max(0f, CurrentStamina - staminaDrainRate * Time.deltaTime);
		else
			CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenRate * Time.deltaTime);

		if (CurrentStamina <= 0f) staminaExhausted = true;
		else if (CurrentStamina >= sprintRecoverThreshold) staminaExhausted = false;

		OnStaminaChanged?.Invoke(GetStaminaPercent());
	}

	private void HandleOxygen()
	{
		// Breathe whenever the head is above water (surfaced or on land); only drain
		// while the head is submerged or while walking the seabed.
		bool headUnderwater = (transform.position.y + eyeLevelOffset) < waterSurfaceY;
		if (headUnderwater || isUndergroundGrounded)
			playerStats.DrainOxygen(isUndergroundGrounded, Time.deltaTime);
		else
			playerStats.RegenOxygen(Time.deltaTime);
	}

	private void OnJump(InputAction.CallbackContext ctx)
	{
		if (InventoryController.GameplayBlocked) return;
		if (lifeProcess != null && lifeProcess.IsPooping) return;

		if (isSwimming)
		{
			if (!isUndergroundGrounded) return;

			rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
			rb.AddForce(Vector3.up * Mathf.Sqrt(jumpHeight * -2f * gravity), ForceMode.VelocityChange);
			return;
		}

		if (!isGrounded) return;

		rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

		ParticleSystem p = Instantiate(jumpParticle, transform.position + Vector3.down * 0.5f, Quaternion.identity);
		p.Play();
		Destroy(p.gameObject, p.main.duration + p.main.startLifetime.constantMax);

		rb.AddForce(Vector3.up * Mathf.Sqrt(jumpHeight * -2f * gravity), ForceMode.VelocityChange);
	}

	private void OnAttack(InputAction.CallbackContext ctx)
	{
		if (InventoryController.GameplayBlocked) return;
		if (lifeProcess != null && lifeProcess.IsPooping) return;
		if (attackTimer > 0f) return;
		attackTimer = attackCooldown;
		animator.SetTrigger(HashAttack);
	}

	private void ApplyGravity()
	{
		rb.AddForce(Vector3.up * gravity, ForceMode.Acceleration);
	}

	private void CheckGround()
	{
		if (isSwimming) { isGrounded = false; return; }
		isGrounded = Physics.CheckSphere(
			transform.position + Vector3.down * groundOffset, 0.2f, groundMask
		);
	}

	private void UpdateAnimator()
	{
		Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
		float speedNormalized = Mathf.Clamp01(horizontalVel.magnitude / sprintSpeed) * 2;

		if (speedNormalized > 1.5f && isGrounded && !isSwimming)
		{
			if (!runParticle.isPlaying) runParticle.Play();
		}
		else
		{
			if (runParticle.isPlaying) runParticle.Stop();
		}

		animator.SetFloat(HashSpeed, speedNormalized, 0.05f, Time.deltaTime);
		animator.SetBool(HashGrounded, isGrounded);
		animator.SetBool(HashSwimming, isSwimming);
		animator.SetBool(HashAtSurface, isAtSurface);
	}

	private void OnDrawGizmos()
	{
		if (!Application.isPlaying) return;

		Gizmos.color = isSubmerged ? Color.red : Color.green;
		Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y + eyeLevelOffset, transform.position.z), 0.15f);

		Gizmos.color = isSwimming ? Color.blue : Color.gray;
		Gizmos.DrawWireSphere(transform.position, 0.15f);

		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(
			new Vector3(transform.position.x - 2f, waterSurfaceY, transform.position.z),
			new Vector3(transform.position.x + 2f, waterSurfaceY, transform.position.z)
		);

		Gizmos.color = isGrounded ? Color.yellow : Color.magenta;
		Gizmos.DrawWireSphere(transform.position + Vector3.down * groundOffset, 0.2f);

		Gizmos.color = isUndergroundGrounded ? Color.cyan : Color.black;
		Gizmos.DrawWireSphere(transform.position + Vector3.down * groundOffset, 0.25f);
	}
}