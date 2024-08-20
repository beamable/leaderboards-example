using System;
using System.Collections.Generic;
using Beamable.Common.Interfaces;
using Beamable.Server;

namespace Beamable.Common.Models
{
    [Serializable]
    public class LeaderboardData : StorageDocument, ISetStorageDocument<LeaderboardData>
    {
        public string leaderboardName;
        public List<long> bannedGroupIds = new List<long>(); // List of banned groups

        public void Set(LeaderboardData document)
        {
            leaderboardName = document.leaderboardName;
            bannedGroupIds = document.bannedGroupIds;
        }
    }
}