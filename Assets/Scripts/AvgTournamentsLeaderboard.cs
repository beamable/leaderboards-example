using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Common.Api.Tournaments;
using Beamable.Server.Clients;
using Managers;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AvgTournamentLeaderboard : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendGroupServiceClient _backendGroupService;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;
    private PlayerGroupManager _groupManager;


    private async void Start()
    {
        // Initialize BeamContext and services
        _beamContext = await BeamContext.Default.Instance;
        
        _backendGroupService = new BackendGroupServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);
        await _groupManager.Initialize();
        
        // Get the active or upcoming tournament
        var activeTournament = await GetActiveOrUpcomingTournament();
        if (activeTournament == null)
        {
            Debug.LogError("No active or upcoming tournament found.");
            return;
        }
        
        // Construct the group average leaderboard ID
        var groupAvgLeaderboardId = await DiscoverLeaderboardId(activeTournament.tournamentId);

        // Log the leaderboard ID
        Debug.Log($"Constructed Group Average Leaderboard ID: {groupAvgLeaderboardId}");

        // Display the leaderboard
        await DisplayGroupLeaderboard(groupAvgLeaderboardId);
    }

    private async Task<TournamentInfo> GetActiveOrUpcomingTournament()
    {
        var tournaments = await GetTournaments();

        // Filter to get active and future tournaments
        var validTournaments = tournaments.Where(t =>
        {
            var isStartTimeValid = DateTime.TryParse(t.startTimeUtc, out _);
            var isEndTimeValid = DateTime.TryParse(t.endTimeUtc, out var endTime);

            // Consider tournaments that are either currently active or will start in the future
            return isStartTimeValid && isEndTimeValid && endTime > DateTime.UtcNow;
        }).ToList();

        if (validTournaments.Any())
        {
            var tournamentWithLatestEndTime = validTournaments.OrderByDescending(t =>
            {
                DateTime endTime;
                DateTime.TryParse(t.endTimeUtc, out endTime);
                return endTime;
            }).FirstOrDefault();

            return tournamentWithLatestEndTime;
        }

        Debug.LogWarning("No valid tournaments found.");
        return null;
    }

    private async Task<List<TournamentInfo>> GetTournaments()
    {
        var response = await _beamContext.Api.TournamentsService.GetAllTournaments();
        var tournaments = response?.tournaments ?? new List<TournamentInfo>();
        return tournaments;
    }

    private string ConstructGroupAvgLeaderboardId(string tournamentId)
    {
        var leaderboardId = $"{tournamentId}.0.0.group#0";
        return leaderboardId;
    }

    private async Task DisplayGroupLeaderboard(string leaderboardId)
    {
        try
        {
            // Fetch banned groups for this leaderboard
            var bannedGroupsResponse = await _backendGroupService.GetBannedGroups(leaderboardId);
            var bannedGroups = bannedGroupsResponse.data ?? new List<long>();

            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
            var rankings = view.rankings;
            ClearScrollViewContent();

            foreach (var rankEntry in rankings.Where(rankEntry => !bannedGroups.Contains(rankEntry.gt)))
            {
                var groupName = await GetGroupName(rankEntry.gt);
                var groupMembersCount = await GetGroupMembersCount(rankEntry.gt);
                var averageScore = groupMembersCount > 0 ? rankEntry.score / groupMembersCount : 0;
                CreateRankingItem(groupName, averageScore.ToString(), rankEntry.gt, leaderboardId);
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

    private async Task<string> GetGroupName(long groupId)
    {
        try
        {
            var group = await _groupManager.GetGroup(groupId);
            return group != null ? group.name : groupId.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching group name: {e.Message}");
            return groupId.ToString();
        }
    }

    private void CreateRankingItem(string groupName, string score, long groupId, string leaderboardId)
    {
        var rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        var texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length < 2)
        {
            Debug.LogError("RankingItemPrefab must have at least two TextMeshProUGUI components for GroupName and Score.");
            return;
        }

        foreach (var text in texts)
        {
            text.text = text.name switch
            {
                "GroupName" => groupName,
                "Score" => score,
                _ => text.text
            };
        }

        var banButton = rankingItem.GetComponentInChildren<Button>();
        banButton.onClick.AddListener(async () => await BanGroup(groupId, leaderboardId));
    }

    private async Task BanGroup(long groupId, string leaderboardId)
    {
        var response = await _backendGroupService.BanGroup(leaderboardId, groupId);
        if (response.data)
        {
            // Refresh the leaderboard after banning the group
            await DisplayGroupLeaderboard(leaderboardId);
        }
        else
        {
            Debug.LogError($"Error banning group: {response.errorMessage}");
        }
    }

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }
    
    private async Task<int> GetGroupMembersCount(long groupId)
    {
        try
        {
            var groupCount = await _groupManager.GetGroupMembersCount(groupId);
            return groupCount;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching group members count: {e.Message}");
            return 0;
        }
    }
    
    private async Task<string> DiscoverLeaderboardId(string tournamentId)
    {
        string baseId = $"{tournamentId}.0.";
        int suffix = 2;

        while (suffix >= 0) 
        {
            string leaderboardId = $"{baseId}{suffix}.group#0";
            if (await LeaderboardExists(leaderboardId))
            {
                return leaderboardId;
            }
            suffix--;
        }

        return null;
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
}