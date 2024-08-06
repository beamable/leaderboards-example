using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beamable.Common.Models;
using Beamable.Server;
using Beamable.Server.Api.Leaderboards;
using UnityEngine;

namespace Beamable.Microservices
{
    [Microservice("BackendService")]
    public class BackendService : Microservice
    {
        private async Task SendNotification(List<long> playerIds, string context, object payload)
        {
            Debug.Log("payload " + payload);
            var jsonPayload = JsonUtility.ToJson(payload);
            Debug.Log("Json payload: " + jsonPayload);
            await Services.Notifications.NotifyPlayer(playerIds, context, jsonPayload);
        }
        
        [ClientCallable]
        public async Task SendInvitation(string invitee, long groupId)
        {
            var inviteeProfile = await Storage.GetByFieldName<PlayerData, string>("avatarName", invitee);
            if (inviteeProfile == null)
                throw new System.Exception("Invitee not found");

            var inviteeGamerTag = inviteeProfile.gamerTag;

            var invitation = new InviteData
            {
                groupId = groupId
            };

            await SendNotification(new List<long> { inviteeGamerTag }, "GroupInvite", invitation);
        }
        
        [ClientCallable]
        public async Task SetScore(string eventId, double score)
        {
            try
            {
                await Services.Events.SetScore(eventId, score);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                throw;
            }
        }
        
        [ClientCallable]
        public async Task SetGroupLeaderboard(string eventId)
        {
            try
            {
                await Services.Leaderboards.CreateLeaderboard(eventId, new CreateLeaderboardRequest());
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                throw;
            }
        }
    }
}