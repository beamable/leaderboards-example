using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beamable;
using Beamable.Common.Api.Events;
using Beamable.Server.Clients;
using Managers;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Note: There is a known bug where the leaderboard only registers the first group that scores into the average score per group leaderboard.
// This means subsequent groups may not be registered correctly on the leaderboard.
// This issue needs to be addressed in future updates.

public class AvgEventsLeaderboard : MonoBehaviour
{
    private BeamContext _beamContext;
    private PlayerGroupManager _groupManager;
    private BackendGroupServiceClient _backendGroupService;


    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;

    private async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;
        
        _backendGroupService = new BackendGroupServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);
        await _groupManager.Initialize();
        
        _beamContext.Api.EventsService.Subscribe(OnEventUpdate);
    }

    private async void OnEventUpdate(EventsGetResponse eventsGetResponse)
    {
        if (!HasRunningEvents(eventsGetResponse))
        {
            Debug.LogError("No running events found.");
            return;
        }

        var eventView = eventsGetResponse.running[0];
        if (eventView == null)
        {
            Debug.LogError("Event with ID is not running.");
            return;
        }

        var leaderboardId = $"event_{eventView.id}_groups";
        await DisplayGroupLeaderboard(leaderboardId);
    }

    private static bool HasRunningEvents(EventsGetResponse eventsGetResponse)
    {
        return eventsGetResponse?.running != null && eventsGetResponse.running.Count > 0;
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

            foreach (var rankEntry in rankings)
            {
                if (!bannedGroups.Contains(rankEntry.gt))
                {
                    var groupName = await GetGroupName(rankEntry.gt);
                    CreateRankingItem(groupName, rankEntry.score.ToString(), rankEntry.gt, leaderboardId);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching leaderboard: {e.Message}");
        }
    }


    private async Task<string> GetGroupName(long groupId)
    {
        try
        {
            var group = await _beamContext.Api.GroupsService.GetGroup(groupId);
            if (group != null)
            {
                Debug.Log(group.name);
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
            if (text.name == "GroupName")
            {
                text.text = groupName;
            }
            else if (text.name == "Score")
            {
                text.text = score;
            }
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
