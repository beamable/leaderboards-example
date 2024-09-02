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
    private PlayerGroupManager _groupManager;
    private string _tournamentId;
    private string _groupIdString;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;
    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private TMP_Text groupScoreText;

    private async void Start()
    {
        Debug.Log("Starting TournamentScript...");

        if (!await InitializeContext()) return;

        Debug.Log("BeamContext initialized successfully.");

        // Load group ID and display group name
        if (TryGetGroupId(out var groupId))
        {
            Debug.Log($"Group ID found: {groupId}");
            await DisplayGroupName(groupId);
        }
        else
        {
            Debug.LogWarning("No Group ID found in PlayerPrefs.");
        }

        // Get and join the active tournament
        var activeTournament = await GetActiveOrUpcomingTournament();
        if (activeTournament == null)
        {
            Debug.LogError("No active or upcoming tournament found.");
            return;
        }

        _tournamentId = activeTournament.tournamentId;
        Debug.Log($"Active Tournament ID: {_tournamentId}");
        await JoinTournament(_tournamentId);

        // Construct leaderboard ID and ensure score
        var leaderboardId = ConstructGroupLeaderboardId(_tournamentId);
        if (string.IsNullOrEmpty(leaderboardId))
        {
            Debug.LogError("Failed to construct leaderboard ID.");
            return;
        }

        Debug.Log($"Constructed Leaderboard ID: {leaderboardId}");

        // Ensure leaderboard and score are properly set
        await EnsureTournamentScore(leaderboardId);
        await DisplayLeaderboard(leaderboardId);
        await DisplayGroupScore(activeTournament.tournamentId);
    }

    private async Task<bool> InitializeContext()
    {
        try
        {
            _beamContext = await BeamContext.Default.Instance;
            _service = new BackendServiceClient();
            _userService = new UserServiceClient();
            _groupManager = new PlayerGroupManager(_beamContext);
            _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing BeamContext: {e.Message}");
            return false;
        }
    }

    private bool TryGetGroupId(out long groupId)
    {
        groupId = 0;
        return !string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out groupId);
    }
    
    private async Task EnsureTournamentScore(string leaderboardId)
    {
        int points = await GetTournamentPoints();

        if (!await LeaderboardExists(leaderboardId))
        {
            Debug.Log("Leaderboard does not exist. Setting score in the tournament first.");
            await _beamContext.Api.Tournaments.SetScore(_tournamentId, _beamContext.PlayerId, points);

            if (!await LeaderboardExists(leaderboardId))
            {
                Debug.Log("Leaderboard still does not exist after setting score. Creating it now.");
                await CreateAndPopulateLeaderboard(leaderboardId, points);
            }
        }
        else
        {
            Debug.Log("Leaderboard exists, ensuring player score...");
            await EnsurePlayerScoreOnLeaderboard(leaderboardId);
        }
    }

    private async Task<int> GetTournamentPoints()
    {
        Debug.Log("Fetching tournament points...");
        var stats = await _beamContext.Api.StatsService.GetStats("client", "public", "player", _beamContext.PlayerId);
        var points = stats.TryGetValue("TOURNAMENT_POINTS", out var value) ? int.Parse(value) : GenerateRandomScore();

        if (points == 0)
        {
            Debug.Log("No points found in stats, generating random score.");
            points = GenerateRandomScore();
            await _service.SetStats("TOURNAMENT_POINTS", points.ToString());
        }

        Debug.Log($"Tournament points: {points}");
        return points;
    }

    private async Task<List<TournamentInfo>> GetTournaments()
    {
        Debug.Log("Fetching all tournaments...");
        var response = await _beamContext.Api.TournamentsService.GetAllTournaments();
        var tournaments = response?.tournaments ?? new List<TournamentInfo>();
        Debug.Log($"Total tournaments fetched: {tournaments.Count}");
        return tournaments;
    }

    private async Task JoinTournament(string tournamentId)
    {
        Debug.Log($"Joining tournament with ID: {tournamentId}");
        await _beamContext.Api.TournamentsService.JoinTournament(tournamentId);
    }

    private async Task<bool> LeaderboardExists(string leaderboardId)
    {
        try
        {
            await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1);
            return true;
        }
        catch
        {
            Debug.LogWarning($"Leaderboard with ID {leaderboardId} does not exist.");
            return false;
        }
    }

    private async Task EnsurePlayerScoreOnLeaderboard(string leaderboardId)
    {
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        if (view.rankings.All(rankEntry => rankEntry.gt != _beamContext.PlayerId))
        {
            Debug.Log("Player does not have a score on the leaderboard, submitting a new score...");
            int points = await GetTournamentPoints();
            await _service.SetLeaderboardScore(leaderboardId, points);
            await _beamContext.Api.Tournaments.SetScore(_tournamentId, _beamContext.PlayerId, points);
        }
        else
        {
            Debug.Log("Player already has a score on the leaderboard.");
        }
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId, double score)
    {
        Debug.Log($"Creating and populating leaderboard with ID: {leaderboardId} and score: {score}");
        await _service.SetGroupLeaderboard(leaderboardId);
        await _service.SetLeaderboardScore(leaderboardId, score);
        await _beamContext.Api.Tournaments.SetScore(_tournamentId, _beamContext.PlayerId, score);
    }

    private async Task DisplayLeaderboard(string leaderboardId)
    {
        try
        {
            Debug.Log($"Displaying leaderboard with ID: {leaderboardId}");
            var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
            ClearScrollViewContent();

            foreach (var rankEntry in view.rankings)
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

    private void ClearScrollViewContent()
    {
        Debug.Log("Clearing leaderboard content...");
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private string ConstructGroupLeaderboardId(string tournamentId)
    {
        var groupId = _groupIdString;
        return string.IsNullOrEmpty(groupId) ? null : $"{tournamentId}.0.0.group.{groupId}";
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
        Debug.Log($"Creating ranking item for {username} with score {score}");
        var rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        var texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();

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

    private int GenerateRandomScore() => new System.Random().Next(0, 1000);

    private async Task DisplayGroupName(long groupId)
    {
        try
        {
            Debug.Log($"Fetching group name for group ID: {groupId}");
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

    private async Task<TournamentInfo> GetActiveOrUpcomingTournament()
    {
        var tournaments = await GetTournaments();
        var validTournaments = tournaments.Where(t => DateTime.TryParse(t.endTimeUtc, out var endTime) && endTime > DateTime.UtcNow).ToList();

        Debug.Log($"Active or upcoming tournaments count: {validTournaments.Count}");

        return validTournaments.OrderByDescending(t => DateTime.Parse(t.endTimeUtc)).FirstOrDefault();
    }

    private async Task ClaimAllRewards(string tournamentId)
    {
        try
        {
            Debug.Log($"Claiming all rewards for tournament ID: {tournamentId}");
            var notClaimedRewards = await _beamContext.Api.Tournaments.GetUnclaimedRewards(tournamentId);
            Debug.Log($"Not claimed rewards count: {notClaimedRewards.rewardCurrencies.Count}");
            if (notClaimedRewards.rewardCurrencies.Count > 0)
            {
                await _beamContext.Api.Tournaments.ClaimAllRewards(tournamentId);
                Debug.Log("Reward claimed successfully.");
            }
            else
            {
                Debug.Log("No rewards pending for this tournament.");
            }
        }
        catch (PlatformRequesterException ex)
        {
            if (ex.Error.error == "NoClaimsPending")
            {
                Debug.Log("No rewards pending for this tournament.");
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

    public async void OnClaimRewardsButtonPressed()
    {
        if (string.IsNullOrEmpty(_tournamentId))
        {
            Debug.LogWarning("No tournament ID available, cannot claim rewards.");
            return;
        }

        Debug.Log("Claim rewards button pressed.");
        await ClaimAllRewards(_tournamentId);
    }
    
    private async Task DisplayGroupScore(string tournamentId)
    {
        Debug.Log("Displaying group score...");

        // Construct the leaderboard ID for the group scores
        var groupLeaderboardId = await DiscoverLeaderboardId(tournamentId);

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
            var customLeaderboardTitle = ConstructGroupLeaderboardId(tournamentId);
            var customLeaderboard = await _beamContext.Api.LeaderboardService.GetBoard(customLeaderboardTitle, 1, 1000);
            groupScore = customLeaderboard.rankings.Sum(r => (int)r.score);
            Debug.Log($"Group not found, summing all scores: {groupScore}");
        }

        // Update the text on the UI
        groupScoreText.text = $"Event Group Score: {groupScore}";
        Debug.Log($"Group score displayed: {groupScore}");
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
    
}
