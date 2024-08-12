using System;
using System.Threading.Tasks;
using Beamable;
using Managers;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public class CreateGroups : MonoBehaviour
{
    private PlayerGroupManager _player;

    [SerializeField]
    private TMP_InputField usernameInput;
    [SerializeField]
    private TMP_Dropdown groupTypeDropdown;
    [SerializeField]
    private TMP_InputField groupNameInput;
    [SerializeField]
    private TMP_InputField maxMembersInput;
    [SerializeField]
    private Button createGroupButton;
    [SerializeField]
    private TMP_Text infoText;

    protected async void Start()
    {
        await SetupBeamable();
        SetupUIListeners();

        createGroupButton.interactable = false;
    }

    private async Task SetupBeamable()
    {
        var beamContext = await BeamContext.Default.Instance;
        _player = new PlayerGroupManager(beamContext);
        await _player.Initialize();
    }

    private void SetupUIListeners()
    {
        groupNameInput.onValueChanged.AddListener(CheckFields);
        maxMembersInput.onValueChanged.AddListener(CheckFields);
    }

    public string GetDropdownValue()
    {
        var dropdownValue = groupTypeDropdown.value;
        var selectedOption = groupTypeDropdown.options[dropdownValue].text;
        return selectedOption;
    }

    private string GenerateTag(string groupName)
    {
        var words = groupName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tag = "";

        for (int i = 0; i < Math.Min(words.Length, 3); i++)
        {
            tag += words[i][0];
        }

        // If the tag is less than 3 characters, pad it with the first characters of the group name
        while (tag.Length < 3 && groupName.Length > 0)
        {
            tag += groupName[0];
            groupName = groupName.Substring(1);
        }

        return tag.ToUpper();
    }

    public async void CreateGroup()
    {
        var generatedTag = GenerateTag(groupNameInput.text);
        var type = GetDropdownValue();
         
        var response = await _player.CreateGroup(groupNameInput.text, null, type, 0,
            int.Parse(maxMembersInput.text), usernameInput.text);

        if (!string.IsNullOrEmpty(response.errorMessage))
        {
            infoText.text = "Error: " + response.errorMessage;
        }
        else
        {
            infoText.text = "Group created successful";
        }
    }

    private void CheckFields(string value)
    {
        var allFieldsCompleted = !string.IsNullOrEmpty(groupNameInput.text) &&
                                 !string.IsNullOrEmpty(maxMembersInput.text);

        createGroupButton.interactable = allFieldsCompleted;
    }
}
