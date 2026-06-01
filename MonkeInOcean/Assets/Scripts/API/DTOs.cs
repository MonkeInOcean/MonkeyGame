using System;
using UnityEngine;

public class DTOs
{
	[Serializable]
	public class LoginRequest
	{
		public string emailOrUsername;
		public string password;
	}

	[Serializable]
	public class PlayerProfile
	{
		public string id;
		public string username;
		public float hunger;
		public float energy;
		public float x;
		public float y;
		public float z;
	}

	[Serializable]
	public class LoginResponse
	{
		public string token;
		public PlayerProfile profile;
	}
}
