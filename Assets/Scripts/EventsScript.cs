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
    private PlayerGroupManager _groupManager;

    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        Debug.Log("Starting EventsScript...");
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
        Debug.Log("Received event update...");
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
            Debug.Log("No leaderboard ID found. Registering stats-based score.");
            await RegisterStatsBasedScore(eventView.id);
        }
        else
        {
            Debug.Log($"Leaderboard ID found: {eventView.leaderboardId}. Ensuring score on leaderboard.");
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
            Debug.Log($"Creating and populating new leaderboard with ID: {customLeaderboardId}");
            await CreateAndPopulateLeaderboard(customLeaderboardId);
        }

        await EnsurePlayerScoreOnCustomLeaderboard(customLeaderboardId);
        await DisplayLeaderboard(customLeaderboardId);
    }

    private async Task RegisterStatsBasedScore(string eventId)
    {
        Debug.Log($"Registering stats-based score for event ID: {eventId}");
        var points = await GetVictoryPoints(); // Retrieve the player's EVENT_POINTS
        Debug.Log($"Retrieved Victory Points: {points}");

        var leaderboardStats = new Dictionary<string, object>
        {
            { "event_points", points },
            { "submission_timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        };

        await _service.SetEventScore(eventId, points, leaderboardStats); // Submit score with stats
        Debug.Log($"Score registered with stats: {points}, Stats: {leaderboardStats}");
    }

    private async Task<int> GetVictoryPoints()
    {
        Debug.Log("Fetching Victory Points...");
        var stats = await _beamContext.Api.StatsService.GetStats("client", "public", "player", _beamContext.PlayerId);
        if (stats.TryGetValue("EVENT_POINTS", out var points))
        {
            Debug.Log($"Victory Points found: {points}");
            return int.Parse(points);
        }
        Debug.LogWarning("No Victory Points found, returning 0.");
        return GenerateRandomScore();
    }

    private async Task<bool> LeaderboardExists(string leaderboardId)
    {
        try
        {
            Debug.Log($"Checking if leaderboard exists: {leaderboardId}");
            await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1);
            Debug.Log("Leaderboard exists.");
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
        Debug.Log($"Ensuring score on leaderboard: {leaderboardId}");
        var rankings = (await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000)).rankings;

        if (!rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId))
        {
            Debug.Log("No score found for player on the leaderboard. Registering new score.");
            await RegisterStatsBasedScore(eventId);
        }
        else
        {
            Debug.Log("Player already has a score on the leaderboard.");
        }
    }

    private async Task EnsurePlayerScoreOnCustomLeaderboard(string leaderboardId)
    {
        Debug.Log($"Ensuring player score on custom leaderboard: {leaderboardId}");
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = view.rankings;

        bool hasScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId);
        bool hasZeroScore = rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId && rankEntry.score == 0);
        Debug.Log($"Has Score: {hasScore}, Has Zero Score: {hasZeroScore}");

        if (!hasScore || hasZeroScore)
        {
            var randomPoints = GenerateRandomScore();
            Debug.Log($"Registering player score on custom leaderboard: {randomPoints}");
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
            Debug.Log($"Displaying leaderboard: {leaderboardId}");
            var rankings = (await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000)).rankings;
            ClearScrollViewContent();

            foreach (var rankEntry in rankings)
            {
                var username = await GetPlayerUsername(rankEntry.gt);
                CreateRankingItem(username, rankEntry.score.ToString());
                Debug.Log($"Ranking item created for {username} with score {rankEntry.score}");
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
            Debug.Log($"Fetching username for gamerTag: {gamerTag}");
            var response = await _userService.GetPlayerAvatarName(gamerTag);
            string username = !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
            Debug.Log($"Username fetched: {username}");
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
        Debug.Log($"Creating ranking item for {username} with score {score}");
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

    private void ClearScrollViewContent()
    {
        Debug.Log("Clearing scroll view content...");
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    [ItemCanBeNull]
    private Task<string> ConstructCustomLeaderboardId(string eventId)
    {
        Debug.Log($"Constructing custom leaderboard ID for event ID: {eventId}");
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        return Task.FromResult(string.IsNullOrEmpty(groupId) ? null : $"event_{eventId}_group_{groupId}");
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId)
    {
        Debug.Log($"Creating and populating leaderboard: {leaderboardId}");
        await _service.SetGroupLeaderboard(leaderboardId);
        var points = await GetVictoryPoints();
        Debug.Log($"Populating leaderboard with initial score: {points}");
        await _service.SetStats("EVENT_POINTS", points.ToString());
        await _service.SetLeaderboardScore(leaderboardId, points);
    }

    private static bool HasRunningEvents(EventsGetResponse eventsGetResponse)
    {
        Debug.Log("Checking if there are running events...");
        return eventsGetResponse?.running != null && eventsGetResponse.running.Count > 0;
    }

    private async Task DisplayGroupName(long groupId)
    {
        try
        {
            Debug.Log($"Displaying group name for group ID: {groupId}");
            var group = await _groupManager.GetGroup(groupId);
            if (group != null)
            {
                groupNameText.text = group.name;
                Debug.Log($"Group name displayed: {group.name}");
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
    
    private int GenerateRandomScore()
    {
        var random = new System.Random();
        return random.Next(0, 1000);
    }
}
