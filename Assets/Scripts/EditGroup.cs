using System;
using System.Threading.Tasks;
using Beamable;
using Beamable.Common.Api.Groups;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EditGroup : MonoBehaviour
{
    private PlayerGroupManager _groupManager;
    private Group _group;
    
    private string _groupIdString;

    [SerializeField]
    private TMP_Text groupNameText;
    [SerializeField]
    private TMP_InputField groupNameInput;
    [SerializeField]
    private TMP_InputField groupSloganInput;
    [SerializeField]
    private TMP_InputField groupMotdInput;
    [SerializeField]
    private TMP_Dropdown groupTypeDropdown;
    [SerializeField]
    private TMP_Text resultText;

    private async void Start()
    {
        var beamContext = await BeamContext.Default.Instance;
        _groupManager = new PlayerGroupManager(beamContext);
        await _groupManager.Initialize();

        _groupIdString = PlayerPrefs.GetString("SelectedGroupId", string.Empty);
        if (!string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out var groupId))
        {
            _group = await _groupManager.GetGroup(groupId);
            if (_group != null)
            {
                groupNameText.text = _group.name;
                groupNameInput.text = _group.name;
                groupSloganInput.text = _group.slogan;
                groupMotdInput.text = _group.motd;
                switch (_group.enrollmentType.ToLower())
                {
                    case "open":
                        groupTypeDropdown.value = 0;
                        break;
                    case "restricted":
                        groupTypeDropdown.value = 1;
                        break;
                    case "closed":
                        groupTypeDropdown.value = 2;
                        break;
                    default:
                        Debug.LogWarning($"Unknown enrollment type: {_group.enrollmentType}");
                        break;
                }
            }
        }
    }

    public void GoBack()
    {
        SceneManager.LoadScene("GroupDetails");
    }

    public async void SaveGroup()
    {
        if (!string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out var groupId))
        {
            var groupName = groupNameInput.text;
            var groupSlogan = groupSloganInput.text;
            var groupMotd = groupMotdInput.text;
            var groupType = groupTypeDropdown.options[groupTypeDropdown.value].text.ToLower();

            var props = new GroupUpdateProperties
            {
                name = groupName,
                slogan = groupSlogan,
                motd = groupMotd,
                enrollmentType = groupType
            };

            var result = await _groupManager.UpdateGroupProperties(groupId, props);
            resultText.text = result ? "Group updated successfully" : "Error updating group";
        }
    }

    public async void DisbandGroupTrigger()
    {
        if (!string.IsNullOrEmpty(_groupIdString) && long.TryParse(_groupIdString, out var groupId))
        {
            var result = await _groupManager.DisbandGroup(groupId);
            if (result)
            {
                SceneManager.LoadScene("CreateGroup");
            }
        }
    }
}
