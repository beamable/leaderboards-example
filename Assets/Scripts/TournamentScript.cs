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

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;
    [SerializeField] private TMP_Text groupNameText;

    private async void Start()
    {
        if (!await InitializeContext()) return;

        // Load group ID and display group name
        if (long.TryParse(PlayerPrefs.GetString("SelectedGroupId", string.Empty), out var groupId))
        {
            await DisplayGroupName(groupId);
        }

        // Get and join the active tournament
        var activeTournament = await GetActiveOrUpcomingTournament();
        if (activeTournament == null) return;

        _tournamentId = activeTournament.tournamentId;
        await JoinTournament(_tournamentId);

        // Construct leaderboard ID and ensure score
        var leaderboardId = await ConstructGroupLeaderboardId(_tournamentId);
        if (string.IsNullOrEmpty(leaderboardId)) return;

        await EnsureTournamentScore(leaderboardId);
        await DisplayLeaderboard(leaderboardId);
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
            Debug.LogError($"Error initializing BeamContext: {e.Message}");
            return false;
        }
    }

    private async Task EnsureTournamentScore(string leaderboardId)
    {
        if (await LeaderboardExists(leaderboardId))
        {
            await EnsurePlayerScoreOnLeaderboard(leaderboardId);
        }
        else
        {
            await UpdateTournamentStats();
            var points = await GetTournamentPoints();
            await CreateAndPopulateLeaderboard(leaderboardId, points);
        }
    }

    private async Task UpdateTournamentStats()
    {
        var points = await GetTournamentPoints();
        await _service.SetStats("TOURNAMENTS_POINTS", points.ToString());
    }

    private async Task<int> GetTournamentPoints()
    {
        var stats = await _beamContext.Api.StatsService.GetStats("client", "public", "player", _beamContext.PlayerId);
        return stats.TryGetValue("TOURNAMENT_POINTS", out var points) ? int.Parse(points) : GenerateRandomScore();
    }

    private async Task<List<TournamentInfo>> GetTournaments()
    {
        var response = await _beamContext.Api.TournamentsService.GetAllTournaments();
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
        catch
        {
            return false;
        }
    }

    private async Task EnsurePlayerScoreOnLeaderboard(string leaderboardId)
    {
        var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        if (view.rankings.All(rankEntry => rankEntry.gt != _beamContext.PlayerId))
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
        catch (Exception e)
        {
            Debug.LogError($"Error fetching leaderboard: {e.Message}");
        }
    }

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private static async Task<string> ConstructGroupLeaderboardId(string tournamentId)
    {
        var groupId = PlayerPrefs.GetString("SelectedGroupId");
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

        return validTournaments.OrderByDescending(t => DateTime.Parse(t.endTimeUtc)).FirstOrDefault();
    }
    private async Task ClaimAllRewards(string tournamentId)
    {
        try
        {
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
        if (string.IsNullOrEmpty(_tournamentId)) return;
        await ClaimAllRewards(_tournamentId);
    }
}
