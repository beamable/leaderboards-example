using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Beamable.Common;
using Beamable.Common.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Beamable.Server
{
	[StorageObject("UserGroupData")]
	public class UserGroupData : MongoStorageObject
	{
	}

	public static class UserGroupDataExtension
	{
		/// <summary>
		/// Get an authenticated MongoDB instance for UserGroupData
		/// </summary>
		/// <returns></returns>
		public static Promise<IMongoDatabase> UserGroupDataDatabase(this IStorageObjectConnectionProvider provider)
			=> provider.GetDatabase<UserGroupData>();

		/// <summary>
		/// Gets a MongoDB collection from UserGroupData by the requested name, and uses the given mapping class.
		/// If you don't want to pass in a name, consider using <see cref="UserGroupDataCollection{TCollection}()"/>
		/// </summary>
		/// <param name="name">The name of the collection</param>
		/// <typeparam name="TCollection">The type of the mapping class</typeparam>
		/// <returns>When the promise completes, you'll have an authorized collection</returns>
		public static Promise<IMongoCollection<TCollection>> UserGroupDataCollection<TCollection>(
			this IStorageObjectConnectionProvider provider, string name)
			where TCollection : StorageDocument
			=> provider.GetCollection<UserGroupData, TCollection>(name);

		/// <summary>
		/// Gets a MongoDB collection from UserGroupData by the requested name, and uses the given mapping class.
		/// If you want to control the collection name separate from the class name, consider using <see cref="UserGroupDataCollection{TCollection}(string)"/>
		/// </summary>
		/// <param name="name">The name of the collection</param>
		/// <typeparam name="TCollection">The type of the mapping class</typeparam>
		/// <returns>When the promise completes, you'll have an authorized collection</returns>
		public static Promise<IMongoCollection<TCollection>> UserGroupDataCollection<TCollection>(
			this IStorageObjectConnectionProvider provider)
			where TCollection : StorageDocument
			=> provider.GetCollection<UserGroupData, TCollection>();
		
		
			/// <summary>
		/// Updates the document under the given ID using values from the <see cref="updatedData"/> parameter.
		/// </summary>
		/// <returns>True if at least one document was affected. False otherwise.</returns>
		public static async Promise<bool> Update<T>(this IStorageObjectConnectionProvider provider, string id,
			T updatedData) where T : StorageDocument, ISetStorageDocument<T>
		{
			var collection = await provider.GetCollection<UserGroupData, T>();
			var documentToUpdate = await provider.GetById<T>(id);
			if (documentToUpdate == null) 
				return false;

			documentToUpdate.Set(updatedData);
			var result = await collection.ReplaceOneAsync(provider.GetFilterById<T>(id), documentToUpdate);
			return result.ModifiedCount > 0;
		}

		/// <summary>
		/// Gets all objects of given type from the database
		/// </summary>
		public static async Promise<List<T>> GetAll<T>(this IStorageObjectConnectionProvider provider)
			where T : StorageDocument
		{
			var collection = await provider.GetCollection<UserGroupData, T>();
			return collection.Find(data => true).ToList();
		}

		/// <summary>
		/// Gets an object of given type and id.
		/// </summary>
		public static async Promise<T> GetById<T>(this IStorageObjectConnectionProvider provider, string id)
			where T : StorageDocument
		{
			var collection = await provider.GetCollection<UserGroupData, T>();
			var search = await collection.FindAsync(provider.GetFilterById<T>(id));
			return search.FirstOrDefault();
		}

		/// <summary>
		/// Gets an object of given type by field name.
		/// </summary>
		public static async Promise<T> GetByFieldName<T, TValue>(this IStorageObjectConnectionProvider provider,
			string field, TValue value) where T : StorageDocument
		{
			var collection = await provider.GetCollection<UserGroupData, T>();
			var search = await collection.FindAsync(provider.GetFilterByField<T, TValue>(field, value));
			return search.FirstOrDefault();
		}

		/// <summary>
		/// Gets all object of given type by field name.
		/// </summary>
		public static async Promise<List<T>> GetAllByFieldName<T, TValue>(
			this IStorageObjectConnectionProvider provider, Expression<Func<T, TValue>> field,
			IEnumerable<TValue> values) where T : StorageDocument
		{
			var collection = await provider.GetCollection<UserGroupData, T>();
			var search = await collection.FindAsync(provider.GetAllFilterByField(field, values));
			return search.ToList();
		}

		/// <summary>
		/// Gets a MongoDB ID filter for a given <see cref="StorageDocument"/>
		/// </summary>
		/// <typeparam name="T">A <see cref="StorageDocument"/> derived type you want to filter.</typeparam>
		private static FilterDefinition<T> GetFilterById<T>(this IStorageObjectConnectionProvider provider, string id)
			where T : StorageDocument
			=> Builders<T>.Filter.Eq("_id", new ObjectId(id));

		/// <summary>
		/// Gets a MongoDB field filter for a given <see cref="StorageDocument"/>
		/// </summary>
		/// <typeparam name="T">A <see cref="StorageDocument"/> derived type you want to filter.</typeparam>
		/// <typeparam name="TValue"></typeparam>
		private static FilterDefinition<T> GetFilterByField<T, TValue>(this IStorageObjectConnectionProvider provider,
			string field, TValue value) where T : StorageDocument
			=> Builders<T>.Filter.Eq(field, value);

		/// <summary>
		/// Gets all MongoDB field filter for a given <see cref="StorageDocument"/>
		/// </summary>
		/// <typeparam name="T">A <see cref="StorageDocument"/> derived type you want to filter.</typeparam>
		/// <typeparam name="TValue"></typeparam>
		private static FilterDefinition<T> GetAllFilterByField<T, TValue>(
			this IStorageObjectConnectionProvider provider, Expression<Func<T, TValue>> field,
			IEnumerable<TValue> values) where T : StorageDocument
			=> Builders<T>.Filter.In(field, values);
	}
	
	
}
