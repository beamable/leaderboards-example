using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Common.Api.Groups;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class ViewGroups : MonoBehaviour
{
    private BeamContext _beamContext;

    [SerializeField]
    private TMP_InputField searchInput;
    [SerializeField]
    private Button groupButtonPrefab;
    [SerializeField]
    private Transform groupsListContent;

    private GroupsView _groupsView = null;
    private List<Group> _allGroups = new List<Group>();
    private bool isDisplayingGroups = false;

    protected async void Start()
    {
        await SetupBeamable();
        searchInput.onValueChanged.AddListener(OnSearchValueChanged);
    }

    private async Task SetupBeamable()
    {
        _beamContext = await BeamContext.Default.Instance;

        _beamContext.Api.GroupsService.Subscribe(async groupsView =>
        {
            _groupsView = groupsView;
            await FetchAndStoreGroups();
            DisplayGroups(_allGroups);
        });
    }

    private async Task FetchAndStoreGroups()
    {
        try
        {
            var groupSearchResponse = await _beamContext.Api.GroupsService.Search("", new List<string> { "open", "closed" });
            _allGroups = groupSearchResponse.groups;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching groups: {e.Message}");
        }
    }

    private void DisplayGroups(List<Group> groups)
    {
        if (isDisplayingGroups)
            return;

        StartCoroutine(DisplayGroupsCoroutine(groups));
    }

    private IEnumerator DisplayGroupsCoroutine(List<Group> groups)
    {
        isDisplayingGroups = true;

        // Clear previous group listings
        foreach (Transform child in groupsListContent)
        {
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        // Wait for the end of the frame to ensure all child objects are destroyed
        yield return new WaitForEndOfFrame();

        if (this == null)
        {
            // The object has been destroyed, exit the coroutine
            yield break;
        }

        var count = 1;
        foreach (var group in groups)
        {
            var button = Instantiate(groupButtonPrefab, groupsListContent);
            button.GetComponentInChildren<TextMeshProUGUI>().text = $"{count}. {group.name}";
            button.onClick.AddListener(() => OnGroupClick(group));
            count++;
        }

        isDisplayingGroups = false;
    }

    public void OnSearchValueChanged(string searchText)
    {
        var filteredGroups = _allGroups
            .Where(group => group.name.ToLower().Contains(searchText.Trim().ToLower()))
            .ToList();
        DisplayGroups(filteredGroups);
    }

    private void OnGroupClick(Group group)
    {
        PlayerPrefs.SetString("SelectedGroupId", group.id.ToString());
        SceneManager.LoadScene("GroupDetails");
    }
}
