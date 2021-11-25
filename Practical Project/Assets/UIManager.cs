using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UIManager : MonoBehaviour
{
	public PlayerManager playerManager;
	[SerializeField] private TMP_Text collectableScore;

	private void Update()
	{
		collectableScore.text = "Collectable: " + playerManager.collectable;
	}
}
