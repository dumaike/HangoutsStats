using ChartAndGraph;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.UI;

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
		QUESTIONS,
		WORD_FREQUENCY_ABSOLUTE,
        WORD_FREQUENCY_RELATIVE
	}

	public MODE runMode = MODE.GRAPH;

	public GRAPH_TYPE graphType = GRAPH_TYPE.MESSAGES;

	public TextAsset hangoutsText;

	public long horizontalPeriodDays = 7;

	//The user we're parsing text history with
	public string userNameToExport;

	public GraphChartBase graph;

	public Text titleText;

	public Text user1WordFrequency;

	public Text user2WordFrequency;

	private DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private long horizontalPeriodTicks;

	public void Start()
	{
		if (runMode == MODE.GRAPH)
		{
			if (graphType == GRAPH_TYPE.WORD_FREQUENCY_ABSOLUTE)
			{
				CreateAbsoluteWordFreuqencyList();
			}
			else if (graphType == GRAPH_TYPE.WORD_FREQUENCY_RELATIVE)
			{
				CreateRelativeWordFrequencyList();
			}
			else
			{
				GraphDataPackage graphData = GetGraphForConversation();
				DrawGraph(graphData);
			}
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
		horizontalPeriodTicks = (long)1000000 * 60 * 60 * 24 * horizontalPeriodDays;

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
			if (message.timestamp - startOfTimePeriod > horizontalPeriodTicks)
			{
				graphData.entries.Add(graphEntry);
				graphEntry = new GraphDataEntry();				
				
				graphEntry.timestamp = message.timestamp / 1000000;

				while (startOfTimePeriod + horizontalPeriodTicks < message.timestamp)
				{
					startOfTimePeriod += horizontalPeriodTicks;
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

	private void CreateRelativeWordFrequencyList()
	{
		ConversationsEntry convo = JsonConvert.DeserializeObject<ConversationsEntry>(hangoutsText.text);

		string user1Id = convo.conversation.conversation.participant_data[0].id.chat_id;
		string user2Id = convo.conversation.conversation.participant_data[1].id.chat_id;

		Dictionary<string, int> user1WordCount = CreateWordFrequencyDictionary(convo, user1Id);
		List<KeyValuePair<string, int>> sortedWords1 = SortWordFrequencyList(user1WordCount);
		Dictionary<string, int> user2WordCount = CreateWordFrequencyDictionary(convo, user2Id);
		List<KeyValuePair<string, int>> sortedWords2 = SortWordFrequencyList(user2WordCount);

		List<WordFrequencyPackage> user1UsesMore = new List<WordFrequencyPackage>();
		List<WordFrequencyPackage> user2UsesMore = new List<WordFrequencyPackage>();
		HashSet<string> completeWords = new HashSet<string>();

		int topCutoffInCompare = 2000;
		CalculateRelativeFrequency(sortedWords1, sortedWords2, user2WordCount, user1UsesMore, user2UsesMore, completeWords, topCutoffInCompare);
		CalculateRelativeFrequency(sortedWords2, sortedWords1, user1WordCount, user2UsesMore, user1UsesMore, completeWords, topCutoffInCompare);

		user1UsesMore.Sort((x, y) => {
			return Math.Abs(y.relativeFrequency).CompareTo(Math.Abs(x.relativeFrequency));
		});
		user2UsesMore.Sort((x, y) => {
			return Math.Abs(y.relativeFrequency).CompareTo(Math.Abs(x.relativeFrequency));
		});

		string user1Name = convo.conversation.conversation.participant_data[0].fallback_name;
		string user2Name = convo.conversation.conversation.participant_data[1].fallback_name;

		string textOutput = user1Name + NumSpaces(15) + user2Name + "\n";
		int spaceBuffer = user1Name.Length + 15;

		int maxWords = Math.Max(user1UsesMore.Count, user2UsesMore.Count);
		for (int i = 0; i < maxWords; i++)
		{
			string textLine = "";
			if (user1UsesMore.Count > i)
			{
				textLine += "\"" + FirstCharToUpper(user1UsesMore[i].word) + "\" ";
				string user2Frequency =
					(user1UsesMore[i].user2Frequency == int.MaxValue) ? "INF" : user1UsesMore[i].user2Frequency.ToString("N0");

				textLine += user1UsesMore[i].user1Frequency.ToString("N0") + "->" + user2Frequency;
            }

			textLine += NumSpaces(spaceBuffer - textLine.Length);

			if (user2UsesMore.Count > i)
			{
				textLine += "\"" + FirstCharToUpper(user2UsesMore[i].word) + "\" ";
				int smallFrequency = Math.Min(user2UsesMore[i].user2Frequency, user2UsesMore[i].user1Frequency);
				int bigFrequency = Math.Max(user2UsesMore[i].user2Frequency, user2UsesMore[i].user1Frequency);

				string smallFreqText = smallFrequency.ToString("N0");
				string bigFreqText = bigFrequency == int.MaxValue ? "INF" : bigFrequency.ToString("N0");

				textLine += smallFreqText + "->" + bigFreqText;
			}

			textOutput += textLine + "\n";
		}

		string myName = user2Name.Contains("Dwyer") ? user1Name : user2Name;
		string theirUserName = myName.Replace(" ", "");
		System.IO.File.WriteAllText(Application.dataPath + "/HangoutsHistory/" + theirUserName + "RelativeWords.txt", textOutput);
	}

	private void CalculateRelativeFrequency(
		List<KeyValuePair<string, int>> sortedWords1, 
		List<KeyValuePair<string, int>> sortedWords2,
		Dictionary<string, int> user2WordCount,
		List<WordFrequencyPackage> user1UsesMore,
		List<WordFrequencyPackage> user2UsesMore,
		HashSet<string> completeWords,
		int topCutoffInCompare)
    {
		int topToCount = 200;
		for (int i = 0; i < topToCount; ++i)
		{
			WordFrequencyPackage result = new WordFrequencyPackage();
			KeyValuePair<string, int> user1Entry = sortedWords1[i];

			//Don't check words we've already compared
			if (completeWords.Contains(user1Entry.Key))
			{
				continue;
			}

			int positionForUser2 = int.MaxValue - 1;
			if (user2WordCount.ContainsKey(user1Entry.Key))
			{
				for (int j = 0; j < sortedWords2.Count; ++j)
				{
					if (sortedWords2[j].Key == user1Entry.Key)
					{
						positionForUser2 = j;
						break;
					}
				}
			}
			else
			{
				positionForUser2 = topCutoffInCompare;
			}
			
			result.word = user1Entry.Key;
			result.relativeFrequency = i - positionForUser2;
			result.user1Frequency = i + 1;
			result.user2Frequency = positionForUser2 + 1;

			completeWords.Add(user1Entry.Key);
			if (result.relativeFrequency < 0)
			{
				user1UsesMore.Add(result);
			}
			else if (result.relativeFrequency > 0)
			{
				user2UsesMore.Add(result);
			}
		}
	}

	private void CreateAbsoluteWordFreuqencyList()
	{
		ConversationsEntry entry = JsonConvert.DeserializeObject<ConversationsEntry>(hangoutsText.text);

		string user1Id = entry.conversation.conversation.participant_data[0].id.chat_id;
		string user2Id = entry.conversation.conversation.participant_data[1].id.chat_id;

		string user1Name = entry.conversation.conversation.participant_data[0].fallback_name;
		string user2Name = entry.conversation.conversation.participant_data[1].fallback_name;

		Dictionary<string, int> user1WordCount = CreateWordFrequencyDictionary(entry, user1Id);
		Dictionary<string, int> user2WordCount = CreateWordFrequencyDictionary(entry, user2Id);

		string wordList = user1Name + NumSpaces(10) + user2Name + "\n";
		int spaceBuffer = user1Name.Length + 10;

		List<KeyValuePair<string, int>> sortedWords1 = SortWordFrequencyList(user1WordCount);
		List<KeyValuePair<string, int>> sortedWords2 = SortWordFrequencyList(user2WordCount);

		int maxVocab = Math.Max(sortedWords1.Count, sortedWords2.Count);
		for (int i = 0; i < maxVocab; i++)
		{
			//Don't count words that have appeared less than 2 times
			if (sortedWords1[i].Value < 2 && sortedWords2[i].Value < 2)
			{
				break;
			}

			string u1Word = sortedWords1[i].Value < 2 ? "----" : FirstCharToUpper(sortedWords1[i].Key) + " (" + sortedWords1[i].Value.ToString("N0") + ")";
			string u2Word = sortedWords2[i].Value < 2 ? "----" : FirstCharToUpper(sortedWords2[i].Key) + " (" + sortedWords2[i].Value.ToString("N0") + ")";

			int spaceBufferThisLine = spaceBuffer - u1Word.Length - NumDigits(i + 1) + 2;

			wordList += (i + 1) + ". " + u1Word + NumSpaces(spaceBufferThisLine) + u2Word + "\n";
		}

		string myName = user2Name.Contains("Dwyer") ? user1Name : user2Name;
		string theirUserName = myName.Replace(" ", "");
		System.IO.File.WriteAllText(Application.dataPath + "/HangoutsHistory/" + theirUserName + "Words.txt", wordList);
	}

	private Dictionary<string, int> CreateWordFrequencyDictionary(ConversationsEntry convo, string userId)
	{
		Dictionary<string, int> user1WordCount = new Dictionary<string, int>();

		foreach (ConversationEvent message in convo.events)
		{
			//If the message was from them
			if (message.sender_id.chat_id == userId)
			{
				IncrementWordCountInDictionary(message, user1WordCount);
			}
		}

		return user1WordCount;
	}

	private List<KeyValuePair<string, int>> SortWordFrequencyList(Dictionary<string, int> dict)
	{
		dict.Remove("");

		List<KeyValuePair<string, int>> sortedList = dict.ToList();

		sortedList.Sort(
			delegate (KeyValuePair<string, int> pair1,
			KeyValuePair<string, int> pair2)
			{
				return Math.Abs(pair2.Value).CompareTo(Math.Abs(pair1.Value));
			}
		);

		return sortedList;
	}

	private string FirstCharToUpper(string input)
	{
		if (input == null || input.Length == 0)
		{
			return input;
		}

		return input.First().ToString().ToUpper() + input.Substring(1);
	}

	private string NumSpaces(int num)
	{
		string returnString = "";

		for (int i = 0; i < num; i++)
		{
			returnString += " ";
		}

		return returnString;
	}

	private void IncrementWordCountInDictionary(ConversationEvent messageEvent, Dictionary<string, int> dict)
	{
		//For non chat messages like call notifications
		if (messageEvent.chat_message == null)
		{
			return;
		}

		if (messageEvent.chat_message.message_content == null || 
			messageEvent.chat_message.message_content.segment == null)
		{
			return;
		}

		foreach (MessageSegment messageText in messageEvent.chat_message.message_content.segment)
		{
			if (messageText == null || messageText.text == null)
			{
				continue;
			}

			string[] messageSplit = messageText.text.Split(' ');
			foreach (string word in messageSplit)
			{
				string lcWord = word.ToLower();
				lcWord = lcWord.Replace("\"", "");
				lcWord = lcWord.Replace("?", "");
				lcWord = lcWord.Replace(".", "");
				lcWord = lcWord.Replace("!", "");
				lcWord = lcWord.Replace("-", "");
				lcWord = lcWord.Replace("`", "");
				lcWord = lcWord.Replace("(", "");
				lcWord = lcWord.Replace(")", "");
				lcWord = lcWord.Replace(",", "");
				lcWord = lcWord.Replace(":", "");
				lcWord = lcWord.Replace("\n", "");
				lcWord = lcWord.Trim();

				if (!dict.ContainsKey(lcWord))
				{
					dict.Add(lcWord, 0);
				}
				dict[lcWord]++;
			}
		}
	}

	private void DrawGraph(GraphDataPackage graphData)
	{
		if (graph == null)
		{
			Debug.LogError("There is no graph");
			return;
		}

		graph.gameObject.SetActive(true);

		string typeAsText = graphType.ToString().ToLower();
		typeAsText = typeAsText.Substring(0, 1).ToUpper() + typeAsText.Substring(1);
		titleText.text = "Number of " + typeAsText + " Sent";

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

	private int NumDigits(int input)
	{
		return (int)Math.Floor(Math.Log10(input) + 1);
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