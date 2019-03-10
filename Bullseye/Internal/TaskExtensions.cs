namespace Bullseye.Internal
{
    using System;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {
        public static TimeSpan? GetDuration(this Task<TimeSpan?> task) =>
            task.IsFaulted
                ? (task.Exception.InnerException as TargetFailedException)?.Duration
                : task.Result;
    }
}
