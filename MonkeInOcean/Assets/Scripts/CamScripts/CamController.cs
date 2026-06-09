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
	[SerializeField] private float swimOffsetSmooth = 12f;

	[Header("Pitch Clamp")]
	[SerializeField] private float minPitch = -80f;
	[SerializeField] private float maxPitch = 80f;

	[Header("References")]
	[SerializeField] private Transform playerBody;
	[SerializeField] private Transform headBone;
	[SerializeField] private Transform camHolder;
	[SerializeField] private Transform controlledCamera;
	[SerializeField] private Transform riggedModel;

	[Header("Model")]
	[SerializeField] private float riggedModelYawOffset;

	[Header("Camera Offset")]
	[SerializeField] private bool useCurrentCamHolderOffsetOnStart = true;
	[SerializeField] private Vector3 standingLocalOffset = new Vector3(0f, 3f, 1f);
	[SerializeField] private Vector3 swimmingLocalOffset = new Vector3(0f, 3f, 1.75f);

	private PlayerInputActions inputs;
	private PlayerMovement playerMovement;

	private float targetYaw;
	private float targetPitch;

	private float currentYaw;
	private float currentPitch;

	private float yawVelocity;
	private float pitchVelocity;

	public float CurrentYaw => currentYaw;

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

		if (camHolder == null)
		{
			controlledCamera = FindControlledCamera();
			camHolder = controlledCamera == transform && transform.parent != null
				? transform.parent
				: transform;
		}
		else if (controlledCamera == null)
		{
			controlledCamera = FindControlledCamera();
		}

		if (playerBody == null && camHolder.parent != null)
			playerBody = camHolder.parent;

		if (riggedModel == null && playerBody != null)
			riggedModel = playerBody.Find("RiggedModel");

		if (playerBody != null)
			playerMovement = playerBody.GetComponent<PlayerMovement>();

		if (useCurrentCamHolderOffsetOnStart && camHolder != null)
			standingLocalOffset = camHolder.localPosition;

		targetYaw = playerBody != null ? playerBody.eulerAngles.y : camHolder.eulerAngles.y;
		targetPitch = NormalizeAngle(controlledCamera.localEulerAngles.x);

		currentYaw = targetYaw;
		currentPitch = targetPitch;
	}

	private void LateUpdate()
	{
		HandleLook();
		FollowModelYaw();
	}

	private void UpdateCamHolderPosition()
	{
		if (camHolder == null)
			return;

		if (playerBody != null && camHolder.parent == playerBody)
		{
			Vector3 targetOffset = playerMovement != null && playerMovement.isSwimming
				? swimmingLocalOffset
				: standingLocalOffset;

			camHolder.localPosition = Vector3.Lerp(
				camHolder.localPosition,
				targetOffset,
				swimOffsetSmooth * Time.deltaTime);
			return;
		}

		if (headBone == null)
			return;

		camHolder.position = Vector3.Lerp(
			camHolder.position,
			headBone.position,
			positionSmooth * Time.deltaTime);
	}

	private void HandleLook()
	{
		UpdateCamHolderPosition();

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

		if (playerBody != null)
		{
			// Player owns yaw so movement, model, and camera all face the same way.
			playerBody.rotation =
				Quaternion.Euler(0f, currentYaw, 0f);

			if (camHolder != null && camHolder != controlledCamera && camHolder.parent == playerBody)
				camHolder.localRotation = Quaternion.identity;
		}
		else if (camHolder != null)
		{
			camHolder.rotation =
				Quaternion.Euler(0f, currentYaw, 0f);
		}

		if (controlledCamera == camHolder)
		{
			controlledCamera.rotation =
				Quaternion.Euler(currentPitch, currentYaw, 0f);
			return;
		}

		// Camera owns pitch only.
		controlledCamera.localRotation =
			Quaternion.Euler(currentPitch, 0f, 0f);
	}

	private void FollowModelYaw()
	{
		if (riggedModel == null)
			return;

		if (playerBody != null && riggedModel.IsChildOf(playerBody))
		{
			riggedModel.localRotation =
				Quaternion.Euler(0f, riggedModelYawOffset, 0f);
			return;
		}

		riggedModel.rotation =
			Quaternion.Euler(0f, currentYaw + riggedModelYawOffset, 0f);
	}

	private static float NormalizeAngle(float angle)
	{
		return angle > 180f ? angle - 360f : angle;
	}

	private Transform FindControlledCamera()
	{
		Camera ownCamera = GetComponent<Camera>();
		if (ownCamera != null)
			return ownCamera.transform;

		Camera childCamera = GetComponentInChildren<Camera>();
		return childCamera != null ? childCamera.transform : transform;
	}
}
