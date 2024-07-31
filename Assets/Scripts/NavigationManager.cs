using UnityEngine;
using UnityEngine.SceneManagement;

public class NavigationManager : MonoBehaviour
{
    public void LoadCreateGroupScene()
    {
        SceneManager.LoadScene("CreateGroup");
    }

    public void LoadViewGroupsScene()
    {
        SceneManager.LoadScene("ViewGroups");
    }
}