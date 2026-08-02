using UnityEngine;

public enum AnimalHabitat
{
	Land,
	Water
}

[CreateAssetMenu(fileName = "NewAnimal", menuName = "Animals/Animal Profile")]
public class AnimalProfile : ScriptableObject
{
	[Header("Display")]
	public string animalName;

	[Header("Spawning")]
	public GameObject prefab;
	public AnimalHabitat habitat;
	public Vector2 scaleRange = Vector2.one;

	[Header("Temperament")]
	public bool isPredator;

	[Header("Movement")]
	public float walkSpeed = 1.5f;
	public float fleeSpeed = 5f;
	public float turnSpeed = 6f;
	public float acceleration = 8f;

	[Header("Senses")]
	public float detectRadius = 12f;
	public float giveUpRadius = 25f;
	public float roamRadius = 20f;
	public float fleeDistance = 18f;

	[Header("Idling")]
	public Vector2 idleDurationRange = new Vector2(2f, 6f);

	[Header("Attack")]
	public float attackRange = 2f;
	public float attackDamage = 8f;
	public float attackCooldown = 1.5f;

	[Header("Animator States")]
	public string idleState = "Idle_A";
	public string moveState = "Walk";
	public string fastState = "Run";
	public string alertState = "Fear";
	public string attackState = "Attack";
}
