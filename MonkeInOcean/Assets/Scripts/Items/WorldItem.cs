using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour
{
	public ItemData item;
	public int quantity = 1;

	private void Awake()
	{
		// ensure collider is not a trigger so raycast hits it
		Collider col = GetComponent<Collider>();
		col.isTrigger = false;

		// every collectable gets the black outline highlight
		if (!TryGetComponent(out ItemOutline outline))
			outline = gameObject.AddComponent<ItemOutline>();

		outline.ApplyOutline();
	}
}