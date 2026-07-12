using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
	[Header("Sensitivity")]
	[SerializeField] private float sensitivityX = 2f;
	[SerializeField] private float sensitivityY = 2f;

	[Header("Smoothing")]
	[SerializeField] private float rotationSmoothSpeed = 8f;

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

		cameraTransform.localRotation = Quaternion.identity;
	}

	private void Update()
	{
		ReadInput();
	}

	private void FixedUpdate()
	{
		currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotationSmoothSpeed * Time.fixedDeltaTime);
		playerRb.MoveRotation(Quaternion.Euler(0f, currentYaw, 0f));
	}

	private void LateUpdate()
	{
		currentPitch = Mathf.LerpAngle(currentPitch, targetPitch, rotationSmoothSpeed * Time.deltaTime);
		cameraTransform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
		UpdateCameraPosition();
	}

	private void ReadInput()
	{
		Vector2 look = inputs.Player.Look.ReadValue<Vector2>();
		targetYaw += look.x * sensitivityX;
		targetPitch -= look.y * sensitivityY;
		targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
	}

	private void UpdateCameraPosition()
	{
		bool swimming = playerMovement.isSwimming;
		float heightOffset = swimming ? swimEyeLevelOffset : eyeLevelOffset;

		Vector3 localPos = Vector3.up * heightOffset + Vector3.forward * 1f;

		if (swimming)
			localPos -= (Vector3.forward * swimBackOffset + Vector3.up * 2f);

		cameraTransform.localPosition = localPos;
	}
}