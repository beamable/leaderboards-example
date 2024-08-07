using System;
using System.Collections;
using System.Threading.Tasks;
using Beamable;
using Beamable.Server.Clients;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GroupDetails : MonoBehaviour
{
    private BeamContext _beamContext;
    
    private UserServiceClient _userService;

    private string _groupIdString;
    private bool _isLeader;
    private PlayerGroupManager _groupManager;

    [SerializeField]
    private TMP_Text groupNameText;
    [SerializeField]
    private Button leaveGroupButton;
    [SerializeField]
    private Button eventLeaderboardButton;
    [SerializeField]
    private Button tournamentLeaderboardButton;
    [SerializeField]
    private GameObject memberItemPrefab;
    [SerializeField]
    private Transform groupMembersList;
    [SerializeField]
    private GameObject setLeaderPanel;
    [SerializeField] 
    private GameObject sendInvite;
    [SerializeField]
    private TMP_InputField inviteInput;


    private async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;

        _userService = new UserServiceClient();
        _groupManager = new PlayerGroupManager(_beamContext);
        await _groupManager.Initialize();
        
        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
        if (!string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out var groupId))
        {
            await DisplayGroupDetails(groupId);
        }
    }
    private async Task DisplayGroupDetails(long groupId)
    {
        try
        {
            var group = await _groupManager.GetGroup(groupId);
            if (group != null)
            {
                groupNameText.text = group.name;

                var playerInGroup = group.members.Exists(member => member.gamerTag == _beamContext.PlayerId);

                _isLeader = group.members.Exists(member => member.gamerTag == _beamContext.PlayerId && member.role == "leader");
                leaveGroupButton.gameObject.SetActive(playerInGroup);
                eventLeaderboardButton.gameObject.SetActive(playerInGroup);
                tournamentLeaderboardButton.gameObject.SetActive(playerInGroup);

                foreach (Transform child in groupMembersList)
                {
                    Destroy(child.gameObject);
                }

                if (group.members != null)
                {
                    foreach (var member in group.members)
                    {
                        var username = await _userService.GetPlayerAvatarName(member.gamerTag);
                        AddMemberItem(username.data, group.id, member.gamerTag);
                    }
                }
                
                sendInvite.gameObject.SetActive(_isLeader);
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

    private void AddMemberItem(string username, long groupId, long gamerTag)
    {
        var memberItem = Instantiate(memberItemPrefab, groupMembersList);
        var text = memberItem.GetComponentInChildren<TMP_Text>();
        text.text = username;

        var kickButton = memberItem.transform.Find("KickButton").GetComponent<Button>();
        kickButton.gameObject.SetActive(_isLeader && gamerTag != _beamContext.PlayerId);
        kickButton.onClick.AddListener(async () => await KickMember(groupId, gamerTag));

        var setLeaderButton = memberItem.transform.Find("SetLeader").GetComponent<Button>();
        setLeaderButton.gameObject.SetActive(_isLeader && gamerTag != _beamContext.PlayerId);
        setLeaderButton.onClick.AddListener(async () => await SetLeader(groupId, gamerTag));
    }

    private async Task KickMember(long groupId, long gamerTag)
    {
        if (await _groupManager.KickMember(groupId, gamerTag))
        {
            await DisplayGroupDetails(groupId); // Refresh the group details
        }
    }

    private async Task SetLeader(long groupId, long gamerTag)
    {
        if (await _groupManager.SetLeader(groupId, gamerTag))
        {
            await DisplayGroupDetails(groupId); // Refresh the group details
        }
    }

 
    
    public async void LeaveGroup()
    {
        if (string.IsNullOrEmpty(_groupIdString) || !long.TryParse(_groupIdString, out var groupId)) return;
        var group = await _groupManager.GetGroup(groupId);
        Debug.Log(_beamContext.PlayerId);
        if (group.members.Exists(member => member.gamerTag == _beamContext.PlayerId && member.role == "leader"))
        {
            setLeaderPanel.SetActive(true);
            StartCoroutine(HideSetLeaderPanel());
        }
        else
        {
            try
            {
                await _groupManager.LeaveGroup(groupId);
                Debug.Log("Left group successfully");
                SceneManager.LoadScene("CreateGroup"); // Navigate to a different scene after leaving the group
            }
            catch (Exception e)
            {
                Debug.LogError($"Error leaving group: {e.Message}");
            }
        }
    }

    private IEnumerator HideSetLeaderPanel()
    {
        yield return new WaitForSeconds(5);
        setLeaderPanel.SetActive(false);
    }
    
    public async void SendInvitation()
    {
        var inviteeName = inviteInput.text;
        if (string.IsNullOrEmpty(inviteeName)) return;

        try
        {
            var groupId = long.Parse(_groupIdString);
            var service = new BackendServiceClient();
            await service.SendInvitation(inviteeName, groupId);
            inviteInput.text = "";
            Debug.Log("Invitation sent successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending invitation: {e.Message}");
        }
    }
}
