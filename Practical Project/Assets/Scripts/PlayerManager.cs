using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
	public float collectable;
	public float playerMaxHealth = 100;
	public float playerCurrentHealth;
	public Material potionLiquid;
	public HealthBarScript healthBar;

	private void Start()
	{
		playerCurrentHealth = playerMaxHealth;
		healthBar.SetMaxHealth(playerMaxHealth);
		potionLiquid.SetInt("Fill Amount", 1);
	}
}
