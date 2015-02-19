using System;
using System.Threading.Tasks;

namespace Microsoft.Its.Domain.Tests.Infrastructure
{
    public static class TaskExtensions
    {
        public static async Task Timeout(
            this Task task,
            TimeSpan timeout,
            bool throwOnTimeout = true)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                await task;
            }
            else if (throwOnTimeout)
            {
                throw new TimeoutException();
            }
        }
    }
}