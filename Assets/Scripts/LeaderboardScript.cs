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

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

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
        if (eventsGetResponse?.running == null || eventsGetResponse.running.Count == 0)
        {
            Debug.LogError("No running events found.");
            return;
        }

        var eventView = eventsGetResponse.running[0];
        if (eventView == null)
        {
            Debug.LogError("Event with ID is not running.");
            return;
        }

        var eventId = eventView.id;
        Debug.Log($"Event ID: {eventId}");

        if (string.IsNullOrEmpty(eventView.leaderboardId))
        {
            await RegisterRandomScore(eventId);
        }
        else
        {
            if (!await LeaderboardExists(eventView.leaderboardId))
            {
                await RegisterRandomScore(eventId);
            }
            else
            {
                await EnsureScoreOnLeaderboard(eventView.leaderboardId, eventId);
            }
        }

        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        if (string.IsNullOrEmpty(groupId))
        {
            Debug.LogError("Group ID is not set in PlayerPrefs.");
            return;
        }

        var customLeaderboardId = $"event_{eventId}_group_{groupId}";
        Debug.Log($"Constructed Leaderboard ID: {customLeaderboardId}");

        if (!await LeaderboardExists(customLeaderboardId))
        {
            await _service.SetGroupLeaderboard(customLeaderboardId);
            await _service.SetLeaderboardScore(customLeaderboardId, GenerateRandomScore());
        }
        
        var view = await _beamContext.Api.LeaderboardService.GetBoard(customLeaderboardId, 1, 1000);
        var rankings = view.rankings;

        bool hasScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId);
        bool hasZeroScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId && rankEntry.score == 0);
        Debug.Log(hasZeroScore);
        if (!hasScore || hasZeroScore)
        {
            // Generate a random score between 0 and 1000
            double randomScore = GenerateRandomScore();
            await _service.SetLeaderboardScore(customLeaderboardId, randomScore);
        }
        else
        {
            Debug.Log("Player already has a score on the leaderboard.");
        }
        
        await DisplayLeaderboard(customLeaderboardId);
        
    }

    private async Task RegisterRandomScore(string eventId)
    {
        double randomScore = GenerateRandomScore();
        await _service.SetScore(eventId, randomScore);
        Debug.Log("Score registered " + randomScore);

    }

    private async Task<bool> LeaderboardExists(string leaderboardId)
    {
        try
        {
            await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1);
            return true;
        }
        catch (PlatformRequesterException e)
        {
                Debug.LogWarning($"Leaderboard with ID {leaderboardId} does not exist.");
                return false;
        }
    }

    private async Task EnsureScoreOnLeaderboard(string leaderboardId, string eventId)
    {
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = view.rankings;

        bool hasScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId);
        if (!hasScore)
        {
            await RegisterRandomScore(eventId);
        }
        else
        {
            Debug.Log("Player already has a score on the leaderboard.");
        }
    }

    private async Task DisplayLeaderboard(string leaderboardId)
    {
        try
        {
            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);

            foreach (Transform child in scrollViewContent)
            {
                Destroy(child.gameObject);
            }

            foreach (var (rankEntry, index) in view.rankings.WithIndex())
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
