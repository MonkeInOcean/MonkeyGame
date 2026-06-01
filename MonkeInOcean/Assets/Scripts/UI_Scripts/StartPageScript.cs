using UnityEngine;
using UnityEngine.UI;

public class StartPageScript : MonoBehaviour
{
	[SerializeField] private AuthService auth;

	[Header("UI")]
	[SerializeField] private GameObject startPagePanel;
	[SerializeField] private GameObject loginPanel;
	[SerializeField] private GameObject roomsPanel;

	[Header("Buttons")]
	[SerializeField] private Button startButton;

	private void Start()
	{
		startButton.onClick.AddListener(OnStartClicked);

		//ShowStart();
	}

	private void OnStartClicked()
	{
		print("Start button clicked");
		if (auth.IsLoggedIn())
		{
			print("User is logged in, showing rooms");
			ShowRooms();
		}
		else
		{
			print("User is logged in, showing rooms");
			ShowLogin();
		}
	}

	public void ShowLogin()
	{
		loginPanel.SetActive(true);
		startPagePanel.SetActive(false);
		roomsPanel.SetActive(false);
	}

	public void ShowRooms()
	{
		roomsPanel.SetActive(true);
		startPagePanel.SetActive(false);
		loginPanel.SetActive(false);
	}

	public void ShowStart()
	{
		startPagePanel.SetActive(true);
		loginPanel.SetActive(false);
		roomsPanel.SetActive(false);
	}
}