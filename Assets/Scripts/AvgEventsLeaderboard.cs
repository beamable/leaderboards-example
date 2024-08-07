using System.Collections;
using System.Collections.Generic;
using Beamable;
using Beamable.Common.Api.Events;
using Beamable.Server.Clients;
using UnityEngine;

public class AvgEventsLeaderboard : MonoBehaviour
{
    private BeamContext _beamContext;
    private BackendServiceClient _service;
    
    [SerializeField] private GameObject rankingItemPrefab;
    [SerializeField] private Transform scrollViewContent;
    
    // Start is called before the first frame update
    private async void Start()
    {
        _beamContext = await BeamContext.Default.Instance;

        _service = new BackendServiceClient();
        
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
    }
    
    private static bool HasRunningEvents(EventsGetResponse eventsGetResponse)
    {
        return eventsGetResponse?.running != null && eventsGetResponse.running.Count > 0;
    }
}
