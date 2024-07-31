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
    
    
    /// <summary> A generated client for <see cref="Beamable.Microservices.UserService.UserService"/> </summary
    public sealed class UserServiceClient : MicroserviceClient, Beamable.Common.IHaveServiceName
    {
        
        public UserServiceClient(BeamContext context = null) : 
                base(context)
        {
        }
        
        public string ServiceName
        {
            get
            {
                return "UserService";
            }
        }
        
        /// <summary>
        /// Call the GetPlayerAvatarName method on the UserService microservice
        /// <see cref="Beamable.Microservices.UserService.UserService.GetPlayerAvatarName"/>
        /// </summary>
        public Beamable.Common.Promise<Beamable.Common.Utils.Response<string>> GetPlayerAvatarName(long gamerTag)
        {
            object raw_gamerTag = gamerTag;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("gamerTag", raw_gamerTag);
            return this.Request<Beamable.Common.Utils.Response<string>>("UserService", "GetPlayerAvatarName", serializedFields);
        }
        
        /// <summary>
        /// Call the SetPlayerAvatarName method on the UserService microservice
        /// <see cref="Beamable.Microservices.UserService.UserService.SetPlayerAvatarName"/>
        /// </summary>
        public Beamable.Common.Promise<Beamable.Common.Utils.Response<bool>> SetPlayerAvatarName(long gamerTag, string avatarName)
        {
            object raw_gamerTag = gamerTag;
            object raw_avatarName = avatarName;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("gamerTag", raw_gamerTag);
            serializedFields.Add("avatarName", raw_avatarName);
            return this.Request<Beamable.Common.Utils.Response<bool>>("UserService", "SetPlayerAvatarName", serializedFields);
        }
        
        /// <summary>
        /// Call the Test method on the UserService microservice
        /// <see cref="Beamable.Microservices.UserService.UserService.Test"/>
        /// </summary>
        public Beamable.Common.Promise<string> Test(long lala, string lalala)
        {
            object raw_lala = lala;
            object raw_lalala = lalala;
            System.Collections.Generic.Dictionary<string, object> serializedFields = new System.Collections.Generic.Dictionary<string, object>();
            serializedFields.Add("lala", raw_lala);
            serializedFields.Add("lalala", raw_lalala);
            return this.Request<string>("UserService", "Test", serializedFields);
        }
    }
    
    internal sealed class MicroserviceParametersUserServiceClient
    {
        
        [System.SerializableAttribute()]
        internal sealed class ParameterSystem_Int64 : MicroserviceClientDataWrapper<long>
        {
        }
        
        [System.SerializableAttribute()]
        internal sealed class ParameterSystem_String : MicroserviceClientDataWrapper<string>
        {
        }
    }
    
    [BeamContextSystemAttribute()]
    public static class ExtensionsForUserServiceClient
    {
        
        [Beamable.Common.Dependencies.RegisterBeamableDependenciesAttribute()]
        public static void RegisterService(Beamable.Common.Dependencies.IDependencyBuilder builder)
        {
            builder.AddScoped<UserServiceClient>();
        }
        
        public static UserServiceClient UserService(this Beamable.Server.MicroserviceClients clients)
        {
            return clients.GetClient<UserServiceClient>();
        }
    }
}
