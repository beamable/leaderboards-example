using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beamable;
using Beamable.Server.Clients;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers
{
    public class BannedGroupsManager : MonoBehaviour
    {
        private BeamContext _beamContext;
        private BackendGroupServiceClient _service;
    
        [SerializeField] private GameObject leaderboardPanelPrefab; 
        [SerializeField] private GameObject bannedGroupItemPrefab; 
        [SerializeField] private Transform scrollViewContent; 

        private async void Start()
        {
            _beamContext = await BeamContext.Default.Instance;

            _service = new BackendGroupServiceClient();
        
            await DisplayAllLeaderboardsWithBannedGroups();
        }

        private async Task DisplayAllLeaderboardsWithBannedGroups()
        {
            try
            {
                // Clear the current content of the ScrollView before updating
                ClearScrollViewContent();

                // Fetch all leaderboards with their banned groups
                var response = await _service.GetAllLeaderboardsWithBannedGroups();

                if (response.data != null)
                {
                    foreach (var leaderboardData in response.data)
                    {
                        CreateLeaderboardPanel(leaderboardData.leaderboardName, leaderboardData.bannedGroupIds);
                    }
                }
                else
                {
                    Debug.LogWarning("No leaderboards found or an error occurred.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error fetching leaderboards: {e.Message}");
            }
        }

        private void CreateLeaderboardPanel(string leaderboardName, List<long> bannedGroups)
        {
            // Instantiate the leaderboard panel prefab
            var leaderboardPanel = Instantiate(leaderboardPanelPrefab, scrollViewContent);
            var leaderboardNameText = leaderboardPanel.transform.Find("LeaderboardName").GetComponent<TextMeshProUGUI>();
            leaderboardNameText.text = leaderboardName;

            // Populate the leaderboard panel with banned group items
            foreach (var groupId in bannedGroups)
            {
                CreateBannedGroupItem(leaderboardPanel.transform, leaderboardName, groupId);
            }
        }

        private async void CreateBannedGroupItem(Transform parent, string leaderboardName, long groupId)
        {
            // Instantiate the banned group item prefab
            var bannedGroupItem = Instantiate(bannedGroupItemPrefab, parent.Find("BannedGroupPrefab"));
            var groupNameText = bannedGroupItem.transform.Find("GroupName").GetComponent<TextMeshProUGUI>();

            // Set the group name 
            var groupName = await GetGroupName(groupId);
            groupNameText.text = $"{groupName}";

            // Get the unban button and set up its click event
            var unbanButton = bannedGroupItem.transform.Find("UnbanButton").GetComponent<Button>();
            unbanButton.onClick.AddListener(() => UnbanGroup(leaderboardName, groupId));
        }

        private async void UnbanGroup(string leaderboardName, long groupId)
        {
            try
            {
                await _service.UnbanGroup(leaderboardName, groupId);
                Debug.Log($"Group {groupId} unbanned from leaderboard {leaderboardName}");
                await DisplayAllLeaderboardsWithBannedGroups(); // Refresh the list after unbanning
            }
            catch (Exception e)
            {
                Debug.LogError($"Error unbanning group: {e.Message}");
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

        private void ClearScrollViewContent()
        {
            // Clear all children of the ScrollView content
            foreach (Transform child in scrollViewContent)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
