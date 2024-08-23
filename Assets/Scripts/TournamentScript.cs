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

public class TournamentScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    private UserServiceClient _userService;
    private string _groupIdString;
    private PlayerGroupManager _groupManager;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;
    [SerializeField] private TMP_Text groupNameText;

    private async void Start()
    {
        // Initialize BeamContext and services
        _beamContext = await BeamContext.Default.Instance;
        
        _service = new BackendServiceClient();
        _userService = new UserServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);

        // Load group ID from PlayerPrefs
        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
        if (!string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out var groupId))
        {
            await DisplayGroupName(groupId);
        }

        // Get the active or upcoming tournament
        var activeTournament = await GetActiveOrUpcomingTournament();
        if (activeTournament == null)
        {
            Debug.LogError("No active or upcoming tournament found.");
            return;
        }

        // Join the tournament
        await JoinTournament(activeTournament.tournamentId);

        // Construct the leaderboard ID
        var leaderboardId = await ConstructGroupLeaderboardId(activeTournament.tournamentId);
        if (string.IsNullOrEmpty(leaderboardId))
        {
            Debug.LogError("Group ID is not set in PlayerPrefs.");
            return;
        }

        // Ensure the leaderboard exists and manage the score
        await EnsureTournamentScore(leaderboardId, activeTournament.tournamentId);

        // Display the leaderboard
        await DisplayLeaderboard(leaderboardId);
    }

    private async Task EnsureTournamentScore(string leaderboardId, string tournamentId)
    {
        bool leaderboardExists = await LeaderboardExists(leaderboardId);
        if (!leaderboardExists)
        {
            // If leaderboard doesn't exist, update stats and then create the leaderboard
            await UpdateTournamentStats();

            // Create and populate leaderboard after stats update
            var points = await GetTournamentPoints();
            await CreateAndPopulateLeaderboard(leaderboardId, points);
        }
        else
        {
            // Ensure the player's score is on the leaderboard
            await EnsurePlayerScoreOnLeaderboard(leaderboardId);
        }
    }

    private async Task UpdateTournamentStats()
    {
        Debug.Log("Updating Tournament Stats...");
        var points = await GetTournamentPoints(); // Retrieve the player's TOURNAMENT_POINTS
        Debug.Log($"Retrieved Tournament Points: {points}");

        // Set the updated stats
        var updatedStats = new Dictionary<string, string>
        {
            { "TOURNAMENT_POINTS", points.ToString() }
        };

        await _service.SetStats("TOURNAMENTS_POINTS", points.ToString());
        Debug.Log("Tournament Stats updated.");
    }

    private async Task<int> GetTournamentPoints()
    {
        Debug.Log("Fetching Tournament Points...");
        var stats = await _beamContext.Api.StatsService.GetStats("client", "public", "player", _beamContext.PlayerId);
        if (stats.TryGetValue("TOURNAMENT_POINTS", out var points))
        {
            Debug.Log($"Tournament Points found: {points}");
            return int.Parse(points);
        }
        Debug.LogWarning("No Tournament Points found, returning RANDOM SCORE.");
        return GenerateRandomScore();
    }

    private async Task<List<TournamentInfo>> GetTournaments()
    {
        var response = await _beamContext.Api.TournamentsService.GetAllTournaments();
        var tournaments = response?.tournaments ?? new List<TournamentInfo>();

        if (tournaments.Count > 0)
        {
            Debug.Log($"Total tournaments fetched: {tournaments.Count}");
            foreach (var tournament in tournaments)
            {
                Debug.Log($"Tournament ID: {tournament.tournamentId}, Start Time: {tournament.startTimeUtc}, End Time: {tournament.endTimeUtc}, Cycle: {tournament.cycle}");
            }
        }
        else
        {
            Debug.LogWarning("No tournaments found.");
        }
        
        return tournaments;
    }

    private async Task JoinTournament(string tournamentId)
    {
        await _beamContext.Api.TournamentsService.JoinTournament(tournamentId);
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

    private async Task EnsurePlayerScoreOnLeaderboard(string leaderboardId)
    {
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = view.rankings;

        if (!rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId))
        {
            double randomScore = GenerateRandomScore();
            await _service.SetLeaderboardScore(leaderboardId, randomScore);
        }
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId, double score)
    {
        await _service.SetGroupLeaderboard(leaderboardId);
        await _service.SetLeaderboardScore(leaderboardId, score);
    }

    private async Task DisplayLeaderboard(string leaderboardId)
    {
        try
        {
            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);

            ClearScrollViewContent();

            foreach (var rankEntry in view.rankings)
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

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private async Task<string> ConstructGroupLeaderboardId(string tournamentId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        return await Task.FromResult(string.IsNullOrEmpty(groupId) ? null : $"{tournamentId}.0.0.group.{groupId}");
    }

    private async Task<string> GetPlayerUsername(long gamerTag)
    {
        try
        {
            var response = await _userService.GetPlayerAvatarName(gamerTag);
            return !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
        }
        catch (Exception)
        {
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

    private int GenerateRandomScore()
    {
        return new System.Random().Next(0, 1000);
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

    private async Task<TournamentInfo> GetActiveOrUpcomingTournament()
    {
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
            // Select the tournament that ends last
            var tournamentWithLatestEndTime = validTournaments.OrderByDescending(t =>
            {
                DateTime endTime;
                DateTime.TryParse(t.endTimeUtc, out endTime);
                return endTime;
            }).FirstOrDefault();

            if (tournamentWithLatestEndTime != null)
            {
                return tournamentWithLatestEndTime;
            }
        }

        return null;
    }
}
