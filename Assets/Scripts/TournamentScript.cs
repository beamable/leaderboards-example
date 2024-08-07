using System;
using System.Collections.Generic;
using Beamable;
using Beamable.Common.Tournaments;
using Beamable.Server.Clients;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using Beamable.Api;
using Beamable.Common.Api.Tournaments;
using Extensions;

public class TournamentScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    private UserServiceClient _userService;
    
    private long _userId;

    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        // Initialize BeamContext and get the User ID
        _beamContext = BeamContext.Default;
        await _beamContext.OnReady;
        _userId = _beamContext.PlayerId;

        // Initialize services
        _service = new BackendServiceClient();
        _userService = new UserServiceClient();

        // Get tournaments and join a tournament
        var tournaments = await GetTournaments();
        
        // Join the latest one if no specific tournament is found
        var tournamentToJoin = tournaments.Count > 0 ? tournaments[0] : null;

        if (tournamentToJoin != null)
        {
            await JoinTournament(tournamentToJoin.tournamentId);

            // Check if leaderboard exists and register score if necessary
            var leaderboardId = tournamentToJoin.tournamentId;
            if (!await LeaderboardExists(leaderboardId))
            {
                await CreateAndPopulateLeaderboard(leaderboardId);
            }

            await EnsurePlayerScoreOnLeaderboard(leaderboardId);
            await DisplayLeaderboard(leaderboardId);
        }
        else
        {
            Debug.LogError("No suitable tournament found to join.");
        }
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
        catch (PlatformRequesterException)
        {
            return false;
        }
    }

    private async Task CreateAndPopulateLeaderboard(string leaderboardId)
    {
        await _service.SetGroupLeaderboard(leaderboardId);
        Debug.Log($"leaderboard id: {leaderboardId}");
        await _service.SetLeaderboardScore(leaderboardId, GenerateRandomScore());
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
}
