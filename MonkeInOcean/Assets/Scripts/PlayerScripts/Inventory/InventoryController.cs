using UnityEngine;
using UnityEngine.InputSystem;

// Handles inventory keybinds: Tab = open/close, F = use selected, Q = drop selected.
public class InventoryController : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Inventory inventory;
	[SerializeField] private InventoryUI inventoryUI;
	[SerializeField] private LifeProcess lifeProcess;
	[SerializeField] private Transform cameraTransform;

	// checked by CamController / PlayerMovement / PlayerInteraction to freeze gameplay
	public static bool GameplayBlocked { get; private set; }

	private PlayerInputActions inputs;

	private void Awake()
	{
		inputs = new PlayerInputActions();
	}

	private void OnEnable()
	{
		inputs.Player.Enable();
		inputs.Player.ToggleInventory.performed += OnToggleInventory;
		inputs.Player.UseItem.performed += OnUseItem;
		inputs.Player.DropItem.performed += OnDropItem;

		inventoryUI.OnSlotUseRequested += OnSlotUseRequested;
		inventoryUI.OnSlotDropRequested += OnSlotDropRequested;
	}

	private void OnDisable()
	{
		inputs.Player.ToggleInventory.performed -= OnToggleInventory;
		inputs.Player.UseItem.performed -= OnUseItem;
		inputs.Player.DropItem.performed -= OnDropItem;
		inputs.Player.Disable();

		if (inventoryUI != null)
		{
			inventoryUI.OnSlotUseRequested -= OnSlotUseRequested;
			inventoryUI.OnSlotDropRequested -= OnSlotDropRequested;
		}

		SetBlocked(false);
	}

	// double-click a slot to consume it
	private void OnSlotUseRequested(int slotIndex)
	{
		inventory.UseItem(slotIndex, lifeProcess);
	}

	// right-click a slot to drop it
	private void OnSlotDropRequested(int slotIndex)
	{
		inventory.DropItem(slotIndex, cameraTransform);
	}

	private void OnToggleInventory(InputAction.CallbackContext ctx)
	{
		inventoryUI.Toggle();
		SetBlocked(inventoryUI.IsOpen);
	}

	private void OnUseItem(InputAction.CallbackContext ctx)
	{
		if (!inventoryUI.IsOpen) return;
		if (inventoryUI.SelectedSlotIndex < 0) return;

		inventory.UseItem(inventoryUI.SelectedSlotIndex, lifeProcess);
	}

	private void OnDropItem(InputAction.CallbackContext ctx)
	{
		if (!inventoryUI.IsOpen) return;
		if (inventoryUI.SelectedSlotIndex < 0) return;

		inventory.DropItem(inventoryUI.SelectedSlotIndex, cameraTransform);
	}

	private void SetBlocked(bool blocked)
	{
		GameplayBlocked = blocked;
		Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked;
		Cursor.visible = blocked;
	}
}
