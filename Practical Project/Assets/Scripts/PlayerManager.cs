using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
	public float collectable;
	public float playerHealth = 100;
	public Material potionLiquid;

	private void Start()
	{
		potionLiquid.SetInt("Fill Amount", 1);
	}
}
