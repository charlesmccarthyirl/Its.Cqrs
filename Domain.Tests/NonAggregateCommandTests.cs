using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Its.Recipes;
using NUnit.Framework;

namespace Microsoft.Its.Domain.Tests
{
    [TestFixture]
    public class NonAggregateCommandTests
    {
        internal static int CallCount;

        [SetUp]
        public void Setup()
        {
            CallCount = 0;
        }

        [Test]
        public void non_event_sourced_command_schedulers_can_be_resolved()
        {
            Action getScheduler = () => Configuration.Current.CommandScheduler<Foo>();

            getScheduler.ShouldNotThrow();
        }

        [Test]
        public async Task when_a_command_is_applied_directly_the_command_is_executed()
        {
            Command<Foo>.AuthorizeDefault = (account, command) => true;
            var validationString = Any.String();
            var doStuff = new DoStuff { Validation = validationString };
            await doStuff.ApplyToAsync(new Foo());
            CallCount.Should().Be(1);
        }

        [Test]
        public async Task when_a_command_is_applied_directly_with_an_etag_the_command_is_executed()
        {
            Command<Foo>.AuthorizeDefault = (account, command) => true;
            var validationString = Any.String();
            var doStuff = new DoStuff { Validation = validationString, ETag = Any.Guid().ToString() };
            await doStuff.ApplyToAsync(new Foo());
            CallCount.Should().Be(1);
        }
    }

    public class Foo
    {
    }

    public class DoStuffCommandHandler : ICommandHandler<Foo, DoStuff>
    {
        public async Task EnactCommand(Foo aggregate, DoStuff command)
        {
            NonAggregateCommandTests.CallCount++;
        }

        public async Task HandleScheduledCommandException(Foo aggregate, CommandFailed<DoStuff> command)
        {
        }
    }

    public class DoStuff : Command<Foo>
    {
        public string Validation { get; set; }
    }
}