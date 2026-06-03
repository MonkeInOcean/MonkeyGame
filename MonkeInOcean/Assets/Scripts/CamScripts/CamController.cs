using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
	[Header("Sensitivity")]
	[SerializeField] private float sensitivityX = 0.2f;
	[SerializeField] private float sensitivityY = 0.2f;

	[Header("Pitch Clamp")]
	[SerializeField] private float minPitch = -80f;
	[SerializeField] private float maxPitch = 80f;

	[Header("References")]
	[SerializeField] private Transform playerBody;

	private PlayerInputActions inputs;
	private float pitch;

	// ─────────────────────────────────────────
	// Lifecycle
	// ─────────────────────────────────────────
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
	}

	// LateUpdate ensures the player has moved before the camera follows
	private void LateUpdate()
	{
		HandleLook();
	}

	// ─────────────────────────────────────────
	// Look — no smoothing, raw delta = crisp
	// ─────────────────────────────────────────
	private void HandleLook()
	{
		Vector2 lookInput = inputs.Player.Look.ReadValue<Vector2>();

		// rotate player body horizontally
		playerBody.Rotate(Vector3.up * lookInput.x * sensitivityX);

		// pitch camera vertically
		pitch -= lookInput.y * sensitivityY;
		pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

		transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
	}
}