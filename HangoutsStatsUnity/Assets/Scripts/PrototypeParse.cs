using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrototypeParse : MonoBehaviour
{
	public TextAsset hangoutsText;

	private string selfId;

	public void Start()
	{

		Debug.Log(hangoutsText.text);

		Conversations allConvos = JsonConvert.DeserializeObject<Conversations>(hangoutsText.text);



		foreach (ConversationsEntry entry in allConvos.conversations)
		{
			foreach (ParticipantData user in entry.conversation.conversation.participant_data)
			{
				Debug.Log(user.fallback_name);
			}
		}
	}
}