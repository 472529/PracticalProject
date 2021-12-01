using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingPlatform : MonoBehaviour
{
	private GameObject player;
	private Rigidbody rb;

	private void Start()
	{
		rb = GetComponent<Rigidbody>();
		player = GameObject.FindGameObjectWithTag("Player");
	}

	private void OnCollisionEnter(Collision collision)
	{
		rb.constraints = RigidbodyConstraints.None;
	}
}
