using UnityEngine;

public enum ItemType
{
	Food,
	Drink,
	General
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
	[Header("Display")]
	public string itemName;
	public Sprite icon;
	[TextArea] public string description;

	[Header("Stacking")]
	public int maxStackSize = 10;

	[Header("Type")]
	public ItemType itemType;

	[Header("Consumable Values")]
	public float hungerRestore;
	public float thirstRestore;
	public float bowelIncrease = 10f;
	public float bladderIncrease = 10f;

	[Header("World Prefab")]
	public GameObject worldPrefab;
}