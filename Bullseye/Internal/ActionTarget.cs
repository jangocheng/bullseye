namespace Bullseye.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class ActionTarget : Target
    {
        private readonly Func<Task> action;

        public ActionTarget(string name, IEnumerable<string> dependencies, Func<Task> action)
            : base(name, dependencies) => this.action = action;

        public override async Task<TimeSpan?> RunAsync(bool dryRun, bool parallel, Logger log, Func<Exception, bool> messageOnly)
        {
            await log.Starting(this.Name).ConfigureAwait(false);

            var stopWatch = Stopwatch.StartNew();

            if (!dryRun && this.action != default)
            {
                try
                {
                    await this.action().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (!messageOnly(ex))
                    {
                        await log.Error(this.Name, ex).ConfigureAwait(false);
                    }

                    stopWatch.Stop();
                    await log.Failed(this.Name, ex, stopWatch.Elapsed.TotalMilliseconds).ConfigureAwait(false);
                    throw new TargetFailedException(stopWatch.Elapsed, ex);
                }
            }

            stopWatch.Stop();
            await log.Succeeded(this.Name, stopWatch.Elapsed.TotalMilliseconds).ConfigureAwait(false);
            return stopWatch.Elapsed;
        }
    }
}
