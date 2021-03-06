namespace Bullseye.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class TargetCollectionExtensions
    {
        public static Task RunAsync(this TargetCollection targets, IEnumerable<string> args) =>
            RunAsync(targets ?? new TargetCollection(), args.Sanitize().ToList());

        public static Task RunAndExitAsync(this TargetCollection targets, IEnumerable<string> args, Func<Exception, bool> messageOnly) =>
            RunAndExitAsync(targets ?? new TargetCollection(), args.Sanitize().ToList(), messageOnly ?? (_ => false));

        private static async Task RunAsync(this TargetCollection targets, List<string> args)
        {
            var (names, options) = args.Parse();
            var log = await ConsoleExtensions.Initialize(options).ConfigureAwait(false);

            await RunAsync(targets, names, options, log, _ => false, args).ConfigureAwait(false);
        }

        private static async Task RunAndExitAsync(this TargetCollection targets, List<string> args, Func<Exception, bool> messageOnly)
        {
            var (names, options) = args.Parse();
            var log = await ConsoleExtensions.Initialize(options).ConfigureAwait(false);

            try
            {
                await RunAsync(targets, names, options, log, messageOnly, args).ConfigureAwait(false);
            }
            catch (InvalidUsageException ex)
            {
                await log.Error(ex.Message).ConfigureAwait(false);
                Environment.Exit(2);
            }
            catch (TargetFailedException)
            {
                Environment.Exit(1);
            }

            Environment.Exit(0);
        }

        private static async Task RunAsync(this TargetCollection targets, List<string> names, Options options, Logger log, Func<Exception, bool> messageOnly, List<string> args)
        {
            if (options.UnknownOptions.Count > 0)
            {
                throw new InvalidUsageException($"Unknown option{(options.UnknownOptions.Count > 1 ? "s" : "")} {options.UnknownOptions.Spaced()}. \"--help\" for usage.");
            }

            await log.Verbose($"Args: {string.Join(" ", args)}").ConfigureAwait(false);

            if (options.ShowHelp)
            {
                await log.Usage().ConfigureAwait(false);
                return;
            }

            if (options.ListTree || options.ListDependencies || options.ListInputs || options.ListTargets)
            {
                var rootTargets = names.Any() ? names : targets.Select(target => target.Name).OrderBy(name => name).ToList();
                var maxDepth = options.ListTree ? int.MaxValue : options.ListDependencies ? 1 : 0;
                var maxDepthToShowInputs = options.ListTree ? int.MaxValue : 0;
                await log.Targets(targets, rootTargets, maxDepth, maxDepthToShowInputs, options.ListInputs).ConfigureAwait(false);
                return;
            }

            if (names.Count == 0)
            {
                names.Add("default");
            }

            await targets.RunAsync(names, options.SkipDependencies, options.DryRun, options.Parallel, log, messageOnly).ConfigureAwait(false);
        }
    }
}
