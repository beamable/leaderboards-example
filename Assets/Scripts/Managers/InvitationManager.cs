using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Beamable;
using Beamable.Common.Api.Notifications;
using Beamable.Common.Models;
using Beamable.Serialization.SmallerJSON;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers
{
    public class InvitationManager : MonoBehaviour
    {
        private BeamContext _beamContext;
        private PlayerGroupManager _groupManager;
        private bool isSubscribed = false;

        [SerializeField]
        private GameObject invitePopup;

        private TMP_Text _inviteMessage;
        private Button _acceptButton;
        private Button _declineButton;

        private async void Start()
        {
            InitializePopupComponents();
            await SetupInvitationListener();

            _groupManager = new PlayerGroupManager(_beamContext);
            await _groupManager.Initialize();
        }

        private void InitializePopupComponents()
        {
            _inviteMessage = invitePopup.GetComponentInChildren<TMP_Text>();
            var buttons = invitePopup.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                if (button.name == "AcceptButton")
                {
                    _acceptButton = button;
                }
                else if (button.name == "DeclineButton")
                {
                    _declineButton = button;
                }
            }

            if (_inviteMessage == null || _acceptButton == null || _declineButton == null)
            {
                Debug.LogError("Failed to find all components in invitePopup.");
            }
        }

        private async Task SetupInvitationListener()
        {
            _beamContext = await BeamContext.Default.Instance;
            _beamContext.Api.NotificationService.Subscribe("GroupInvite", HandleInvitation);
            isSubscribed = true;
            Debug.Log("Subscribed to GroupInvite notifications.");
        }

        private async void HandleInvitation(object payload)
        {
            try
            {
                // Try to parse the payload as a JSON string
                if (payload is ArrayDict arrayDict)
                {
                    if (arrayDict.TryGetValue("stringValue", out var jsonString))
                    {
                        var inviteData = JsonUtility.FromJson<InviteData>(jsonString.ToString());

                        var group = await _groupManager.GetGroup(inviteData.groupId);
                        _inviteMessage.text = $"You've been invited to join {group.name}";
                        invitePopup.SetActive(true);

                        _acceptButton.onClick.RemoveAllListeners();
                        _declineButton.onClick.RemoveAllListeners();

                        _acceptButton.onClick.AddListener(() => AcceptInvite(inviteData.groupId));
                        _declineButton.onClick.AddListener(DeclineInvite);
                    }
                    else
                    {
                        Debug.LogError("No 'stringValue' found in payload");
                    }
                }
                else if (payload is Dictionary<string, object> dict && dict.Count == 0)
                {
                    Debug.LogWarning("Received an empty payload");
                }
                else
                {
                    Debug.LogError("Payload is not a valid JSON string.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to parse GroupInvite payload: {ex.Message}");
            }
        }

        private async void AcceptInvite(long groupId)
        {
            invitePopup.SetActive(false);
            await _groupManager.JoinGroup(groupId);
        }

        private void DeclineInvite()
        {
            invitePopup.SetActive(false);
        }

        private void OnDisable()
        {
            if (isSubscribed && _beamContext != null)
            {
                _beamContext.Api.NotificationService.Unsubscribe("GroupInvite", HandleInvitation);
                isSubscribed = false;
                Debug.Log("Unsubscribed from GroupInvite notifications.");
            }
        }

        private void OnDestroy()
        {
            if (isSubscribed && _beamContext != null)
            {
                _beamContext.Api.NotificationService.Unsubscribe("GroupInvite", HandleInvitation);
                isSubscribed = false;
                Debug.Log("Unsubscribed from GroupInvite notifications.");
            }
        }
    }
}
