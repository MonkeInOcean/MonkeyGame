using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static DTOs;

public class RoomManager : MonoBehaviour
{
	[Header("Navigation")]
	[SerializeField] private StartPageScript startPage;

	[Header("Endpoints")]
	[SerializeField] private string hostedEndpoint = "/api/rooms/hosted";
	[SerializeField] private string joinedEndpoint = "/api/rooms/joined";

	[Header("Prefab")]
	[SerializeField] private GameObject roomPrefab;

	[Header("Scroll View Content Parents")]
	[SerializeField] private Transform hostedContent;
	[SerializeField] private Transform joinedContent;

	[Header("Section Headers")]
	[SerializeField] private GameObject hostedHeader;
	[SerializeField] private GameObject joinedHeader;

	private const string TargetScene = "GameScene";
	private const string SavedRoomKey = "selectedRoomId";

	// ─────────────────────────────────────────
	// Lifecycle
	// ─────────────────────────────────────────
	private void Start()
	{
		Debug.Log("[RoomManager] Start called");
		Debug.Log($"[RoomManager] IsLoggedIn: {APIManager.Instance.IsLoggedIn()}");
		Debug.Log($"[RoomManager] Token: {APIManager.Instance.Token}");

		if (!APIManager.Instance.IsLoggedIn())
		{
			Debug.LogWarning("[RoomManager] Not logged in — redirecting to login panel");
			startPage.ShowLogin();
			return;
		}

		Debug.Log("[RoomManager] Fetching hosted and joined rooms...");
		FetchRooms(hostedEndpoint, hostedContent, hostedHeader);
		FetchRooms(joinedEndpoint, joinedContent, joinedHeader);
	}

	private void FetchRooms(string endpoint, Transform contentParent, GameObject sectionHeader)
	{
		Debug.Log($"[RoomManager] FetchRooms called — endpoint: {endpoint}");
		Debug.Log($"[RoomManager] Content parent assigned: {contentParent != null}");
		Debug.Log($"[RoomManager] Room prefab assigned: {roomPrefab != null}");

		StartCoroutine(
			APIManager.Instance.Get(
				endpoint,
				response =>
				{
					Debug.Log($"[RoomManager] Response received from {endpoint}: {response}");

					List<RoomData> rooms = ParseRoomList(response);
					Debug.Log($"[RoomManager] Parsed {rooms.Count} rooms from {endpoint}");

					if (sectionHeader != null)
						sectionHeader.SetActive(rooms.Count > 0);

					if (rooms.Count == 0)
					{
						Debug.LogWarning($"[RoomManager] No rooms returned from {endpoint}");
						return;
					}

					foreach (RoomData room in rooms)
					{
						Debug.Log($"[RoomManager] Spawning room — id: {room.id} | name: '{room.name}' | code: {room.code}");
						SpawnRoomEntry(room, contentParent);
					}
				},
				error =>
				{
					Debug.LogError($"[RoomManager] GET failed for {endpoint}: {error}");
				}
			)
		);
	}

	private void SpawnRoomEntry(RoomData room, Transform parent)
	{
		GameObject entry = Instantiate(roomPrefab, parent);

		string displayName = string.IsNullOrWhiteSpace(room.name) ? room.code : room.name;
		entry.GetComponentInChildren<TextMeshProUGUI>().text = 
			$"{displayName}  ({room.playerCount}/{room.maxPlayers})";

		entry.GetComponentInChildren<Button>().onClick.AddListener(() => OnJoinClicked(room.id));
	}

	// ─────────────────────────────────────────
	// Join
	// ─────────────────────────────────────────
	private void OnJoinClicked(string roomId)
	{
		PlayerPrefs.SetString(SavedRoomKey, roomId);
		PlayerPrefs.Save();

		SceneManager.LoadScene(TargetScene);
	}

	// ─────────────────────────────────────────
	// JSON parsing
	// ─────────────────────────────────────────
	private List<RoomData> ParseRoomList(string json)
	{
		string wrapped = $"{{\"rooms\":{json}}}";
		RoomListResponse parsed = JsonUtility.FromJson<RoomListResponse>(wrapped);
		return parsed?.rooms != null
			? new List<RoomData>(parsed.rooms)
			: new List<RoomData>();
	}
}
