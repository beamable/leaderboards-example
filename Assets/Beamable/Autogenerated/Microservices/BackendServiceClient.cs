//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Beamable.Server.Clients
{
    using System;
    using Beamable.Platform.SDK;
    using Beamable.Server;
    
    
    /// <summary> A generated client for <see cref="Beamable.Microservices.BackendService"/> </summary
    public sealed class BackendServiceClient : MicroserviceClient, Beamable.Common.IHaveServiceName
    {
        
        public BackendServiceClient(BeamContext context = null) : 
                base(context)
        {
        }
        
        public string ServiceName
        {
            get
            {
                return "BackendService";
            }
        }
        
        /// <summary>
        /// Call the SendInvitation method on the BackendService microservice
        /// <see cref="Beamable.Microservices.BackendService.SendInvitation"/>
        /// </summary>
        public Beamable.Common.Promise<System.Threading.Tasks.Task> SendInvitation(string invitee, long groupId)
        {
            object raw_invitee = invitee;
            object raw_groupId = groupId;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("invitee", raw_invitee);
            serializedFields.Add("groupId", raw_groupId);
            return this.Request<System.Threading.Tasks.Task>("BackendService", "SendInvitation", serializedFields);
        }
        
        /// <summary>
        /// Call the SetEventScore method on the BackendService microservice
        /// <see cref="Beamable.Microservices.BackendService.SetEventScore"/>
        /// </summary>
        public Beamable.Common.Promise<System.Threading.Tasks.Task> SetEventScore(string eventId, double score, System.Collections.Generic.Dictionary<string, object> stats)
        {
            object raw_eventId = eventId;
            object raw_score = score;
            object raw_stats = stats;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("eventId", raw_eventId);
            serializedFields.Add("score", raw_score);
            serializedFields.Add("stats", raw_stats);
            return this.Request<System.Threading.Tasks.Task>("BackendService", "SetEventScore", serializedFields);
        }
        
        /// <summary>
        /// Call the SetLeaderboardScore method on the BackendService microservice
        /// <see cref="Beamable.Microservices.BackendService.SetLeaderboardScore"/>
        /// </summary>
        public Beamable.Common.Promise<System.Threading.Tasks.Task> SetLeaderboardScore(string leaderboardId, double score)
        {
            object raw_leaderboardId = leaderboardId;
            object raw_score = score;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("leaderboardId", raw_leaderboardId);
            serializedFields.Add("score", raw_score);
            return this.Request<System.Threading.Tasks.Task>("BackendService", "SetLeaderboardScore", serializedFields);
        }
        
        /// <summary>
        /// Call the SetGroupLeaderboard method on the BackendService microservice
        /// <see cref="Beamable.Microservices.BackendService.SetGroupLeaderboard"/>
        /// </summary>
        public Beamable.Common.Promise<System.Threading.Tasks.Task> SetGroupLeaderboard(string eventId)
        {
            object raw_eventId = eventId;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("eventId", raw_eventId);
            return this.Request<System.Threading.Tasks.Task>("BackendService", "SetGroupLeaderboard", serializedFields);
        }
        
        /// <summary>
        /// Call the SetStats method on the BackendService microservice
        /// <see cref="Beamable.Microservices.BackendService.SetStats"/>
        /// </summary>
        public Beamable.Common.Promise<System.Threading.Tasks.Task> SetStats(string statKey, string newValue)
        {
            object raw_statKey = statKey;
            object raw_newValue = newValue;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("statKey", raw_statKey);
            serializedFields.Add("newValue", raw_newValue);
            return this.Request<System.Threading.Tasks.Task>("BackendService", "SetStats", serializedFields);
        }
    }
    
    internal sealed class MicroserviceParametersBackendServiceClient
    {
        
        [System.SerializableAttribute()]
        internal sealed class ParameterSystem_String : MicroserviceClientDataWrapper<string>
        {
        }
        
        [System.SerializableAttribute()]
        internal sealed class ParameterSystem_Int64 : MicroserviceClientDataWrapper<long>
        {
        }
        
        [System.SerializableAttribute()]
        internal sealed class ParameterSystem_Double : MicroserviceClientDataWrapper<double>
        {
        }
        
        [System.SerializableAttribute()]
        internal sealed class ParameterSystem_Collections_Generic_Dictionary_System_String_System_Object : MicroserviceClientDataWrapper<System.Collections.Generic.Dictionary<string, object>>
        {
        }
    }
    
    [BeamContextSystemAttribute()]
    public static class ExtensionsForBackendServiceClient
    {
        
        [Beamable.Common.Dependencies.RegisterBeamableDependenciesAttribute()]
        public static void RegisterService(Beamable.Common.Dependencies.IDependencyBuilder builder)
        {
            builder.AddScoped<BackendServiceClient>();
        }
        
        public static BackendServiceClient BackendService(this Beamable.Server.MicroserviceClients clients)
        {
            return clients.GetClient<BackendServiceClient>();
        }
    }
}
