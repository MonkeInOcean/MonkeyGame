using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
	[Header("Sensitivity")]
	[SerializeField] private float sensitivityX = 2f;
	[SerializeField] private float sensitivityY = 2f;

	[Header("Smoothing")]
	[SerializeField] private float rotationSmoothSpeed = 20f;

	[Header("Pitch Clamp")]
	[SerializeField] private float minPitch = -80f;
	[SerializeField] private float maxPitch = 80f;

	[Header("Eye Level")]
	[SerializeField] private float eyeLevelOffset = 2.2f;
	[SerializeField] private float swimEyeLevelOffset = 1.5f;

	[Header("References")]
	[SerializeField] private Transform playerBody;
	[SerializeField] private Transform camHolder;
	[SerializeField] private Transform cameraChild;
	[SerializeField] private PlayerMovement playerMovement;

	private PlayerInputActions inputs;

	private float targetYaw;
	private float targetPitch;
	private float currentYaw;
	private float currentPitch;

	private void Awake() => inputs = new PlayerInputActions();
	private void OnEnable() => inputs.Player.Enable();
	private void OnDisable() => inputs.Player.Disable();

	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		targetYaw = playerBody.eulerAngles.y;
		currentYaw = targetYaw;
		targetPitch = 0f;
		currentPitch = 0f;

		transform.localRotation = Quaternion.identity;
	}

	private void LateUpdate()
	{
		ReadInput();
		ApplyRotation();
		FollowPlayer();
	}

	private void ReadInput()
	{
		Vector2 look = inputs.Player.Look.ReadValue<Vector2>();
		targetYaw += look.x * sensitivityX;
		targetPitch -= look.y * sensitivityY;
		targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
	}

	private void ApplyRotation()
	{
		currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotationSmoothSpeed * Time.deltaTime);
		currentPitch = Mathf.LerpAngle(currentPitch, targetPitch, rotationSmoothSpeed * Time.deltaTime);

		playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);
		cameraChild.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
	}

	private void FollowPlayer()
	{
		float offset = playerMovement.isSwimming ? swimEyeLevelOffset : eyeLevelOffset;
		camHolder.position = playerBody.position + Vector3.up * offset;
	}
}