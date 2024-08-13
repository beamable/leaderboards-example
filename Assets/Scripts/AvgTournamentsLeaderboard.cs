using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Common.Api.Tournaments;
using UnityEngine;
using TMPro;

public class AvgTournamentLeaderboard : MonoBehaviour
{
    private BeamContext _beamContext;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        // Initialize BeamContext and services
        _beamContext = await BeamContext.Default.Instance;
        
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
            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);

            foreach (Transform child in scrollViewContent)
            {
                Destroy(child.gameObject);
            }

            foreach (var rankEntry in view.rankings)
            {
                var groupName = await GetGroupName(rankEntry.gt);
                CreateRankingItem(groupName, rankEntry.score.ToString());
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
            if (group != null)
            {
                return group.name;
            }

            return groupId.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching group name: {e.Message}");
            return groupId.ToString();
        }
    }

    private void CreateRankingItem(string groupName, string score)
    {
        GameObject rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        TextMeshProUGUI[] texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length < 2)
        {
            Debug.LogError("RankingItemPrefab must have at least two TextMeshProUGUI components for GroupName and Score.");
            return;
        }

        foreach (var text in texts)
        {
            if (text.name == "GamerTag")
            {
                text.text = groupName;
            }
            else if (text.name == "Score")
            {
                text.text = score;
            }
        }
    }
}
