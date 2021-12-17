using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootStep : MonoBehaviour
{
	[SerializeField] private AudioClip[] audioClip;
	private AudioSource audioSource;

	private void Start()
	{
		audioSource = GetComponent<AudioSource>();
	}

	private void StepEvent()
	{
		AudioClip clip = GetRandomClip();
		audioSource.PlayOneShot(clip);
	}

	private AudioClip GetRandomClip()
	{
		int index = Random.Range(0, audioClip.Length - 1);
		return audioClip[index];
	}
}
