using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Its.Domain.Testing;
using NUnit.Framework;
using Sample.Domain.Ordering;

namespace Microsoft.Its.Domain.Sql.Tests
{
    [Category("Catchups")]
    [TestFixture]
    public class RemainingTimeCatchupTests : EventStoreDbTest
    {
        protected override void AfterClassIsInitialized()
        {
              // these tests behave a little oddly sometimes if the database has just been rebuilt and no events have yet been caught up, so run a quick catchup to start
            Events.Write(1);
            RunCatchup(new TestProjector());
        }

        [SetUp]
        public void Init()
        {
            VirtualClock.Start();
        }

        [TearDown]
        public new void TearDown()
        {
            Clock.Reset();
        }

        [Test]
        public void If_events_have_been_processed_during_initial_replay_then_the_remaining_time_is_estimated_correctly()
        {
            //arrange
            IEnumerable<EventHandlerProgress> progress = null;
            Events.Write(10);
            var eventsProcessed = 0;
            var projector = new TestProjector
            {
                DoSomething = e =>
                {
                    if (eventsProcessed == 5)
                    {
                        progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());
                    }
                    VirtualClock.Current.AdvanceBy(TimeSpan.FromSeconds(1));
                    eventsProcessed++;
                }
            };

            //act
            RunCatchup(projector);
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .TimeRemainingForCatchup.Value
                    .Should()
                    .Be(TimeSpan.FromSeconds(5));
        }

        [Test]
        public void If_events_have_been_processed_after_initial_replay_then_the_remaining_time_is_estimated_correctly()
        {
            //arrange
            //Initial replay
            Events.Write(10);
            var projector = new TestProjector();
            RunCatchup(projector);

            //new set of events come in
            IEnumerable<EventHandlerProgress> progress = null;
            Events.Write(10);
            var eventsProcessed = 0;
            projector.DoSomething = (e) =>
            {
                if (eventsProcessed == 5)
                {
                    progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());
                }
                VirtualClock.Current.AdvanceBy(TimeSpan.FromSeconds(1));
                eventsProcessed++;
            };

            //act
            RunCatchup(projector);
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .TimeRemainingForCatchup.Value
                    .Should()
                    .Be(TimeSpan.FromSeconds(5));
        }

        [Test]
        public void If_events_have_been_processed_after_initial_replay_then_the_time_taken_for_initial_replay_is_saved()
        {
            //arrange
            ResetReadModelInfo();
            var projector = new TestProjector
            {
                DoSomething = e => VirtualClock.Current.AdvanceBy(TimeSpan.FromSeconds(1))
            };
            //Initial replay
            Events.Write(10);
            RunCatchup(projector);

            //new set of events come in
            Events.Write(5);

            //act
            RunCatchup(projector);
            var progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());

            //assert
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .TimeTakenForInitialCatchup.Value
                    .Should()
                    .Be(TimeSpan.FromSeconds(9));
        }

        [Test]
        public void If_events_have_been_processed_after_initial_replay_then_the_number_of_events_for_initial_replay_is_saved()
        {
            //arrange
            ResetReadModelInfo();
            var projector = new TestProjector
            {
                DoSomething = e => VirtualClock.Current.AdvanceBy(TimeSpan.FromSeconds(1))
            };
            //Initial replay
            Events.Write(10);
            RunCatchup(projector);

            //new set of events come in
            Events.Write(5);
            RunCatchup(projector);

            //act
            var progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());

            //assert
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .InitialCatchupEvents.Value
                    .Should()
                    .Be(10);
        }

        [Test]
        public void If_events_have_been_processed_then_the_correct_number_of_remaining_events_is_returned()
        {
            //arrange
            IEnumerable<EventHandlerProgress> progress = null;
            Events.Write(5);

            var eventsProcessed = 0;
            var projector = new TestProjector
            {
                DoSomething = e =>
                {
                    if (eventsProcessed == 4)
                    {
                        progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());
                    }
                    eventsProcessed++;
                }
            };

            //act
            RunCatchup(projector);

            //assert
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .EventsRemaining
                    .Should()
                    .Be(1);
        }

        [Test]
        public void If_no_events_have_been_processed_then_the_remaining_time_is_null()
        {
            //arrange
            ResetReadModelInfo();
            Events.Write(5);

            //act
            var progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());

            //assert
            progress.First(p => p.Name == EventHandler.FullName(new TestProjector()))
                    .TimeRemainingForCatchup
                    .HasValue
                    .Should()
                    .BeFalse();
        }

        [Test]
        public void If_all_events_have_been_processed_then_the_remaining_time_is_zero()
        {
            //arrange
            var projector = new TestProjector();
            Events.Write(5);
            RunCatchup(projector);

            //act
            var progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());

            //assert
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .TimeRemainingForCatchup
                    .Value
                    .Should()
                    .Be(TimeSpan.FromMinutes(0));
        }

        [Test]
        public void If_all_events_have_been_processed_then_the_percentage_completed_is_100()
        {
            //arrange
            Events.Write(5);
            var projector = new TestProjector();
            RunCatchup(projector);

            //act
            var progress = EventHandlerProgressCalculator.Calculate(() => new ReadModelDbContext());

            //assert
            progress.First(p => p.Name == EventHandler.FullName(projector))
                    .PercentageCompleted
                    .Should()
                    .Be(100);
        }

        private void ResetReadModelInfo()
        {
            using (var db = new ReadModelDbContext())
            {
                foreach (var info in db.Set<ReadModelInfo>())
                {
                    info.InitialCatchupStartTime = null;
                    info.InitialCatchupEndTime = null;
                    info.BatchRemainingEvents = 0;
                    info.BatchTotalEvents = 0;
                    info.BatchStartTime = null;
                }
                db.SaveChanges();
            }
        }

        private void RunCatchup(TestProjector projector)
        {
            using (var catchup = CreateReadModelCatchup(projector))
            {
                catchup.Run();
            }
        }
    }

    public class TestProjector : IUpdateProjectionWhen<Order.ItemAdded>
    {
        public Action<Order.ItemAdded> DoSomething = e => { };

        public void UpdateProjection(Order.ItemAdded @event)
        {
            DoSomething(@event);
        }
    }
}