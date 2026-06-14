using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
	[SerializeField] private int slotCount = 24;

	public InventorySlot[] Slots { get; private set; }

	public event Action OnInventoryChanged;

	private void Awake()
	{
		Slots = new InventorySlot[slotCount];
		for (int i = 0; i < slotCount; i++)
			Slots[i] = new InventorySlot();
	}

	// ─────────────────────────────────────────
	// Add — tries to stack first, then fills empty slot
	// Returns true if fully added, false if inventory full (item not added)
	// ─────────────────────────────────────────
	public bool AddItem(ItemData item, int quantity = 1)
	{
		// try stacking onto existing slots first
		for (int i = 0; i < Slots.Length; i++)
		{
			if (Slots[i].item == item && Slots[i].quantity < item.maxStackSize)
			{
				int spaceLeft = item.maxStackSize - Slots[i].quantity;
				int toAdd = Mathf.Min(spaceLeft, quantity);

				Slots[i].quantity += toAdd;
				quantity -= toAdd;

				if (quantity <= 0)
				{
					OnInventoryChanged?.Invoke();
					return true;
				}
			}
		}

		// then fill empty slots
		for (int i = 0; i < Slots.Length; i++)
		{
			if (Slots[i].IsEmpty)
			{
				int toAdd = Mathf.Min(item.maxStackSize, quantity);

				Slots[i].item = item;
				Slots[i].quantity = toAdd;
				quantity -= toAdd;

				if (quantity <= 0)
				{
					OnInventoryChanged?.Invoke();
					return true;
				}
			}
		}

		OnInventoryChanged?.Invoke();
		return quantity <= 0; // false if leftover couldn't fit
	}

	// ─────────────────────────────────────────
	// Remove from a specific slot
	// ─────────────────────────────────────────
	public void RemoveFromSlot(int slotIndex, int quantity = 1)
	{
		if (slotIndex < 0 || slotIndex >= Slots.Length) return;
		if (Slots[slotIndex].IsEmpty) return;

		Slots[slotIndex].quantity -= quantity;

		if (Slots[slotIndex].quantity <= 0)
			Slots[slotIndex].Clear();

		OnInventoryChanged?.Invoke();
	}

	// ─────────────────────────────────────────
	// Use item from slot — for Food/Drink, calls LifeProcess
	// ─────────────────────────────────────────
	public void UseItem(int slotIndex, LifeProcess lifeProcess)
	{
		if (slotIndex < 0 || slotIndex >= Slots.Length) return;
		if (Slots[slotIndex].IsEmpty) return;

		ItemData item = Slots[slotIndex].item;

		switch (item.itemType)
		{
			case ItemType.Food:
				lifeProcess.Eat(item.hungerRestore, item.bowelIncrease);
				RemoveFromSlot(slotIndex, 1);
				break;

			case ItemType.Drink:
				lifeProcess.Drink(item.thirstRestore, item.bladderIncrease);
				RemoveFromSlot(slotIndex, 1);
				break;

			case ItemType.General:
				// TODO: handle general item use later
				break;
		}
	}

	public InventorySlot GetSlot(int index) => Slots[index];
}