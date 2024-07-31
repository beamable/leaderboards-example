using System;
using Beamable.Common;
using Beamable.Common.Models;
using Beamable.Common.Utils;
using Beamable.Mongo;
using Beamable.Server;
using UnityEngine;

namespace Beamable.Microservices.UserService
{
	[Microservice("UserService")]
	public class UserService : Microservice
	{
		
		[ClientCallable]
		public async Promise<Response<string>> GetPlayerAvatarName(long gamerTag)
		{
			try
			{
				Debug.Log(gamerTag.ToString());
				var playerData = await Storage.GetByFieldName<PlayerData, long>("gamerTag", gamerTag);
				if (playerData != null && !string.IsNullOrEmpty(playerData.avatarName))
				{
					return new Response<string>(playerData.avatarName);
				}
				else
				{
					return new Response<string>("Avatar name not found");
				}
			}
			catch (Exception e)
			{
				BeamableLogger.LogError(e);
				return new Response<string>(e.Message);
			}
		}
		
		[ClientCallable]
		public async Promise<Response<bool>> SetPlayerAvatarName(long gamerTag, string avatarName)
		{
			try
			{
				// Check if the avatar name already exists
				var duplicateAvatarNameData = await Storage.GetByFieldName<PlayerData, string>("avatarName", avatarName);
				if (duplicateAvatarNameData != null)
				{
					return new Response<bool>(false, "Avatar name already exists");
				}

				// Check if the player with the given gamerTag exists
				var existingPlayerData = await Storage.GetByFieldName<PlayerData, long>("gamerTag", gamerTag);
				if (existingPlayerData == null)
				{
					// Create a new player data record
					await Storage.Create<UserGroupData, PlayerData>(new PlayerData
					{
						gamerTag = gamerTag,
						avatarName = avatarName
					});
				}
				else
				{
					// Update the existing player data with the new avatar name
					existingPlayerData.avatarName = avatarName;
					await Storage.Update(existingPlayerData.Id, existingPlayerData);
				}

				return new Response<bool>(true);
			}
			catch (Exception e)
			{
				BeamableLogger.LogError(e);
				return new Response<bool>(false, "Error setting avatar name");
			}
		}

		
		[ClientCallable]
		public string Test(long lala, string lalala)
		{
			Debug.Log("Testt");
			return "test";

		}
	}
}
