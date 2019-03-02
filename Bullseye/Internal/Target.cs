namespace Bullseye.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class Target
    {
        public Target(string name, IEnumerable<string> dependencies)
        {
            this.Name = name ?? throw new InvalidUsageException("Target name cannot be null.");
            this.Dependencies = dependencies.Sanitize().ToList();
        }

        public string Name { get; }

        public List<string> Dependencies { get; }

        public virtual async Task<TimeSpan?> RunAsync(bool dryRun, bool parallel, Logger log, Func<Exception, bool> messageOnly)
        {
            await log.Succeeded(this.Name, null).ConfigureAwait(false);
            return default;
        }
    }
}
