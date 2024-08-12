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

    // Note: When a player joins a tournament, their current group (if any) is automatically 
    // registered in the leaderboard by the system. However, if the player switches to a 
    // different group after already being registered in the tournament, the new group 
    // will not be automatically registered in the leaderboard. 
    // Therefore, in such cases, we manually check for the leaderboard's existence 
    // and create it if necessary. This ensures that the new group can participate in the leaderboard,
    // even if the system does not automatically handle this scenario.
    
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

    // Check if the leaderboard exists
    bool leaderboardExists = await LeaderboardExists(leaderboardId);
    double score;

    if (!leaderboardExists)
    {
        // Generate a score
        score = GenerateRandomScore();
        
        // Set the score in the tournament
        await SetScoreForTournament(activeTournament.tournamentId, score);

        // Check if the leaderboard now exists
        leaderboardExists = await LeaderboardExists(leaderboardId);
        if (!leaderboardExists)
        {
            // Create and populate the leaderboard with the same score
            await CreateAndPopulateLeaderboard(leaderboardId, score);
        }
    }
    else
    {
        // Ensure the player's score is on the leaderboard
        await EnsurePlayerScoreOnLeaderboard(leaderboardId);
    }

    // Display the leaderboard
    await DisplayLeaderboard(leaderboardId);
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

            return tournamentWithLatestEndTime;
        }

        return null;
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
        
        return response?.tournaments ?? new List<TournamentInfo>();
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

    private async Task SetScoreForTournament(string tournamentId, double score)
    {
        try
        {
            await _beamContext.Api.TournamentsService.SetScore(tournamentId, _beamContext.PlayerId, score);
            Debug.Log($"Score {score} set for tournament {tournamentId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to set score for tournament {tournamentId}: {e.Message}");
        }
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId, double score)
    {
        await _service.SetGroupLeaderboard(leaderboardId);
        await _service.SetLeaderboardScore(leaderboardId, score);
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

    private async Task DisplayLeaderboard(string leaderboardId)
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

    private Task<string> ConstructGroupLeaderboardId(string tournamentId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        return Task.FromResult(string.IsNullOrEmpty(groupId) ? null : $"{tournamentId}.0.0.group.{groupId}");
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

    private double GenerateRandomScore()
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
}
