using System;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;
using Microsoft.Its.Recipes;

namespace Microsoft.Its.Domain.Sql
{
    public static class EventStore
    {
        /// <summary>
        /// Attempts to acquire ann exclusive SQL resource lock.
        /// </summary>
        /// <param name="context">The <see cref="EventStoreDbContext" />.</param>
        /// <param name="resourceName">The name of the resource.</param>
        /// <param name="wait">The amount of time to wait to acquire the lock.</param>
        /// <returns>True if the lock was acquired and the connection is usable; otherwise, false.</returns>
        /// <remarks>Other connections attempting to acquire a lock with the same name will wait for the specified time. If the lock becomes available during that time, one waiting caller will immediately receive the lock and be able to use their <see cref="EventStoreDbContext" />. Callers who are awaiting a lock or failed to acquire a lock due to the wait time expiring will receive an exception when attempting to use their <see cref="EventStoreDbContext" />.</remarks>
        public static Task<bool> ExclusiveConnection(
            this EventStoreDbContext context,
            string resourceName,
            TimeSpan? wait = null)
        {
            var tcs = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                var dbConnection = ((IObjectContextAdapter) context).ObjectContext.Connection;
                dbConnection.Open();

                // this call will block if the AppLock is not acquired
                var appLock = new AppLock(context,
                                          resourceName,
                                          wait.IfNotNull()
                                              .Then(t => (int) t.TotalMilliseconds)
                                              .ElseDefault());

                var isAcquired = appLock.IsAcquired;

                if (!isAcquired)
                {
                    dbConnection.Dispose();
                }

                tcs.SetResult(isAcquired);
            });

            return tcs.Task;
        }
    }
}