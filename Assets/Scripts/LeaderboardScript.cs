using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beamable;
using Beamable.Common.Api.Events;
using Beamable.Common.Leaderboards;
using Beamable.Server.Clients;
using Extensions;
using UnityEngine;
using TMPro;

public class LeaderboardScript : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;

    [SerializeField] private GameObject rankingItemPrefab; // Prefab for displaying each ranking
    [SerializeField] private Transform scrollViewContent; // Content GameObject of the ScrollView

    async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;
        await _beamContext.OnReady;

        _service = new BackendServiceClient();
        
        _beamContext.Api.EventsService.Subscribe(OnEventUpdate);
    }

    private async void OnEventUpdate(EventsGetResponse eventsGetResponse)
    {
        var eventView = eventsGetResponse.running[0];
        if (eventView != null)
        {
            var eventId = eventView.id;
            Debug.Log("Event id " + eventId);

            // Generate a random score between 0 and 1000
            double randomScore = GenerateRandomScore();
            await _service.SetScore(eventId, randomScore);

            var leaderboardId = eventView.leaderboardId;
            Debug.Log("Leaderboard id " + leaderboardId);
            if (!string.IsNullOrEmpty(leaderboardId))
            {
                Debug.Log("Leaderboard id " + leaderboardId);
                var view = await _beamContext.Api.LeaderboardService.GetBoard(leaderboardId, 1, 6);
                var rankings = view.rankings;
                foreach (Transform child in scrollViewContent)
                {
                    Destroy(child.gameObject); // Clear existing items
                }

                foreach (var (rankEntry, index) in rankings.WithIndex())
                {
                    Debug.Log($"Gamer Tag: {rankEntry.gt}, Score: {rankEntry.score}");
                    CreateRankingItem(rankEntry.gt.ToString(), rankEntry.score.ToString());
                }
            }
        }
        else
        {
            Debug.LogError($"Event with ID is not running.");
        }
    }

    private void CreateRankingItem(string gamerTag, string score)
    {
        GameObject rankingItem = Instantiate(rankingItemPrefab, scrollViewContent);
        TextMeshProUGUI[] texts = rankingItem.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length < 2)
        {
            Debug.LogError("RankingItemPrefab must have at least two TextMeshProUGUI components for GamerTag and Score.");
            return;
        }

        foreach (var text in texts)
        {
            if (text.name == "GamerTag")
            {
                text.text = $"{gamerTag}";
            }
            else if (text.name == "Score")
            {
                text.text = $"{score}";
            }
        }
    }

    private double GenerateRandomScore()
    {
        System.Random random = new System.Random();
        return random.Next(0, 1000);
    }
}
