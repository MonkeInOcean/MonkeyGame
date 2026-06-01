using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPanel : MonoBehaviour
{
	[SerializeField] private AuthService auth;

	[SerializeField] private TextMeshProUGUI emailInput;
	[SerializeField] private TextMeshProUGUI passwordInput;
	[SerializeField] private Button loginButton;

	[SerializeField] private StartPageScript startPage;

	private void Start()
	{
		loginButton.onClick.AddListener(OnLoginClicked);
	}

	private void OnLoginClicked()
	{
		print("Login button clicked with email: " + emailInput.text);
		print("Login button clicked with password: " + passwordInput.text);
		auth.Login(
			emailInput.text.Trim(),
			passwordInput.text.Trim(),
			profile =>
			{
				Debug.Log("Welcome " + profile.username);

				startPage.ShowRooms();
			},
			error =>
			{
				Debug.LogError(error);
			}
		);
	}
}