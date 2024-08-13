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
    
    public void LoadEventsLeaderboardScene()
    {
        SceneManager.LoadScene("EventsLeaderboard");
    }
    
    public void LoadTournamentsLeaderboardScene()
    {
        SceneManager.LoadScene("TournamentsLeaderboard");
    }
    
    public void LoadAvgEventsLeaderboardScene()
    {
        SceneManager.LoadScene("AvgEventsLeaderboard");
    }
    
    public void LoadAvgTournamentsLeaderboardScene()
    {
        SceneManager.LoadScene("AvgTournamentsLeaderboard");
    }
}