using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Inventory inventory;
	[SerializeField] private GameObject panelRoot; // whole inventory panel, toggled with Tab
	[SerializeField] private Transform slotParent;
	[SerializeField] private GameObject slotPrefab; // prefab with Image (icon) + TextMeshProUGUI (quantity)

	[Header("Selection")]
	[SerializeField] private Color normalSlotColor = new Color(1f, 1f, 1f, 0.4f);
	[SerializeField] private Color selectedSlotColor = new Color(1f, 0.85f, 0.3f, 0.9f);

	public bool IsOpen { get; private set; }
	public int SelectedSlotIndex { get; private set; } = -1;

	// raised when a slot is double-clicked (use) or right-clicked (drop)
	public event Action<int> OnSlotUseRequested;
	public event Action<int> OnSlotDropRequested;

	// cached per-slot UI pieces so RefreshUI never does transform.Find
	private Image[] icons;
	private TextMeshProUGUI[] quantityTexts;
	private Image[] backgrounds;

	private void Start()
	{
		BuildSlots();
		inventory.OnInventoryChanged += RefreshUI;
		RefreshUI();

		if (panelRoot != null)
			panelRoot.SetActive(false);
		IsOpen = false;
	}

	private void OnDestroy()
	{
		if (inventory != null)
			inventory.OnInventoryChanged -= RefreshUI;
	}

	public void Toggle()
	{
		IsOpen = !IsOpen;

		if (panelRoot != null)
			panelRoot.SetActive(IsOpen);

		if (!IsOpen)
			SetSelectedSlot(-1);
	}

	private void BuildSlots()
	{
		int count = inventory.Slots.Length;
		icons = new Image[count];
		quantityTexts = new TextMeshProUGUI[count];
		backgrounds = new Image[count];

		for (int i = 0; i < count; i++)
		{
			GameObject slotUI = Instantiate(slotPrefab, slotParent);

			icons[i] = slotUI.transform.Find("Icon")?.GetComponent<Image>();
			quantityTexts[i] = slotUI.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();
			backgrounds[i] = slotUI.GetComponent<Image>();

			// pointer handling: left single = select, left double = use, right = drop
			if (!slotUI.TryGetComponent(out InventorySlotButton slotButton))
				slotButton = slotUI.AddComponent<InventorySlotButton>();

			int index = i;
			slotButton.index = index;
			slotButton.onSelect = SetSelectedSlot;
			slotButton.onUse = idx => OnSlotUseRequested?.Invoke(idx);
			slotButton.onDrop = idx => OnSlotDropRequested?.Invoke(idx);
		}
	}

	private void SetSelectedSlot(int index)
	{
		// clicking an empty slot or the selected slot again deselects
		if (index >= 0 && (inventory.GetSlot(index).IsEmpty || index == SelectedSlotIndex))
			index = -1;

		SelectedSlotIndex = index;
		RefreshSelectionHighlight();
	}

	private void RefreshSelectionHighlight()
	{
		for (int i = 0; i < backgrounds.Length; i++)
		{
			if (backgrounds[i] == null) continue;
			backgrounds[i].color = i == SelectedSlotIndex ? selectedSlotColor : normalSlotColor;
		}
	}

	private void RefreshUI()
	{
		for (int i = 0; i < inventory.Slots.Length; i++)
		{
			InventorySlot slot = inventory.GetSlot(i);

			if (slot.IsEmpty)
			{
				if (icons[i] != null) icons[i].enabled = false;
				if (quantityTexts[i] != null) quantityTexts[i].text = "";
			}
			else
			{
				if (icons[i] != null)
				{
					icons[i].enabled = true;
					icons[i].sprite = slot.item.icon;
				}

				if (quantityTexts[i] != null)
					quantityTexts[i].text = slot.quantity.ToString();
			}
		}

		// drop selection if the selected slot became empty
		if (SelectedSlotIndex >= 0 && inventory.GetSlot(SelectedSlotIndex).IsEmpty)
			SelectedSlotIndex = -1;

		RefreshSelectionHighlight();
	}
}
