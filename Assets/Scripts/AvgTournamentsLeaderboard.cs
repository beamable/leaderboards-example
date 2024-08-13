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

public class AvgTournamentLeaderboard : MonoBehaviour
{
    private BeamContext _beamContext;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        Debug.Log("Starting TournamentScript...");

        // Initialize BeamContext and services
        _beamContext = await BeamContext.Default.Instance;

        Debug.Log("BeamContext and services initialized.");

        // Get the active or upcoming tournament
        var activeTournament = await GetActiveOrUpcomingTournament();
        if (activeTournament == null)
        {
            Debug.LogError("No active or upcoming tournament found.");
            return;
        }

        Debug.Log($"Active Tournament found: ID={activeTournament.tournamentId}, Start Time={activeTournament.startTimeUtc}, End Time={activeTournament.endTimeUtc}");

        // Construct the group average leaderboard ID
        var groupAvgLeaderboardId = ConstructGroupAvgLeaderboardId(activeTournament.tournamentId);
        Debug.Log($"Constructed Group Average Leaderboard ID: {groupAvgLeaderboardId}");

        // Display the leaderboard
        await DisplayGroupLeaderboard(groupAvgLeaderboardId);
    }

    private async Task<TournamentInfo> GetActiveOrUpcomingTournament()
    {
        Debug.Log("Fetching active or upcoming tournaments...");
        var tournaments = await GetTournaments();

        // Filter to get active and future tournaments
        var validTournaments = tournaments.Where(t =>
        {
            DateTime startTime;
            DateTime endTime;
            bool isStartTimeValid = DateTime.TryParse(t.startTimeUtc, out startTime);
            bool isEndTimeValid = DateTime.TryParse(t.endTimeUtc, out endTime);

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

            Debug.Log($"Valid Tournament found: ID={tournamentWithLatestEndTime.tournamentId}, End Time={tournamentWithLatestEndTime.endTimeUtc}");
            return tournamentWithLatestEndTime;
        }

        Debug.LogWarning("No valid tournaments found.");
        return null;
    }

    private async Task<List<TournamentInfo>> GetTournaments()
    {
        Debug.Log("Fetching all tournaments...");
        var response = await _beamContext.Api.TournamentsService.GetAllTournaments();
        var tournaments = response?.tournaments ?? new List<TournamentInfo>();

        Debug.Log($"Total tournaments fetched: {tournaments.Count}");
        return tournaments;
    }

    private string ConstructGroupAvgLeaderboardId(string tournamentId)
    {
        var leaderboardId = $"{tournamentId}.0.0.group#0";
        Debug.Log($"Constructed Group Average Leaderboard ID: {leaderboardId}");
        return leaderboardId;
    }

    private async Task DisplayGroupLeaderboard(string leaderboardId)
    {
        Debug.Log($"Displaying Group Leaderboard for ID: {leaderboardId}");
        try
        {
            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
            Debug.Log($"Leaderboard fetched: {view.rankings.Count} entries found.");

            foreach (Transform child in scrollViewContent)
            {
                Destroy(child.gameObject);
            }

            foreach (var rankEntry in view.rankings)
            {
                var groupName = await GetGroupName(rankEntry.gt);
                Debug.Log($"Group Rank Entry: Group={groupName}, Score={rankEntry.score}");
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
        Debug.Log($"Fetching group name for Group ID: {groupId}");
        try
        {
            var group = await _beamContext.Api.GroupsService.GetGroup(groupId);
            if (group != null)
            {
                Debug.Log($"Group name retrieved: {group.name}");
                return group.name;
            }

            Debug.LogWarning($"Group name not found. Returning Group ID as name.");
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
        Debug.Log($"Creating ranking item: Group={groupName}, Score={score}");
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
