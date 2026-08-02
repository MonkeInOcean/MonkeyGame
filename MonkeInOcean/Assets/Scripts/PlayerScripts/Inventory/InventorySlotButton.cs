using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Per-slot pointer handler: left single click selects, left double click uses,
// right click drops. Wired up by InventoryUI when building the slot grid.
public class InventorySlotButton : MonoBehaviour, IPointerClickHandler
{
	public int index;
	public Action<int> onSelect;
	public Action<int> onUse;
	public Action<int> onDrop;

	public void OnPointerClick(PointerEventData e)
	{
		if (e.button == PointerEventData.InputButton.Right)
		{
			onDrop?.Invoke(index);
			return;
		}

		if (e.button == PointerEventData.InputButton.Left)
		{
			if (e.clickCount >= 2) onUse?.Invoke(index);
			else onSelect?.Invoke(index);
		}
	}
}
