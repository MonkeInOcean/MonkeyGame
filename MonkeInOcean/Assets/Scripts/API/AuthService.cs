using System;
using System.Threading.Tasks;
using UnityEngine;

using static DTOs;

public class AuthService : MonoBehaviour
{
	[SerializeField] private string loginEndpoint = "/api/auth/login";
	[SerializeField] private string hostedEndpoint = "/api/rooms/hosted";
	[SerializeField] private string joinedEndpoint = "/api/rooms/joined";

	// ─────────────────────────────────────────
	// Token helpers
	// ─────────────────────────────────────────
	public bool IsLoggedIn()
	{
		return PlayerPrefs.HasKey("jwt");
	}

	public string GetToken()
	{
		return PlayerPrefs.GetString("jwt");
	}

	public void SaveToken(string token)
	{
		PlayerPrefs.SetString("jwt", token);
		PlayerPrefs.Save();

		APIManager.Instance.SetToken(token);
	}

	public void Logout()
	{
		PlayerPrefs.DeleteKey("jwt");
		PlayerPrefs.Save();

		APIManager.Instance.SetToken(null);
	}

	// ─────────────────────────────────────────
	// Login
	// ─────────────────────────────────────────
	public void Login(
		string email,
		string password,
		Action<PlayerProfile> onSuccess,
		Action<string> onError)
	{
		var req = new LoginRequest
		{
			emailOrUsername = email,
			password = password
		};

		string json = JsonUtility.ToJson(req);

		APIManager.Instance.StartCoroutine(
			APIManager.Instance.Post(
				loginEndpoint,
				json,
				response =>
				{
					var data = JsonUtility.FromJson<LoginResponse>(response);

					if (data == null || string.IsNullOrEmpty(data.token))
					{
						onError?.Invoke("Invalid login response");
						return;
					}

					SaveToken(data.token);
					onSuccess?.Invoke(data.profile);
				},
				error => onError?.Invoke(error)
			)
		);
	}

	// ─────────────────────────────────────────
	// Rooms
	// ─────────────────────────────────────────
	public void FetchHostedRooms(Action<RoomData[]> onSuccess, Action<string> onError)
	{
		FetchRoomList(hostedEndpoint, onSuccess, onError);
	}

	public void FetchJoinedRooms(Action<RoomData[]> onSuccess, Action<string> onError)
	{
		FetchRoomList(joinedEndpoint, onSuccess, onError);
	}

	private void FetchRoomList(string endpoint, Action<RoomData[]> onSuccess, Action<string> onError)
	{
		Debug.Log($"[AuthService] FetchRoomList called for: {endpoint}");
		Debug.Log($"[AuthService] Token present: {!string.IsNullOrEmpty(APIManager.Instance.Token)}");

		APIManager.Instance.StartCoroutine(
			APIManager.Instance.Get(
				endpoint,
				response =>
				{
					Debug.Log($"[AuthService] Raw response from {endpoint}: {response}");

					if (string.IsNullOrWhiteSpace(response))
					{
						Debug.LogError($"[AuthService] Empty response from {endpoint}");
						onError?.Invoke("Empty response");
						return;
					}

					string wrapped = $"{{\"rooms\":{response}}}";
					Debug.Log($"[AuthService] Wrapped JSON: {wrapped}");

					RoomListResponse parsed = JsonUtility.FromJson<RoomListResponse>(wrapped);

					if (parsed == null)
					{
						Debug.LogError("[AuthService] JsonUtility returned null — check RoomListResponse struct");
						onError?.Invoke("Parse returned null");
						return;
					}

					if (parsed.rooms == null)
					{
						Debug.LogError("[AuthService] parsed.rooms is null — JSON shape may not match RoomListResponse");
						onError?.Invoke("Rooms array is null");
						return;
					}

					Debug.Log($"[AuthService] Successfully parsed {parsed.rooms.Length} rooms from {endpoint}");
					onSuccess?.Invoke(parsed.rooms);
				},
				error =>
				{
					Debug.LogError($"[AuthService] GET failed for {endpoint}: {error}");
					onError?.Invoke(error);
				}
			)
		);
	}
}