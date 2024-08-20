using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Common.Api.Tournaments;
using Beamable.Server.Clients;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AvgTournamentLeaderboard : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendGroupServiceClient _backendGroupService;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        // Initialize BeamContext and services
        _beamContext = await BeamContext.Default.Instance;
        
        _backendGroupService = new BackendGroupServiceClient();

        // Get the active or upcoming tournament
        var activeTournament = await GetActiveOrUpcomingTournament();
        if (activeTournament == null)
        {
            Debug.LogError("No active or upcoming tournament found.");
            return;
        }
        
        // Construct the group average leaderboard ID
        var groupAvgLeaderboardId = ConstructGroupAvgLeaderboardId(activeTournament.tournamentId);

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
                CreateRankingItem(groupName, rankEntry.score.ToString(), rankEntry.gt, leaderboardId);
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
            var group = await _beamContext.Api.GroupsService.GetGroup(groupId);
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
}
