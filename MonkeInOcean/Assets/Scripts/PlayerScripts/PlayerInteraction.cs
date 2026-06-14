using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Transform cameraTransform;
	[SerializeField] private Inventory inventory;

	[Header("Settings")]
	[SerializeField] private float interactRange = 3f;
	[SerializeField] private LayerMask interactMask;

	[Header("UI Placeholder")]
	[SerializeField] private GameObject promptPanel;
	[SerializeField] private TextMeshProUGUI promptText;

	private PlayerInputActions inputs;
	private WorldItem currentTarget;

	private void Awake()
	{
		inputs = new PlayerInputActions();
	}

	private void OnEnable()
	{
		inputs.Player.Enable();
		inputs.Player.Interact.performed += OnInteractPressed;
	}

	private void OnDisable()
	{
		inputs.Player.Interact.performed -= OnInteractPressed;
		inputs.Player.Disable();
	}

	private void Update()
	{
		CheckForItem();
	}

	// ─────────────────────────────────────────
	// Raycast from camera center every frame
	// ─────────────────────────────────────────
	private void CheckForItem()
	{
		Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

		if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
		{
			WorldItem worldItem = hit.collider.GetComponent<WorldItem>();

			if (worldItem != null)
			{
				currentTarget = worldItem;
				ShowPrompt(worldItem.item.itemName);
				return;
			}
		}

		currentTarget = null;
		HidePrompt();
	}

	// ─────────────────────────────────────────
	// Pickup
	// ─────────────────────────────────────────
	private void OnInteractPressed(InputAction.CallbackContext ctx)
	{
		if (currentTarget == null) return;

		bool added = inventory.AddItem(currentTarget.item, currentTarget.quantity);

		if (added)
		{
			Destroy(currentTarget.gameObject);
			currentTarget = null;
			HidePrompt();
		}
		else
		{
			Debug.Log("[PlayerInteraction] Inventory full");
		}
	}

	// ─────────────────────────────────────────
	// Prompt UI placeholder
	// ─────────────────────────────────────────
	private void ShowPrompt(string itemName)
	{
		if (promptPanel == null) return;
		promptPanel.SetActive(true);

		if (promptText != null)
			promptText.text = $"Press E to pick up {itemName}";
	}

	private void HidePrompt()
	{
		if (promptPanel == null) return;
		promptPanel.SetActive(false);
	}
}