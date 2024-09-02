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
    private PlayerGroupManager _groupManager;
    private string _currentEventId;

    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        if (!await InitializeContext()) return;

        var groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
        if (long.TryParse(groupIdString, out var groupId))
        {
            await DisplayGroupName(groupId);
        }

        _beamContext.Api.EventsService.Subscribe(OnEventUpdate);
    }

    private async Task<bool> InitializeContext()
    {
        try
        {
            _beamContext = await BeamContext.Default.Instance;
            _service = new BackendServiceClient();
            _userService = new UserServiceClient();
            _groupManager = new PlayerGroupManager(_beamContext);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing context: {e.Message}");
            return false;
        }
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

        var customLeaderboardId = await ConstructCustomLeaderboardId(eventView.id);
        if (string.IsNullOrEmpty(customLeaderboardId)) return;

        if (!await LeaderboardExists(customLeaderboardId))
        {
            await CreateAndPopulateLeaderboard(customLeaderboardId);
        }

        await EnsurePlayerScoreOnCustomLeaderboard(customLeaderboardId);
        await DisplayLeaderboard(customLeaderboardId);
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

    private async Task<string> GetPlayerUsername(long gamerTag)
    {
        try
        {
            var response = await _userService.GetPlayerAvatarName(gamerTag);
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

        if (texts.Length >= 2)
        {
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
