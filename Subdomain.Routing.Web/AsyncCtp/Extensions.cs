using System;
using System.Data.Services.Client;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.StorageClient;

namespace Subdomain.Routing.AsyncCtp
{
    /// <summary>
    ///   <para>General Extensions class</para>
    /// </summary>
    public static class Extensions
    {
        #region type

        /// <summary>
        ///   Check a provider for nullable
        /// </summary>
        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType
                && !type.IsGenericTypeDefinition
                && (type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static Task<bool> CreateTableIfNotExistAsync(this CloudTableClient cloudTableClient, string tableName)
        {
            return
                Task.Factory.FromAsync<string, bool>(cloudTableClient.BeginCreateTableIfNotExist,
                                                     cloudTableClient.EndCreateTableIfNotExist,
                                                     tableName,
                                                     null);
        }

        public static Task<DataServiceResponse> SaveChangesWithRetriesAsync(this TableServiceContext context)
        {
            return
                Task.Factory.FromAsync<DataServiceResponse>(context.BeginSaveChangesWithRetries,
                                                            context.EndSaveChangesWithRetries,
                                                            null);
        }

        public static Task<DataServiceResponse> SaveChangesWithRetriesAsync(this TableServiceContext context, SaveChangesOptions options)
        {
            return
                Task.Factory.FromAsync<SaveChangesOptions, DataServiceResponse>(context.BeginSaveChangesWithRetries,
                                                                                context.EndSaveChangesWithRetries,
                                                                                options,
                                                                                null);
        }

        #endregion
    }
}
