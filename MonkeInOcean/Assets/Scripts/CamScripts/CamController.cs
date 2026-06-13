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

	[Header("References")]
	[SerializeField] private Transform playerBody;
	[SerializeField] private Transform cameraChild;
	[SerializeField] private Transform headBone;

	private PlayerInputActions inputs;

	private float targetYaw;
	private float targetPitch;
	private float currentYaw;
	private float currentPitch;

	private void Awake()
	{
		inputs = new PlayerInputActions();
	}

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
		FollowHead();
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
		// lerp current toward target — no overshoot, consistent ease
		currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotationSmoothSpeed * Time.deltaTime);
		currentPitch = Mathf.LerpAngle(currentPitch, targetPitch, rotationSmoothSpeed * Time.deltaTime);

		// player body owns yaw — movement and swim direction stay aligned
		playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);

		// camera child owns pitch as local rotation
		// inherits yaw from Player → CamHolder → Camera hierarchy
		cameraChild.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
	}

	private void FollowHead()
	{
		// snap camera world position to head bone every frame
		// handles all animation states — walk, swim, surface bob
		cameraChild.position = headBone.position;
	}
}