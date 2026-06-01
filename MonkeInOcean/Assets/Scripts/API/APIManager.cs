using System.Collections;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance { get; private set; }

    [Header("API Settings")]
    [SerializeField] private string baseUrl = "https://localhost:7061";

	public string Token { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);

		if (IsLoggedIn())
		{
			SetToken(PlayerPrefs.GetString("jwt"));
		}
	}

	public void SetToken(string token)
	{
		Token = token;
	}

	// =========================
	// POST
	// =========================
	public IEnumerator Post(
		string endpoint,
		string json,
		System.Action<string> onSuccess,
		System.Action<string> onError)
	{
		var request = new UnityWebRequest(baseUrl + endpoint, "POST");

		byte[] body = Encoding.UTF8.GetBytes(json);

		request.uploadHandler = new UploadHandlerRaw(body);
		request.downloadHandler = new DownloadHandlerBuffer();

		request.SetRequestHeader("Content-Type", "application/json");

		if (!string.IsNullOrEmpty(Token))
		{
			request.SetRequestHeader("Authorization", "Bearer " + Token);
		}

		yield return request.SendWebRequest();

		if (request.result == UnityWebRequest.Result.Success)
		{
			onSuccess?.Invoke(request.downloadHandler.text);
		}
		else
		{
			Debug.LogError("REQUEST FAILED");
			Debug.LogError("URL: " + baseUrl + endpoint);
			Debug.LogError("Response Code: " + request.responseCode);
			Debug.LogError("Error: " + request.error);
			Debug.LogError("Body: " + request.downloadHandler.text);

			onError?.Invoke(request.downloadHandler.text);
		}
	}

	// =========================
	// GET
	// =========================
	public IEnumerator Get(
		string endpoint,
		System.Action<string> onSuccess,
		System.Action<string> onError)
	{
		var request = UnityWebRequest.Get(baseUrl + endpoint);

		if (!string.IsNullOrEmpty(Token))
		{
			request.SetRequestHeader("Authorization", "Bearer " + Token);
		}

		yield return request.SendWebRequest();

		if (request.result == UnityWebRequest.Result.Success)
		{
			onSuccess?.Invoke(request.downloadHandler.text);
		}
		else
		{
			Debug.LogError("REQUEST FAILED");
			Debug.LogError("URL: " + baseUrl + endpoint);
			Debug.LogError("Response Code: " + request.responseCode);
			Debug.LogError("Error: " + request.error);
			Debug.LogError("Body: " + request.downloadHandler.text);

			onError?.Invoke(request.downloadHandler.text);
		}
	}

	public bool IsLoggedIn()
	{
		return PlayerPrefs.HasKey("jwt");
	}
}
