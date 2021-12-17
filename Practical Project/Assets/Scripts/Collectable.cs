using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(AudioSource))]
public class Collectable : MonoBehaviour
{
	public GameObject player;
	public PlayerManager playerManager;
	public AudioClip gemSound;
	public AudioSource audioSource;

	private void Start()
	{
		player = GameObject.FindGameObjectWithTag("Player");
		playerManager = player.GetComponent<PlayerManager>();
		audioSource = GetComponent<AudioSource>();
	}
	private void OnTriggerEnter(Collider other)
	{
		if (player)
		{
			audioSource.PlayOneShot(gemSound);
			Debug.Log("COLLECTABLE");
			playerManager.collectable++;
			Destroy(gameObject);
		}
	}
}
