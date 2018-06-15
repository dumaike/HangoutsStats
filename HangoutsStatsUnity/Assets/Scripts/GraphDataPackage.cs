using System.Collections.Generic;

public struct GraphDataEntry
{
	public int val1;
	public int val2;
	public long timestamp;
}

public class GraphDataPackage
{
	public string user1Name;
	public string user2Name;
	public List<GraphDataEntry> entries = new List<GraphDataEntry>();
}
