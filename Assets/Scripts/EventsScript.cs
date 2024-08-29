using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Common.Api.Events;
using Beamable.Server.Clients;
using JetBrains.Annotations;
using Managers;
using TMPro;
using UnityEngine;

public class EventsScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    private UserServiceClient _userService;
    private string _groupIdString;
    private string _currentEventId;
    private PlayerGroupManager _groupManager;

    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;

        _service = new BackendServiceClient();
        _userService = new UserServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);

        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
        if (!string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out var groupId))
        {
            await DisplayGroupName(groupId);
        }

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
        
        _currentEventId = eventView.id;
        
        if (string.IsNullOrEmpty(eventView.leaderboardId))
        {
            await RegisterStatsBasedScore(eventView.id);
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

    private async Task RegisterStatsBasedScore(string eventId)
    {
        var points = await GetVictoryPoints(); // Retrieve the player's EVENT_POINTS
        var leaderboardStats = new Dictionary<string, object>
        {
            { "event_points", points },
            { "submission_timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        };

        await _service.SetEventScore(eventId, points, leaderboardStats); // Submit score with stats
    }

    private async Task<int> GetVictoryPoints()
    {
        var stats = await _beamContext.Api.StatsService.GetStats("client", "public", "player", _beamContext.PlayerId);
        if (stats.TryGetValue("EVENT_POINTS", out var points))
        {
            return int.Parse(points);
        }
        return GenerateRandomScore();
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
            await RegisterStatsBasedScore(eventId);
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

        var hasScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId);
        var hasZeroScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId && rankEntry.score == 0);

        if (!hasScore || hasZeroScore)
        {
            var randomPoints = GenerateRandomScore();
            await _service.SetStats("EVENT_POINTS", randomPoints.ToString());
            await _service.SetLeaderboardScore(leaderboardId, randomPoints);
        }
        else
        {
            Debug.Log("Player already has a score on the custom leaderboard.");
        }
    }

    private async Task DisplayLeaderboard(string leaderboardId)
    {
        try
        {
            var rankings = (await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000)).rankings;
            ClearScrollViewContent();

            foreach (var rankEntry in rankings)
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
            var username = !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
            return username;
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
            return;
        }

        foreach (var text in texts)
        {
            text.text = text.name switch
            {
                "GamerTag" => username,
                "Score" => score,
                _ => text.text
            };
        }
    }

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    [ItemCanBeNull]
    private static Task<string> ConstructCustomLeaderboardId(string eventId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        return Task.FromResult(string.IsNullOrEmpty(groupId) ? null : $"event_{eventId}_group_{groupId}");
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId)
    {
        await _service.SetGroupLeaderboard(leaderboardId);
        var points = await GetVictoryPoints();
        await _service.SetStats("EVENT_POINTS", points.ToString());
        await _service.SetLeaderboardScore(leaderboardId, points);
    }

    private static bool HasRunningEvents(EventsGetResponse eventsGetResponse)
    {
        return eventsGetResponse?.running != null && eventsGetResponse.running.Count > 0;
    }

    private async Task DisplayGroupName(long groupId)
    {
        try
        {
            var group = await _groupManager.GetGroup(groupId);
            if (group != null)
            {
                groupNameText.text = group.name;
            }
            else
            {
                Debug.LogError("Group details are null.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching group details: {e.Message}");
        }
    }
    
    private async Task ClaimRewards(string eventId)
    {
        try
        {
            await _beamContext.Api.EventsService.Claim(eventId);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error claiming reward: {e.Message}");
        }
    }

    public async void ClaimButton()
    {
        await ClaimRewards(_currentEventId);
    }
    
    private int GenerateRandomScore()
    {
        var random = new System.Random();
        return random.Next(0, 1000);
    }
}
