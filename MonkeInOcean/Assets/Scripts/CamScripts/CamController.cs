using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
	[Header("Sensitivity")]
	[SerializeField] private float sensitivityX = 2f;
	[SerializeField] private float sensitivityY = 2f;

	[Header("Smoothing")]
	[SerializeField] private float rotationSmoothSpeed = 8f;
	[SerializeField] private float offsetSmoothSpeed = 5f;

	[Header("Pitch Clamp")]
	[SerializeField] private float minPitch = -80f;
	[SerializeField] private float maxPitch = 80f;

	[Header("Eye Level")]
	[SerializeField] private float eyeLevelOffset = 2.2f;
	[SerializeField] private float swimEyeLevelOffset = 1.5f;
	[SerializeField] private float swimBackOffset = 1.0f;

	[Header("References")]
	[SerializeField] private Transform playerBody;
	[SerializeField] private Rigidbody playerRb;
	[SerializeField] private Transform cameraTransform; // Main Camera, child of Player
	[SerializeField] private PlayerMovement playerMovement;

	private PlayerInputActions inputs;

	private float targetYaw;
	private float targetPitch;
	private float currentYaw;
	private float currentPitch;
	private Vector3 currentOffset;

	private void Awake() => inputs = new PlayerInputActions();
	private void OnEnable() => inputs.Player.Enable();
	private void OnDisable() => inputs.Player.Disable();

	public float CurrentPitch => currentPitch;

	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		targetYaw = playerBody.eulerAngles.y;
		currentYaw = targetYaw;
		targetPitch = 0f;
		currentPitch = 0f;

		currentOffset = ComputeTargetOffset();
	}

	private void Update()
	{
		ReadInput();
	}

	private void FixedUpdate()
	{
		// Rigidbody just tracks the already-smoothed yaw so movement stays view-relative.
		playerRb.MoveRotation(Quaternion.Euler(0f, currentYaw, 0f));
	}

	private void LateUpdate()
	{
		float t = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
		currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, t);
		currentPitch = Mathf.LerpAngle(currentPitch, targetPitch, t);

		UpdateCameraPosition();

		// World-space rotation: camera no longer depends on the physics-stepped parent yaw.
		cameraTransform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
	}

	private void ReadInput()
	{
		if (InventoryController.GameplayBlocked) return;

		Vector2 look = inputs.Player.Look.ReadValue<Vector2>();
		targetYaw += look.x * sensitivityX;
		targetPitch -= look.y * sensitivityY;
		targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
	}

	private Vector3 ComputeTargetOffset()
	{
		bool swimming = playerMovement.isSwimming;
		float heightOffset = swimming ? swimEyeLevelOffset : eyeLevelOffset;

		Vector3 offset = Vector3.up * heightOffset + Vector3.forward * 1f;

		if (swimming)
			offset -= (Vector3.forward * swimBackOffset + Vector3.up * 2f);

		return offset;
	}

	private void UpdateCameraPosition()
	{
		float t = 1f - Mathf.Exp(-offsetSmoothSpeed * Time.deltaTime);
		currentOffset = Vector3.Lerp(currentOffset, ComputeTargetOffset(), t);

		// Offset follows the smoothed camera yaw, not the physics yaw of the player root.
		cameraTransform.position = playerBody.position + Quaternion.Euler(0f, currentYaw, 0f) * currentOffset;
	}
}
