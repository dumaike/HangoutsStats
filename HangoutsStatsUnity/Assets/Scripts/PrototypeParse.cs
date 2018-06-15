using ChartAndGraph;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrototypeParse : MonoBehaviour
{
	public enum MODE
	{
		SIMPLIFY,
		GRAPH
	}

	public enum GRAPH_TYPE
	{
		MESSAGES,
		CHARACTERS,
		MEDIA,
		QUESTIONS
	}

	public MODE runMode = MODE.GRAPH;

	public GRAPH_TYPE graphType = GRAPH_TYPE.MESSAGES;

	public TextAsset hangoutsText;

	//The user we're parsing text history with
	public string userNameToExport;

	public GraphChartBase graph;
	
	private DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private long oneWeek = (long)1000000 * 60 * 60 * 24 * 7;	

	public void Start()
	{
		if (runMode == MODE.GRAPH)
		{
			GraphDataPackage graphData = GetGraphForConversation();
			DrawGraph(graphData);
		}
		else
		{
			WriteOutSimpleData();
		}
	}

	private void WriteOutSimpleData()
	{
		Conversations allConvos = JsonConvert.DeserializeObject<Conversations>(hangoutsText.text);
		ConversationsEntry matchingEntry = null;

		foreach (ConversationsEntry entry in allConvos.conversations)
		{
			if (matchingEntry != null)
			{
				break;
			}

			foreach (ParticipantData user in entry.conversation.conversation.participant_data)
			{
				if (user.fallback_name == userNameToExport && entry.conversation.conversation.participant_data.Count == 2)
				{
					matchingEntry = entry;
				}
			}
		}

		if (matchingEntry != null)
		{
			//Make sure entries are sorted by timestamp
			matchingEntry.events.Sort(
			(x, y) =>
			{
				if (x.timestamp == y.timestamp)
				{
					return 0;
				}

				if (x.timestamp > y.timestamp)
				{
					return 1;
				}

				return -1;
			});

			string serializedJson = JsonConvert.SerializeObject(matchingEntry, Formatting.Indented);
			string flatUserId = userNameToExport.Replace(" ", "");
			System.IO.File.WriteAllText(Application.dataPath + "/HangoutsHistory/" + flatUserId + ".json", serializedJson);
		}
	}

	private GraphDataPackage GetGraphForConversation()
	{
		ConversationsEntry entry = JsonConvert.DeserializeObject<ConversationsEntry>(hangoutsText.text);

		string user1 = entry.conversation.conversation.participant_data[0].id.chat_id;
		string user2 = entry.conversation.conversation.participant_data[1].id.chat_id;

		if (user2 == null || user1 == null)
		{
			Debug.LogError("No user named \"" + userNameToExport + "\" found");
			return null;
		}

		int user1Messages = 0;
		int user1Media = 0;
		int user1Questions = 0;
		long user1Chars = 0;

		int user2Messages = 0;
		int user2Media = 0;
		int user2Questions = 0;
		long user2Chars = 0;

		long startOfTimePeriod = entry.events[0].timestamp;
		DateTime firstMessage = TimestampToDateTime(startOfTimePeriod);

		GraphDataPackage graphData = new GraphDataPackage();
		GraphDataEntry graphEntry = new GraphDataEntry();
		graphEntry.timestamp = startOfTimePeriod / 1000000;
		int currentMonth = firstMessage.Month;

		graphData.user1Name = entry.conversation.conversation.participant_data[0].fallback_name;
		graphData.user2Name = entry.conversation.conversation.participant_data[1].fallback_name;

		foreach (ConversationEvent message in entry.events)
		{
			//For non chat messages like call notifications
			if (message.chat_message == null)
			{
				continue;
			}

			//If the month rolled over, reset it
			if (message.timestamp - startOfTimePeriod > oneWeek)
			{
				graphData.entries.Add(graphEntry);
				graphEntry = new GraphDataEntry();				
				
				graphEntry.timestamp = message.timestamp / 1000000;

				while (startOfTimePeriod + oneWeek < message.timestamp)
				{
					startOfTimePeriod += oneWeek;
				}
			}

			//If the message was from them
			if (message.sender_id.chat_id == user2)
			{
				if (message.chat_message.message_content == null || message.chat_message.message_content.segment == null)
				{
					user2Media++;
					if (graphType == GRAPH_TYPE.MEDIA)
					{
						graphEntry.val2++;
					}
					continue;
				}

				user2Messages += message.chat_message.message_content.segment.Count;
				if (graphType == GRAPH_TYPE.MESSAGES)
				{
					graphEntry.val2++;
				}

				foreach (MessageSegment messageText in message.chat_message.message_content.segment)
				{
					int charactersThisMessage = messageText.text == null ? 0 : messageText.text.Length;
					user2Chars += charactersThisMessage;

					if (graphType == GRAPH_TYPE.CHARACTERS)
					{
						graphEntry.val2 += charactersThisMessage;
					}

					if (IsQuestion(messageText.text))
					{
						user2Questions++;
						if (graphType == GRAPH_TYPE.QUESTIONS)
						{
							graphEntry.val2++;
						}
					}
				}
			}
			//If the message was from us
			else if (message.sender_id.chat_id == user1)
			{
				if (message.chat_message.message_content == null || message.chat_message.message_content.segment == null)
				{
					user1Media++;
					if (graphType == GRAPH_TYPE.MEDIA)
					{
						graphEntry.val1++;
					}
					continue;
				}

				user1Messages += message.chat_message.message_content.segment.Count;
				if (graphType == GRAPH_TYPE.MESSAGES)
				{
					graphEntry.val1++;
				}
				foreach (MessageSegment messageText in message.chat_message.message_content.segment)
				{
					int charactersThisMessage = messageText.text == null ? 0 : messageText.text.Length;
					user1Chars += charactersThisMessage;

					if (graphType == GRAPH_TYPE.CHARACTERS)
					{
						graphEntry.val1 += charactersThisMessage;
					}

					if (IsQuestion(messageText.text))
					{
						user1Questions++;
						if (graphType == GRAPH_TYPE.QUESTIONS)
						{
							graphEntry.val1++;
						}
					}
				}
			}
		}

		return graphData;
	}

	private void DrawGraph(GraphDataPackage graphData)
	{
		if (graph == null)
		{
			Debug.LogError("There is no graph");
			return;
		}

		graph.DataSource.RenameCategory("Player 1", graphData.user1Name);
		graph.DataSource.RenameCategory("Player 2", graphData.user2Name);

		graph.DataSource.StartBatch();
		graph.DataSource.ClearCategory(graphData.user1Name);
		graph.DataSource.ClearCategory(graphData.user2Name);
		for (int i = 0; i < graphData.entries.Count; i++)
		{
			graph.DataSource.AddPointToCategory(graphData.user1Name, graphData.entries[i].timestamp, graphData.entries[i].val1);
			graph.DataSource.AddPointToCategory(graphData.user2Name, graphData.entries[i].timestamp, graphData.entries[i].val2);
		}
		
		graph.DataSource.EndBatch();
	}

	private DateTime TimestampToDateTime(long timestamp)
	{
		return epoch.AddSeconds(timestamp / 1000000);
	}

	private bool IsQuestion(string input)
	{
		if (input == null)
		{
			return false;
		}

		if (input.Contains("?"))
		{
			return true;
		}

		string firstWord = input.Split(' ')[0].ToLower();
		if (firstWord == "who" || firstWord == "what" || firstWord == "where" ||
			firstWord == "why" || firstWord == "how" || firstWord == "when")
		{
			return true;
		}

		return false;
	}

}