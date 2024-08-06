using System;
using Beamable;
using Beamable.Common.Api.Events;
using Beamable.Common.Api.Leaderboards;
using Beamable.Server.Clients;
using Extensions;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using Beamable.Api;
using UnityEngine.UI;

public class LeaderboardScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    private UserServiceClient _userService;

    [SerializeField] private GameObject rankingItemPrefab; // Prefab for displaying each ranking
    [SerializeField] private Transform scrollViewContent; // Content GameObject of the ScrollView

    async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;
        await _beamContext.OnReady;
        
        _service = new BackendServiceClient();
        _userService = new UserServiceClient();
        
        _beamContext.Api.EventsService.Subscribe(OnEventUpdate);
    }

    private async void OnEventUpdate(EventsGetResponse eventsGetResponse)
    {
        if (eventsGetResponse == null || eventsGetResponse.running == null || eventsGetResponse.running.Count == 0)
        {
            Debug.LogError("No running events found.");
            return;
        }

        var eventView = eventsGetResponse.running[0];
        if (eventView != null)
        {
            var eventId = eventView.id;
            Debug.Log(eventId);
            Debug.Log(eventView.leaderboardId);
            // double randomScore = GenerateRandomScore();
            // await _service.SetScore(eventId, randomScore);

            if (string.IsNullOrEmpty(eventView.leaderboardId))
            {
                // Generate a random score between 0 and 1000
                var randomScore = GenerateRandomScore();
                await _service.SetScore(eventId, randomScore);
            }
            else
            {
            var view = await _beamContext.Api.LeaderboardService.GetBoard(eventView.leaderboardId, 1, 1000); // Retrieve a larger set of rankings
            var rankings = view.rankings;
            // Check if the current player's gamertag is already in the leaderboard
            bool hasScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId);
            if (!hasScore)
            {
                // Generate a random score between 0 and 1000
                double randomScore = GenerateRandomScore();
                await _service.SetScore(eventId, randomScore);
            }
            else
            {
                Debug.Log("Player already has a score on the leaderboard.");
            }
            }
            

            // Construct custom leaderboardId
            var groupId = PlayerPrefs.GetString("SelectedGroupId");

            if (string.IsNullOrEmpty(groupId))
            {
                Debug.LogError("Group ID is not set in PlayerPrefs.");
                return;
            }
            
            var customLeaderboardId = $"event_{eventId}_group_{groupId}";
            Debug.Log($"Constructed Leaderboard ID: {customLeaderboardId}");
            
            try
            {
                var groupLeaderboard = await _beamContext.Api.LeaderboardService.GetBoard(customLeaderboardId, 1, 1000); // Retrieve a larger set of rankings
                

                // Clear existing items
                foreach (Transform child in scrollViewContent)
                {
                    Destroy(child.gameObject);
                }

                // Display updated leaderboard
                foreach (var (rankEntry, index) in groupLeaderboard.rankings.WithIndex())
                {
                    var username = await GetPlayerUsername(rankEntry.gt);
                    CreateRankingItem(username, rankEntry.score.ToString());
                }
            }
            catch (PlatformRequesterException e)
            {
                Debug.LogError($"Error fetching leaderboard: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Unexpected error: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("Event with ID is not running.");
        }
    }



    private async Task<string> GetPlayerUsername(long gamerTag)
    {
        try
        {
            var response = await _userService.GetPlayerAvatarName(gamerTag);
            return !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching player username: {e.Message}");
            return gamerTag.ToString();
        }
    }

    private void CreateRankingItem(string username, string score)
    {
        GameObject rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        TextMeshProUGUI[] texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length < 2)
        {
            Debug.LogError("RankingItemPrefab must have at least two TextMeshProUGUI components for GamerTag and Score.");
            return;
        }

        foreach (var text in texts)
        {
            if (text.name == "GamerTag")
            {
                text.text = username;
            }
            else if (text.name == "Score")
            {
                text.text = score;
            }
        }
    }

    private double GenerateRandomScore()
    {
        System.Random random = new System.Random();
        return random.Next(0, 1000);
    }
}
