using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;

namespace Microsoft.Its.Domain.Sql
{
    public class ReadModelCatchupStatusUpdater : IDisposable
    {
        private readonly Func<DbContext> readModelDbContextCreator;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private DbConnection cachedConnection;


        public ReadModelCatchupStatusUpdater(Func<DbContext> readModelDbContextCreator)
        {
            if (readModelDbContextCreator == null)
            {
                throw new ArgumentNullException("readModelDbContextCreator");
            }

            this.readModelDbContextCreator = readModelDbContextCreator;
            ResetConnection();
        }

        private void ResetConnection()
        {
            disposables.Clear();
            var readModelDbContext = readModelDbContextCreator();
            disposables.Add(readModelDbContext);
            cachedConnection = readModelDbContext.OpenConnection();
            disposables.Add(cachedConnection);
        }

        public void Update(IEnumerable<string> names, DateTimeOffset lastUpdated, long currentAsOfEventId, double latencyInMilliseconds, long expectedNumberOfEvents, long eventsProcessed)
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    UpdateWithCachedConnection(names, lastUpdated, currentAsOfEventId, latencyInMilliseconds, expectedNumberOfEvents, eventsProcessed);
                    return;
                }
                catch
                {
                    if (i >= 4)
                    {
                        throw;
                    }

                    ResetConnection();

                    Thread.Sleep(i * 50);
                }
            }
        }

        private void UpdateWithCachedConnection(IEnumerable<string> names, DateTimeOffset lastUpdated, long currentAsOfEventId, double latencyInMilliseconds, long expectedNumberOfEvents, long eventsProcessed)
        {
            Update(cachedConnection, names: names, lastUpdated: lastUpdated, currentAsOfEventId: currentAsOfEventId, latencyInMilliseconds: latencyInMilliseconds, expectedNumberOfEvents: expectedNumberOfEvents, eventsProcessed: eventsProcessed);
        }

        private void UpdateWithNewConnection(IEnumerable<string> names, DateTimeOffset lastUpdated, long currentAsOfEventId, double latencyInMilliseconds, long expectedNumberOfEvents, long eventsProcessed)
        {
            using (var dbContext = readModelDbContextCreator())
            using (var connection = dbContext.OpenConnection())
            {
                Update(connection, names: names, lastUpdated: lastUpdated, currentAsOfEventId: currentAsOfEventId, latencyInMilliseconds: latencyInMilliseconds, expectedNumberOfEvents: expectedNumberOfEvents, eventsProcessed: eventsProcessed);
            }
        }

        private void Update(DbConnection connection, IEnumerable<string> names, DateTimeOffset lastUpdated, long currentAsOfEventId, double latencyInMilliseconds, long expectedNumberOfEvents, long eventsProcessed)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = GetCommandTextForNames(names);

                var commandParams = CreateParams(command);

                commandParams.LastUpdated.Value = lastUpdated;
                commandParams.CurrentAsOfEventId.Value = currentAsOfEventId;
                commandParams.LatencyInMilliseconds.Value = latencyInMilliseconds;
                commandParams.ExpectedNumberOfEvents.Value = expectedNumberOfEvents;
                commandParams.EventsProcessed.Value = eventsProcessed;

                command.ExecuteNonQuery();
            }
        }

        private static Params CreateParams(DbCommand command)
        {
            var lastUpdatedParam = command.CreateParameter();
            lastUpdatedParam.DbType = DbType.DateTimeOffset;
            lastUpdatedParam.ParameterName = "@lastUpdated";

            var currentAsOfEventIdParam = command.CreateParameter();
            currentAsOfEventIdParam.DbType = DbType.Int64;
            currentAsOfEventIdParam.ParameterName = "@currentAsOfEventId";

            var latencyInMillisecondsParam = command.CreateParameter();
            latencyInMillisecondsParam.DbType = DbType.Double;
            latencyInMillisecondsParam.ParameterName = "@latencyInMilliseconds";

            var expectedNumberOfEventsParam = command.CreateParameter();
            expectedNumberOfEventsParam.DbType = DbType.Int64;
            expectedNumberOfEventsParam.ParameterName = "@expectedNumberOfEvents";

            var eventsProcessedParam = command.CreateParameter();
            eventsProcessedParam.DbType = DbType.Int64;
            eventsProcessedParam.ParameterName = "@eventsProcessed";

            command.Parameters.Add(lastUpdatedParam);
            command.Parameters.Add(currentAsOfEventIdParam);
            command.Parameters.Add(latencyInMillisecondsParam);
            command.Parameters.Add(expectedNumberOfEventsParam);
            command.Parameters.Add(eventsProcessedParam);

            var commandParams = new Params
                                {
                                    ExpectedNumberOfEvents = expectedNumberOfEventsParam,
                                    CurrentAsOfEventId = currentAsOfEventIdParam,
                                    LastUpdated = lastUpdatedParam,
                                    LatencyInMilliseconds = latencyInMillisecondsParam,
                                    EventsProcessed = eventsProcessedParam
                                };
            return commandParams;
        }

        private class Params
        {
            public DbParameter LastUpdated { get; set; }
            public DbParameter CurrentAsOfEventId { get; set; }
            public DbParameter LatencyInMilliseconds { get; set; }
            public DbParameter ExpectedNumberOfEvents { get; set; }
            public DbParameter EventsProcessed { get; set; }
        }

        private static string GetCommandTextForNames(IEnumerable<string> names)
        {
            return string.Format(@"UPDATE [Events].[ReadModelInfo]
  SET 
    LastUpdated = @lastUpdated, 
    CurrentAsOfEventId = @currentAsOfEventId,
    LatencyInMilliseconds = @latencyInMilliseconds, 
    BatchRemainingEvents = @expectedNumberOfEvents - @eventsProcessed,
    BatchStartTime = CASE WHEN @eventsProcessed = 1 
                     THEN @lastUpdated
                     ELSE BatchStartTime 
                     END,
    BatchTotalEvents = CASE WHEN @eventsProcessed = 1 
                     THEN @expectedNumberOfEvents
                     ELSE BatchTotalEvents 
                     END,
    InitialCatchupEvents = CASE WHEN InitialCatchupStartTime IS NULL 
                     THEN @expectedNumberOfEvents
                     ELSE InitialCatchupEvents
                     END,
    InitialCatchupStartTime = CASE WHEN InitialCatchupStartTime IS NULL 
                     THEN @lastUpdated
                     ELSE InitialCatchupStartTime
                     END,
    InitialCatchupEndTime = CASE WHEN (@expectedNumberOfEvents - @eventsProcessed) = 0 AND InitialCatchupEndTime IS NULL 
                     THEN @lastUpdated
                     ELSE InitialCatchupEndTime
                     END
  WHERE Name IN ({0})", string.Join(", ", names.Select(n => string.Format("'{0}'", n))));
        }

        public void Dispose()
        {
            this.disposables.Dispose();
        }
    }
}