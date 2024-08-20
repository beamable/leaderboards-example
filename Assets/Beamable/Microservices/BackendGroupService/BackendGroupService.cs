using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.Common.Models;
using Beamable.Common.Utils;
using Beamable.Mongo;
using Beamable.Server;

namespace Beamable.Microservices
{
    [Microservice("BackendGroupService")]
    public class BackendGroupService : Microservice
    {
        [ClientCallable]
        public async Promise<Response<List<long>>> GetBannedGroups(string leaderboardName)
        {
            try
            {
                var leaderboardData  = await Storage.GetByFieldName<LeaderboardData, string>("leaderboardName", leaderboardName);
                return leaderboardData == null ? new Response<List<long>>(null, "leaderboard not found") : new Response<List<long>>(leaderboardData.bannedGroupIds);
            }
            catch (Exception e)
            {
                BeamableLogger.LogError(e);
                return new Response<List<long>>(null, "Error retrieving group members");
            }
        }

        [ClientCallable]
        public async Promise<Response<bool>> BanGroup(string leaderboardName, long targetGroupId)
        {
            try
            {
                var leaderboardData = await Storage.GetByFieldName<LeaderboardData, string>("leaderboardName", leaderboardName);
        
                if (leaderboardData == null)
                {
                    leaderboardData = new LeaderboardData
                    {
                        leaderboardName = leaderboardName,
                        bannedGroupIds = new List<long> { targetGroupId }
                    };

                    await Storage.Create<UserGroupData, LeaderboardData>(leaderboardData);
                }
                else
                {
                    if (!leaderboardData.bannedGroupIds.Contains(targetGroupId))
                    {
                        leaderboardData.bannedGroupIds.Add(targetGroupId);
                        await Storage.Update(leaderboardData.Id, leaderboardData);
                    }
                }

                return new Response<bool>(true);
            }
            catch (Exception e)
            {
                BeamableLogger.LogError(e);
                return new Response<bool>(false, "Error banning group");
            }
        }


        [ClientCallable]
        public async Promise<Response<bool>> UnbanGroup(string leaderboardName, long targetGroupId)
        {
            try
            {
                // Fetch the leaderboard data
                var leaderboardData = await Storage.GetByFieldName<LeaderboardData, string>("leaderboardName", leaderboardName);
        
                // Check if the leaderboard exists and the group is in the banned list
                if (leaderboardData == null || !leaderboardData.bannedGroupIds.Contains(targetGroupId))
                {
                    return new Response<bool>(false, "Group not found in the banned list");
                }

                // Remove the group from the banned list
                leaderboardData.bannedGroupIds.Remove(targetGroupId);

                // If no more groups are banned, remove the leaderboard from storage
                if (leaderboardData.bannedGroupIds.Count == 0)
                {
                    await Storage.Delete<LeaderboardData>(leaderboardData.Id);
                }
                else
                {
                    // Update the leaderboard data if there are still banned groups
                    await Storage.Update(leaderboardData.Id, leaderboardData);
                }

                return new Response<bool>(true);
            }
            catch (Exception e)
            {
                BeamableLogger.LogError(e);
                return new Response<bool>(false, "Error unbanning group");
            }
        }

        
        [ClientCallable]
        public async Promise<Response<List<LeaderboardData>>> GetAllLeaderboardsWithBannedGroups()
        {
            try
            {
                var allLeaderboards = await Storage.GetAll<LeaderboardData>();
                return new Response<List<LeaderboardData>>(allLeaderboards);
            }
            catch (Exception e)
            {
                BeamableLogger.LogError(e);
                return new Response<List<LeaderboardData>>(null, "Error retrieving leaderboards");
            }
        }
    }
}
