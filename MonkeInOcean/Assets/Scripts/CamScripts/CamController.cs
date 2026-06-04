using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
	[Header("Sensitivity")]
	[SerializeField] private float sensitivityX = 2f;
	[SerializeField] private float sensitivityY = 2f;

	[Header("Look Smoothing")]
	[SerializeField] private float lookSmoothTime = 0.04f;

	[Header("Position Smoothing")]
	[SerializeField] private float positionSmooth = 15f;

	[Header("Pitch Clamp")]
	[SerializeField] private float minPitch = -80f;
	[SerializeField] private float maxPitch = 80f;

	[Header("References")]
	[SerializeField] private Transform playerBody;
	[SerializeField] private Transform headBone;

	private PlayerInputActions inputs;

	private float targetYaw;
	private float targetPitch;

	private float currentYaw;
	private float currentPitch;

	private float yawVelocity;
	private float pitchVelocity;

	private void Awake()
	{
		inputs = new PlayerInputActions();
	}

	private void OnEnable()
	{
		inputs.Player.Enable();
	}

	private void OnDisable()
	{
		inputs.Player.Disable();
	}

	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		targetYaw = playerBody.eulerAngles.y;
		currentYaw = targetYaw;
	}

	private void LateUpdate()
	{
		FollowHead();
		HandleLook();
	}

	private void FollowHead()
	{
		transform.position = Vector3.Lerp(
			transform.position,
			headBone.position,
			positionSmooth * Time.deltaTime);
	}

	private void HandleLook()
	{
		Vector2 lookInput = inputs.Player.Look.ReadValue<Vector2>();

		targetYaw += lookInput.x * sensitivityX;

		targetPitch -= lookInput.y * sensitivityY;
		targetPitch = Mathf.Clamp(
			targetPitch,
			minPitch,
			maxPitch);

		currentYaw = Mathf.SmoothDampAngle(
			currentYaw,
			targetYaw,
			ref yawVelocity,
			lookSmoothTime);

		currentPitch = Mathf.SmoothDampAngle(
			currentPitch,
			targetPitch,
			ref pitchVelocity,
			lookSmoothTime);

		// BODY = YAW ONLY
		playerBody.rotation =
			Quaternion.Euler(0f, currentYaw, 0f);

		// CAMERA = PITCH ONLY
		transform.localRotation =
			Quaternion.Euler(currentPitch, 0f, 0f);
	}
}