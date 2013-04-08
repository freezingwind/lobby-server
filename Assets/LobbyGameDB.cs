using UnityEngine;
using uLobby;
using uGameDB;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class LobbyGameDB : MonoBehaviour {
	// Get character stats
	public IEnumerator GetCharacterStats(LobbyPlayer lobbyPlayer) {
		yield return StartCoroutine(GameDB.Get<CharacterStats>(
		"AccountToCharacterStats",
		lobbyPlayer.account.id.value,
		data => {
			if(data == null)
				lobbyPlayer.charStats = new CharacterStats();
			else
				lobbyPlayer.charStats = data;
		}));
		
		Lobby.RPC("ReceiveCharacterStats", lobbyPlayer.peer, lobbyPlayer.charStats);
	}
	
	// Set character stats
	public IEnumerator SetCharacterStats(LobbyPlayer lobbyPlayer, CharacterStats charStats) {
		yield return StartCoroutine(GameDB.Set<CharacterStats>(
		"AccountToCharacterStats",
		lobbyPlayer.account.id.value,
		charStats,
		data => {
			if(data == null)
				Lobby.RPC("CharacterStatsSaveError", lobbyPlayer.peer);
			else
				lobbyPlayer.charStats = data;
		}));
	}
	
	// Get the player name
	public IEnumerator GetPlayerName(LobbyPlayer lobbyPlayer) {
		yield return StartCoroutine(GameDB.Get<string>(
		"AccountToName",
		lobbyPlayer.account.id.value,
		data => {
			if(data == null) {
				Lobby.RPC("AskPlayerName", lobbyPlayer.peer);
			} else {
				lobbyPlayer.name = data;
				Lobby.RPC("ReceivePlayerInfo", lobbyPlayer.peer, lobbyPlayer.account.id.value, lobbyPlayer.name);
				LobbyServer.OnReceivePlayerName(lobbyPlayer);
			}
		}));
	}
	
	// Sets the player name
	public IEnumerator SetPlayerName(LobbyPlayer lobbyPlayer, string playerName) {
		yield return StartCoroutine(GameDB.Set<string>(
		"AccountToName",
		lobbyPlayer.account.id.value,
		playerName,
		data => {
			if(data == null) {
				Lobby.RPC("PlayerNameChangeError", lobbyPlayer.peer);
			} else {
				lobbyPlayer.name = data;
				Lobby.RPC("ReceivePlayerInfo", lobbyPlayer.peer, lobbyPlayer.account.id.value, lobbyPlayer.name);
				LobbyServer.OnReceivePlayerName(lobbyPlayer);
			}
		}));
	}
	
	// Get stats for a single player
	public IEnumerator GetPlayerStats(LobbyPlayer lobbyPlayer) {
		yield return StartCoroutine(GameDB.Get<PlayerStats>(
		"AccountToStats",
		lobbyPlayer.account.id.value,
		data => {
			if(data == null)
				data = new PlayerStats();
			
			// Assign stats
			lobbyPlayer.stats = data;
			
			// Send the stats to the player
			Lobby.RPC("ReceivePlayerStats", lobbyPlayer.peer,
				Jboy.Json.WriteObject(data)
			);
		}));
	}
	
	// Sets last login date
	public IEnumerator SetLastLoginDate(LobbyPlayer lobbyPlayer, System.DateTime timestamp) {
		yield return StartCoroutine(GameDB.Set<System.DateTime>(
		"AccountToLastLoginDate",
		lobbyPlayer.account.id.value,
		timestamp,
		data => {
			// ...
		}));
	}
	
	// Sets account registration date
	public IEnumerator SetAccountRegistrationDate(string accountId, System.DateTime timestamp) {
		yield return StartCoroutine(GameDB.Set<System.DateTime>(
		"AccountToRegistrationDate",
		accountId,
		timestamp,
		data => {
			// ...
		}));
	}
	
	// Gets account registration date
	public IEnumerator GetAccountRegistrationDate(LobbyPlayer lobbyPlayer) {
		/*yield return StartCoroutine(GameDB.Get<System.DateTime>(
		"AccountToRegistrationDate",
		lobbyPlayer.account.id.value,
		data => {
			if(data == null)
				Debug.LogWarning("Failed getting registration date of account ID '" + lobbyPlayer.account.id.value + "'");
			else
				Debug.Log("Got registration date of account ID '" + lobbyPlayer.account.id.value + "' successfully: " + data);
		}));*/
		yield break;
	}
	
	// Get top ranks
	public IEnumerator GetTopRanks(uint maxPlayerCount, uLobby.LobbyPeer peer) {
		// Retrieve the highscore list from the database by using MapReduce. The MapReduce request consists of a
		// map phase and a reduce phase. The phases are expressed as JavaScript code in string form. The reduce
		// phase also gets the maximum number of scores to fetch as an argument.
		var bucket = new Bucket("AccountToStats");
		var getHighscoresRequest = bucket.MapReduce(
			new JavaScriptMapPhase(highscoresMapFunction),
			new JavaScriptReducePhase(highscoresReduceFunction, maxPlayerCount)
		);
		
		// Wait until the request finishes and then update the local list of highscore entries.
		yield return getHighscoresRequest.WaitUntilDone();
		
		if(getHighscoresRequest.isSuccessful) {
			IEnumerable<RankEntry> rankingEntriesTmp = getHighscoresRequest.GetResult<RankEntry>();
			GameDB.rankingEntries = rankingEntriesTmp.ToArray();
			
			// Get player names
			// TODO: Send X requests at once, then wait for all of them
			var nameBucket = new Bucket("AccountToName");
			var nameRequests = new GetRequest[GameDB.rankingEntries.Length];
			for(int i = 0; i < GameDB.rankingEntries.Length; i++) {
				var entry = GameDB.rankingEntries[i];
				entry.rankIndex = i;
				
				if(GameDB.accountIdToName.ContainsKey(entry.accountId)) {
					entry.name = GameDB.accountIdToName[entry.accountId];
					nameRequests[i] = null;
				} else {
					nameRequests[i] = nameBucket.Get(entry.accountId);
				}
			}
			
			for(int i = 0; i < nameRequests.Length; i++) {
				var nameRequest = nameRequests[i];
				if(nameRequest == null)
					continue;
				
				yield return nameRequest.WaitUntilDone();
				
				if(nameRequest.isSuccessful) {
					var entry = GameDB.rankingEntries[i];
					entry.name = nameRequest.GetValue<string>();
					GameDB.accountIdToName[entry.accountId] = entry.name;
				}
			}
			
			//Debug.Log("Sending the ranking list " + GameDB.rankingEntries + " with " + rankingEntries.Length + " entries");
			Lobby.RPC("ReceiveRankingList", peer, GameDB.rankingEntries, false);
		} else {
			Debug.Log("Failed getting the ranking list: " + getHighscoresRequest.GetErrorString());
			Lobby.RPC("ReceiveRankingList", peer, null, false);
		}
	}
	
	// This is the JavaScript code for the map phase. The map phase operates on each key/value pair in the bucket
	// and should produce a list of any length. The list is then concatenated with the output of other map
	// operations and fed into the reduce phase. This map phase just parses the text value to a JSON object and
	// returns it as a one-element list.
	private const string highscoresMapFunction =
	@"
	function(value, keydata, arg) {
		var scoreEntry = JSON.parse(value.values[0].data);
		return [[0, value.key, '', scoreEntry.bestRanking]];
	}
	";
	
	// This is the JavaScript code for the reduce phase. The reduce phase operates on a combined list of the results
	// from any number of map phases, and should produce a new list. The resulting list can then be combined with
	// even more map phase results and fed into another reduce phase, so it is important that the reduce function
	// can be run many times on the same data without failing. This reduce phase sorts the items in the list
	// by score and trims away any item beyond the maxScoreCount argument that was sent along with the request.
	private const string highscoresReduceFunction =
	@"
	function(valueList, maxScoreCount) {
		var descendingOrder = function(a, b) { return b[3] - a[3]; };
		valueList.sort(descendingOrder);
		if (valueList.length > maxScoreCount) { valueList.length = maxScoreCount; }
		return valueList;
	}
	";
}
