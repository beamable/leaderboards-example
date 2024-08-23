using System;
using Beamable.Common.Interfaces;
using Beamable.Server;
using MongoDB.Bson.Serialization.Attributes;

namespace Beamable.Common.Models
{
    [Serializable]
    public class PlayerData : StorageDocument, ISetStorageDocument<PlayerData>
    {
        public long gamerTag;
        public string avatarName;

        public void Set(PlayerData document)
        {
            gamerTag = document.gamerTag;
            avatarName = document.avatarName;
        }
    }
}