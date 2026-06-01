using System;
using System.Threading.Tasks;
using UnityEngine;

using static DTOs;

public class AuthService : MonoBehaviour
{
	[SerializeField] private string loginEndpoint = "/api/auth/login";

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

	// =========================
	// LOGIN
	// =========================
	public void Login(
		string email,
		string password,
		Action<PlayerProfile> onSuccess,
		Action<string> onError)
	{
		Debug.Log("EMAIL=[" + email + "]");
		Debug.Log("PASSWORD=[" + password + "]");

		var req = new LoginRequest
		{
			emailOrUsername = email,
			password = password
		};

		string json = JsonUtility.ToJson(req);

		Debug.Log("[" + json + "]");

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
				error =>
				{
					onError?.Invoke(error);
				}
			)
		);
	}
}