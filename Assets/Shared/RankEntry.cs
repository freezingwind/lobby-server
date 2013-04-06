using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Jboy;

[System.Serializable]
public class RankEntry {
	public int rankIndex;
	public string accountId;
	public string name;
	public int bestRanking;
	
	public static void WriteRankEntry(uLink.BitStream stream, object val, params object[] args) {
		//Debug.Log("WriteRankEntry");
		RankEntry myObj = (RankEntry)val;
		stream.WriteInt32(myObj.rankIndex);
		stream.WriteString(myObj.accountId);
		stream.WriteString(myObj.name);
		stream.WriteInt32(myObj.bestRanking);
		//Debug.Log("WriteRankEntry: " + myObj.accountId + ", " + myObj.bestRanking);
	}
	
	public static object ReadRankEntry(uLink.BitStream stream, params object[] args) {
		//Debug.Log("ReadRankEntry");
		RankEntry myObj = new RankEntry();
		myObj.rankIndex = stream.ReadInt32();
		myObj.accountId = stream.ReadString();
		myObj.name = stream.ReadString();
		myObj.bestRanking = stream.ReadInt32();
		//Debug.Log("ReadRankEntry: " + myObj.accountId + ", " + myObj.bestRanking);
		return myObj;
	}
	
	// Writer
	public static void JsonSerializer(JsonWriter writer, object instance) {
		var scoreEntry = (RankEntry)instance;
		
		writer.WriteArrayStart();
		writer.WriteNumber(scoreEntry.rankIndex);
		writer.WriteString(scoreEntry.accountId);
		writer.WriteString(scoreEntry.name);
		writer.WriteNumber(scoreEntry.bestRanking);
		writer.WriteArrayEnd();
	}
	
	// Reader
	public static object JsonDeserializer(JsonReader reader) {
		var scoreEntry = new RankEntry();
		
		reader.ReadArrayStart();
		scoreEntry.rankIndex = (int)reader.ReadNumber();
		scoreEntry.accountId = reader.ReadString();
		scoreEntry.name = reader.ReadString();
		scoreEntry.bestRanking = (int)reader.ReadNumber();
		reader.ReadArrayEnd();
		
		return scoreEntry;
	}
}
