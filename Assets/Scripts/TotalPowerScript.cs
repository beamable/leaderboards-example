using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Api;
using Beamable.Server.Clients;
using Managers;
using UnityEngine;
using TMPro;

public class TotalPowerScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private PlayerGroupManager _groupManager;
    private BackendServiceClient _service;
    private UserServiceClient _userService;
    private string _groupIdString;

    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private TMP_Text groupPowerScoreText; // Add this serialized field
    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        await InitializeContext();

        if (!TryGetGroupId(out var groupId)) return;
        await DisplayGroupName(groupId);
        await HandleTotalPowerLeaderboard(groupId);
    }

    private async Task InitializeContext()
    {
        _beamContext = await BeamContext.Default.Instance;
        _service = new BackendServiceClient();
        _userService = new UserServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);
        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
    }

    private bool TryGetGroupId(out long groupId)
    {
        groupId = 0;
        return !string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out groupId);
    }

    private async Task HandleTotalPowerLeaderboard(long groupId)
    {
        var totalPower = await CalculateTotalPower();
        var leaderboardId = ConstructLeaderboardId(groupId);

        if (!await LeaderboardExists(leaderboardId))
        {
            await CreateLeaderboard(leaderboardId);
        }

        await SubmitScoreToLeaderboard(leaderboardId, totalPower);
        await DisplayLeaderboard(leaderboardId);
    }

    private async Task<int> CalculateTotalPower()
    {
        var inventory = _beamContext.Inventory.GetItems();
        await inventory.Refresh();

        var totalPower = inventory.Sum(item => int.Parse(item.Properties["amount"]));

        Debug.Log($"Total power calculated: {totalPower}");
        return totalPower;
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

    private async Task CreateLeaderboard(string leaderboardId)
    {
        // Create leaderboard for the group
        await _service.SetGroupLeaderboard(leaderboardId);
        Debug.Log($"Leaderboard {leaderboardId} created.");
    }

    private async Task SubmitScoreToLeaderboard(string leaderboardId, int totalPower)
    {
        var leaderboard = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000);
        var rankings = leaderboard.rankings;

        // Check if player's score already exists
        var playerEntry = rankings.FirstOrDefault(rank => rank.gt == _beamContext.PlayerId);

        if (playerEntry == null)
        {
            // Player's score does not exist, submit the score
            await _service.SetLeaderboardScore(leaderboardId, totalPower);
            Debug.Log($"Submitted score {totalPower} to leaderboard {leaderboardId}");
        }
        else
        {
            // Player's score exists, compare it with the current total power
            if (playerEntry.score != totalPower)
            {
                // Update the leaderboard score if it differs from the total power
                await _service.SetLeaderboardScore(leaderboardId, totalPower);
                Debug.Log($"Updated player's score to {totalPower} on leaderboard {leaderboardId}");
            }
            else
            {
                Debug.Log($"Player's score ({playerEntry.score}) matches the total power ({totalPower}), no need to update.");
            }
        }
    }

    private async Task DisplayLeaderboard(string leaderboardId)
    {
        try
        {
            var rankings = (await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 1000)).rankings;
            ClearScrollViewContent();

            double groupTotalPower = 0; // Variable to accumulate total power of all players

            foreach (var rankEntry in rankings)
            {
                var username = await GetPlayerUsername(rankEntry.gt);
                CreateRankingItem(username, rankEntry.score.ToString());

                // Sum up the group total power
                groupTotalPower += rankEntry.score;
            }

            // Update the group power score text with the summed-up scores
            groupPowerScoreText.text = $"Group Power: {groupTotalPower}";

            Debug.Log($"Total group power: {groupTotalPower}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching leaderboard: {e.Message}");
        }
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

    private void ClearScrollViewContent()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private async Task DisplayGroupName(long groupId)
    {
        try
        {
            var group = await _groupManager.GetGroup(groupId);
            groupNameText.text = group?.name;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching group details: {e.Message}");
        }
    }

    private string ConstructLeaderboardId(long groupId)
    {
        return $"totalpower_group_{groupId}";
    }
}
