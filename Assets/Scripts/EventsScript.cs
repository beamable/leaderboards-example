using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Common.Api.Events;
using Beamable.Server.Clients;
using Managers;
using TMPro;
using UnityEngine;

public class EventsScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    private UserServiceClient _userService;
    private PlayerGroupManager _groupManager;
    private string _groupIdString;
    private string _currentEventId;

    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private TMP_Text groupScoreText;
    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        await InitializeContext();

        if (TryGetGroupId(out var groupId))
        {
            await DisplayGroupName(groupId);
        }

        _beamContext.Api.EventsService.Subscribe(OnEventUpdate);
    }

    private async Task InitializeContext()
    {
        _beamContext = await BeamContext.Default.Instance;
        _service = new BackendServiceClient();
        _userService = new UserServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);
        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
    }

    private bool TryGetGroupId(out long groupId)
    {
        groupId = 0;
        return !string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out groupId);
    }


    private async void OnEventUpdate(EventsGetResponse eventsGetResponse)
    {
        if (!HasRunningEvents(eventsGetResponse)) return;

        var eventView = eventsGetResponse.running[0];
        if (eventView == null) return;

        _currentEventId = eventView.id;

        if (string.IsNullOrEmpty(eventView.leaderboardId))
        {
            await RegisterStatsBasedScore(eventView.id);
        }
        else
        {
            await EnsureScoreOnLeaderboard(eventView.leaderboardId, eventView.id);
        }

        var customLeaderboardId = ConstructCustomLeaderboardId(eventView.id);
        if (string.IsNullOrEmpty(customLeaderboardId)) return;

        if (!await LeaderboardExists(customLeaderboardId))
        {
            await CreateAndPopulateLeaderboard(customLeaderboardId);
        }

        await EnsurePlayerScoreOnCustomLeaderboard(customLeaderboardId);
        await DisplayLeaderboard(customLeaderboardId);
        await DisplayGroupScore(eventView.id);
    }

    private async Task RegisterStatsBasedScore(string eventId)
    {
        var points = await GetVictoryPoints();
        var leaderboardStats = new Dictionary<string, object>
        {
            { "event_points", points },
        };

        await _service.SetEventScore(eventId, points, leaderboardStats);
    }

    private async Task<int> GetVictoryPoints()
    {
        var stats = await _beamContext.Api.StatsService.GetStats("client", "public", "player", _beamContext.PlayerId);
        return stats.TryGetValue("EVENT_POINTS", out var points) ? int.Parse(points) : GenerateRandomScore();
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
    }

    private async Task EnsurePlayerScoreOnCustomLeaderboard(string leaderboardId)
    {
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = view.rankings;

        var playerEntry = rankings.Find(rankEntry => rankEntry.gt == _beamContext.PlayerId);
        if (playerEntry == null || playerEntry.score == 0)
        {
            var randomPoints = GenerateRandomScore();
            await _service.SetStats("EVENT_POINTS", randomPoints.ToString());
            await _service.SetLeaderboardScore(leaderboardId, randomPoints);
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

    private async Task DisplayGroupScore(string eventId)
    {
        Debug.Log("Displaying group score...");

        // Construct the leaderboard ID for the group scores
        var groupLeaderboardId = $"event_{eventId}_groups";

        var view = await _beamContext.Api.LeaderboardService.GetBoard(groupLeaderboardId, 1, 1000);
        var groupRanking = view.rankings.FirstOrDefault(r => r.gt.ToString() == _groupIdString);
        int groupScore;

        if (groupRanking != null)
        {
            // If the group is found in the leaderboard
            groupScore = (int)groupRanking.score;
            Debug.Log($"Group found in leaderboard with score: {groupScore}");
        }
        else
        {
            // If the group is not found, sum up all the scores
            var customLeaderboardTitle = ConstructCustomLeaderboardId(eventId);
            var customLeaderboard = await _beamContext.Api.LeaderboardService.GetBoard(customLeaderboardTitle, 1, 1000);
            groupScore = customLeaderboard.rankings.Sum(r => (int)r.score);
            Debug.Log($"Group not found, summing all scores: {groupScore}");
        }

        // Update the text on the UI
        groupScoreText.text = $"Event Group Score: {groupScore}";
        Debug.Log($"Group score displayed: {groupScore}");
    }

    private async Task<string> GetPlayerUsername(long gamerTag)
    {
        try
        {
            var response = await _userService.GetPlayerAvatarName(gamerTag);
            return !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
            return !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
        }
        catch
        {
            return gamerTag.ToString();
        }
    }

    private void CreateRankingItem(string username, string score)
    {
        var rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        var texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();

        foreach (var text in texts)
        {
            if (text.name == "GamerTag") text.text = username;
            else if (text.name == "Score") text.text = score;
        }
    }

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private string ConstructCustomLeaderboardId(string eventId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        return string.IsNullOrEmpty(groupId) ? null : $"event_{eventId}_group_{groupId}";
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
            groupNameText.text = group?.name;
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
            Debug.Log("Reward claimed successfully.");
        }
        catch (PlatformRequesterException ex)
        {
            if (ex.Error.error == "NoClaimsPending")
            {
                Debug.Log("No rewards pending for this event.");
            }
            else
            {
                Debug.LogError($"Error claiming reward: {ex.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Unexpected error claiming reward: {e.Message}");
        }
    }


    public async void ClaimButton()
    {
        if (!string.IsNullOrEmpty(_currentEventId))
        {
            await ClaimRewards(_currentEventId);
        }
    }

    private int GenerateRandomScore() => new System.Random().Next(0, 1000);
}
