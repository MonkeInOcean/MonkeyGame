using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
	[SerializeField] private Inventory inventory;
	[SerializeField] private Transform slotParent;
	[SerializeField] private GameObject slotPrefab; // prefab with Image (icon) + TextMeshProUGUI (quantity)

	private GameObject[] slotUIs;

	private void Start()
	{
		BuildSlots();
		inventory.OnInventoryChanged += RefreshUI;
		RefreshUI();
	}

	private void OnDestroy()
	{
		if (inventory != null)
			inventory.OnInventoryChanged -= RefreshUI;
	}

	private void BuildSlots()
	{
		slotUIs = new GameObject[inventory.Slots.Length];

		for (int i = 0; i < inventory.Slots.Length; i++)
		{
			slotUIs[i] = Instantiate(slotPrefab, slotParent);
		}
	}

	private void RefreshUI()
	{
		for (int i = 0; i < inventory.Slots.Length; i++)
		{
			InventorySlot slot = inventory.GetSlot(i);
			GameObject slotUI = slotUIs[i];

			Image icon = slotUI.transform.Find("Icon")?.GetComponent<Image>();
			TextMeshProUGUI quantityText = slotUI.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();

			if (slot.IsEmpty)
			{
				if (icon != null) icon.enabled = false;
				if (quantityText != null) quantityText.text = "";
			}
			else
			{
				if (icon != null)
				{
					icon.enabled = true;
					icon.sprite = slot.item.icon;
				}

				if (quantityText != null)
					quantityText.text = slot.quantity > 1 ? slot.quantity.ToString() : "";
			}
		}
	}
}