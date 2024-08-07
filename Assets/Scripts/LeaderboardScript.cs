using System;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Common.Api.Events;
using Beamable.Server.Clients;
using Extensions;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class LeaderboardScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    private UserServiceClient _userService;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;

        _service = new BackendServiceClient();
        _userService = new UserServiceClient();

        _beamContext.Api.EventsService.Subscribe(OnEventUpdate);
    }

    private async void OnEventUpdate(EventsGetResponse eventsGetResponse)
    {
        if (!HasRunningEvents(eventsGetResponse))
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
        
        if (string.IsNullOrEmpty(eventView.leaderboardId))
        {
            await RegisterRandomScore(eventView.id);
        }
        else
        {
            await EnsureScoreOnLeaderboard(eventView.leaderboardId, eventView.id);
        }

        var customLeaderboardId = await ConstructCustomLeaderboardId(eventView.id);
        if (string.IsNullOrEmpty(customLeaderboardId))
        {
            Debug.LogError("Group ID is not set in PlayerPrefs.");
            return;
        }
        
        if (!await LeaderboardExists(customLeaderboardId))
        {
            await CreateAndPopulateLeaderboard(customLeaderboardId);
        }

        await EnsurePlayerScoreOnCustomLeaderboard(customLeaderboardId);
        await DisplayLeaderboard(customLeaderboardId);
    }

    private async Task RegisterRandomScore(string eventId)
    {
        var randomScore = GenerateRandomScore();
        await _service.SetScore(eventId, randomScore);
        Debug.Log($"Score registered: {randomScore}");
    }

    private async Task<bool> LeaderboardExists(string leaderboardId)
    {
        try
        {
            await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1);
            return true;
        }
        catch (PlatformRequesterException)
        {
            Debug.LogWarning($"Leaderboard with ID {leaderboardId} does not exist.");
            return false;
        }
    }

    private async Task EnsureScoreOnLeaderboard(string leaderboardId, string eventId)
    {
        var rankings = (await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000)).rankings;

        if (!rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId))
        {
            await RegisterRandomScore(eventId);
        }
        else
        {
            Debug.Log("Player already has a score on the leaderboard.");
        }
    }

    private async Task EnsurePlayerScoreOnCustomLeaderboard(string leaderboardId)
    {
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = view.rankings;

        bool hasScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId);
        bool hasZeroScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId && rankEntry.score == 0);
        Debug.Log(hasZeroScore);
        if (!hasScore || hasZeroScore)
        {
            double randomScore = GenerateRandomScore();
            await _service.SetLeaderboardScore(leaderboardId, randomScore);
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
            var rankings = (await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000)).rankings;
            ClearScrollViewContent();

            foreach (var (rankEntry, _) in rankings.WithIndex())
            {
                var username = await GetPlayerUsername(rankEntry.gt);
                CreateRankingItem(username, rankEntry.score.ToString());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching leaderboard: {e.Message}");
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
        var rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        var texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();

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
        return new System.Random().Next(0, 1000);
    }

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    [ItemCanBeNull]
    private async Task<string> ConstructCustomLeaderboardId(string eventId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        await _service.SetLeaderboardScore($"event_{eventId}_groups", 100);
        Debug.Log("Set Group score to 100");
        return string.IsNullOrEmpty(groupId) ? null : $"event_{eventId}_group_{groupId}";
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId)
    {
        await _service.SetGroupLeaderboard(leaderboardId);
        await _service.SetLeaderboardScore(leaderboardId, GenerateRandomScore());
    }

    private static bool HasRunningEvents(EventsGetResponse eventsGetResponse)
    {
        return eventsGetResponse?.running != null && eventsGetResponse.running.Count > 0;
    }
}
