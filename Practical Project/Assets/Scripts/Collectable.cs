using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Collectable : MonoBehaviour
{
	public GameObject player;
	public PlayerManager playerManager;

	private void Start()
	{
		player = GameObject.FindGameObjectWithTag("Player");
		playerManager = player.GetComponent<PlayerManager>();
	}
	private void OnTriggerEnter(Collider other)
	{
		if (player)
		{
			playerManager.collectable++;
			Destroy(gameObject);
		}
	}
}
