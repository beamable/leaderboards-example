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
    private string _tournamentId;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;
    [SerializeField] private TMP_Text groupNameText;

    private async void Start()
    {
        Debug.Log("Starting TournamentScript...");
        
        // Initialize BeamContext and services
        try
        {
            _beamContext = await BeamContext.Default.Instance;
            Debug.Log("BeamContext initialized.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing BeamContext: {e.Message}");
            return;
        }
        
        _service = new BackendServiceClient();
        _userService = new UserServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);

        // Load group ID from PlayerPrefs
        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
        Debug.Log($"Loaded Group ID from PlayerPrefs: {_groupIdString}");
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

        _tournamentId = activeTournament.tournamentId;
        Debug.Log($"Active Tournament ID: {_tournamentId}");

        // Join the tournament
        await JoinTournament(activeTournament.tournamentId);

        // Construct the leaderboard ID
        var leaderboardId = await ConstructGroupLeaderboardId(activeTournament.tournamentId);
        if (string.IsNullOrEmpty(leaderboardId))
        {
            Debug.LogError("Leaderboard ID could not be constructed.");
            return;
        }

        // Ensure the leaderboard exists and manage the score
        await EnsureTournamentScore(leaderboardId, activeTournament.tournamentId);

        // Display the leaderboard
        await DisplayLeaderboard(leaderboardId);
    }

    private async Task EnsureTournamentScore(string leaderboardId, string tournamentId)
    {
        Debug.Log($"Ensuring tournament score for leaderboard ID: {leaderboardId}");
        
        bool leaderboardExists = await LeaderboardExists(leaderboardId);
        if (!leaderboardExists)
        {
            Debug.Log("Leaderboard does not exist. Updating stats and creating leaderboard...");
            await UpdateTournamentStats();

            var points = await GetTournamentPoints();
            await CreateAndPopulateLeaderboard(leaderboardId, points);
        }
        else
        {
            Debug.Log("Leaderboard exists. Ensuring player score...");
            await EnsurePlayerScoreOnLeaderboard(leaderboardId, tournamentId);
        }
    }

    private async Task UpdateTournamentStats()
    {
        Debug.Log("Updating Tournament Stats...");
        var points = await GetTournamentPoints();
        Debug.Log($"Retrieved Tournament Points: {points}");

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
        Debug.Log("Fetching tournaments...");
        var response = await _beamContext.Api.TournamentsService.GetAllTournaments();
        var tournaments = response?.tournaments ?? new List<TournamentInfo>();
        
        return tournaments;
    }

    private async Task JoinTournament(string tournamentId)
    {
        Debug.Log($"Joining tournament with ID: {tournamentId}");
        await _beamContext.Api.TournamentsService.JoinTournament(tournamentId);
        Debug.Log("Joined tournament successfully.");
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
            Debug.Log("Leaderboard does not exist.");
            return false;
        }
    }

    private async Task EnsurePlayerScoreOnLeaderboard(string leaderboardId, string tournamentId)
    {
        Debug.Log($"Ensuring player score on leaderboard: {leaderboardId}");
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = view.rankings;

        if (!rankings.Exists(rankEntry => rankEntry.gt == _beamContext.PlayerId))
        {
            double randomScore = GenerateRandomScore();
            Debug.Log($"Player score not found. Setting random score: {randomScore}");
            await _beamContext.Api.TournamentsService.SetScore(tournamentId, _beamContext.PlayerId, randomScore);
            await _service.SetLeaderboardScore(leaderboardId, randomScore);
        }
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId, double score)
    {
        Debug.Log($"Creating and populating leaderboard: {leaderboardId} with score: {score}");
        await _service.SetGroupLeaderboard(leaderboardId);
        await _service.SetLeaderboardScore(leaderboardId, score);
    }

    private async Task DisplayLeaderboard(string leaderboardId)
    {
        Debug.Log($"Displaying leaderboard: {leaderboardId}");
        try
        {
            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);

            ClearScrollViewContent();

            foreach (var rankEntry in view.rankings)
            {
                var username = await GetPlayerUsername(rankEntry.gt);
                CreateRankingItem(username, rankEntry.score.ToString());
            }

            Debug.Log("Leaderboard displayed successfully.");
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
        Debug.Log("Clearing scroll view content...");
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private static async Task<string> ConstructGroupLeaderboardId(string tournamentId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
        var leaderboardId = string.IsNullOrEmpty(groupId) ? null : $"{tournamentId}.0.0.group.{groupId}";
        Debug.Log($"Constructed leaderboard ID: {leaderboardId}");
        return await Task.FromResult(leaderboardId);
    }

    private async Task<string> GetPlayerUsername(long gamerTag)
    {
        try
        {
            Debug.Log($"Fetching username for gamerTag: {gamerTag}");
            var response = await _userService.GetPlayerAvatarName(gamerTag);
            var username = !string.IsNullOrEmpty(response.data) ? response.data : gamerTag.ToString();
            Debug.Log($"Retrieved username: {username}");
            return username;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching username: {e.Message}");
            return gamerTag.ToString();
        }
    }

    private void CreateRankingItem(string username, string score)
    {
        Debug.Log($"Creating ranking item for username: {username} with score: {score}");
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
        int score = new System.Random().Next(0, 1000);
        Debug.Log($"Generated random score: {score}");
        return score;
    }

    private async Task DisplayGroupName(long groupId)
    {
        Debug.Log($"Displaying group name for group ID: {groupId}");
        try
        {
            var group = await _groupManager.GetGroup(groupId);
            if (group != null)
            {
                groupNameText.text = group.name;
                Debug.Log($"Displayed group name: {group.name}");
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
        Debug.Log("Fetching active or upcoming tournament...");
        var tournaments = await GetTournaments();

        var validTournaments = tournaments.Where(t =>
        {
            DateTime startTime;
            DateTime endTime;
            bool isStartTimeValid = DateTime.TryParse(t.startTimeUtc, out startTime);
            bool isEndTimeValid = DateTime.TryParse(t.endTimeUtc, out endTime);

            return isStartTimeValid && isEndTimeValid && endTime > DateTime.UtcNow;
        }).ToList();

        if (validTournaments.Any())
        {
            Debug.Log($"Total valid tournaments found: {validTournaments.Count}");
            foreach (var tournament in validTournaments)
            {
                Debug.Log($"Valid Tournament - ID: {tournament.tournamentId}, Start Time: {tournament.startTimeUtc}, End Time: {tournament.endTimeUtc}, Cycle: {tournament.cycle}");
            }
            
            var tournamentWithLatestEndTime = validTournaments.OrderByDescending(t =>
            {
                DateTime endTime;
                DateTime.TryParse(t.endTimeUtc, out endTime);
                return endTime;
            }).FirstOrDefault();
            
            if (tournamentWithLatestEndTime != null)
            {
                Debug.Log($"Active or upcoming tournament found: ID {tournamentWithLatestEndTime.tournamentId}");
                return tournamentWithLatestEndTime;
            }
        }

        Debug.LogWarning("No active or upcoming tournament found.");
        return null;
    }

    private async Task ClaimAllRewards(string tournamentId)
    {
        Debug.Log($"Claiming all rewards for tournament ID: {tournamentId}");
        try
        {
            var rewardsNotClaimed = await _beamContext.Api.TournamentsService.GetUnclaimedRewards(tournamentId);
            Debug.Log($"not claimed rewards count {rewardsNotClaimed.rewardCurrencies.Count}");
            await _beamContext.Api.Tournaments.ClaimAllRewards(tournamentId);
            Debug.Log("All rewards claimed successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error claiming rewards: {e.Message}");
        }
    }

    public async void OnClaimRewardsButtonPressed()
    {
        Debug.Log("Claim rewards button pressed.");
        if (string.IsNullOrEmpty(_tournamentId))
        {
            Debug.LogError("Tournament ID is not set, unable to claim rewards.");
            return;
        }

        await ClaimAllRewards(_tournamentId);
    }
}