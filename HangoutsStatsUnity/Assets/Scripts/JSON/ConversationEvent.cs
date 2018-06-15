using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConversationEvent
{
	public ParticipantDataId sender_id;
	public ChatMessage chat_message;
	public long timestamp;
	public string event_type;
}
