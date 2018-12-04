namespace Bullseye.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public static class TargetCollectionExtensions
    {
        public static Task RunAsync(this TargetCollection targets, IEnumerable<string> args, IConsole console) =>
            RunAsync(targets ?? new TargetCollection(), args.Sanitize().ToList(), console ?? new SystemConsole());

        public static Task RunAndExitAsync(this TargetCollection targets, IEnumerable<string> args, IEnumerable<Type> exceptionMessageOnly) =>
            RunAndExitAsync(targets ?? new TargetCollection(), args.Sanitize().ToList(), new SystemConsole(), exceptionMessageOnly ?? Enumerable.Empty<Type>());

        private static async Task RunAsync(this TargetCollection targets, List<string> args, IConsole console)
        {
            var (names, options) = Parse(args);
            var (log, palette) = await InitializeLogger(options, console).ConfigureAwait(false);

            await RunAsync(targets, names, options, log, palette, args, console).ConfigureAwait(false);
        }

        private static async Task RunAndExitAsync(this TargetCollection targets, List<string> args, IConsole console, IEnumerable<Type> exceptionMessageOnly)
        {
            var (names, options) = Parse(args);
            var (log, palette) = await InitializeLogger(options, console).ConfigureAwait(false);

            try
            {
                await RunAsync(targets, names, options, log, palette, args, console).ConfigureAwait(false);
            }
            catch (Exception ex) when (exceptionMessageOnly.Concat(new[] { typeof(BullseyeException) }).Any(type => type.IsAssignableFrom(ex.GetType())))
            {
                await log.Error(ex.Message).ConfigureAwait(false);
                Environment.Exit(2);
            }
            catch (Exception ex)
            {
                await log.Error(ex.ToString()).ConfigureAwait(false);
                Environment.Exit(ex.HResult == 0 ? 1 : ex.HResult);
            }
        }

        private static async Task RunAsync(this TargetCollection targets, List<string> names, Options options, Logger log, Palette palette, List<string> args, IConsole console)
        {
            if (options.UnknownOptions.Count > 0)
            {
                throw new BullseyeException($"Unknown option{(options.UnknownOptions.Count > 1 ? "s" : "")} {options.UnknownOptions.Spaced()}. \"--help\" for usage.");
            }

            await log.Verbose($"Args: {string.Join(" ", args)}").ConfigureAwait(false);

            if (options.ShowHelp)
            {
                await console.Out.WriteLineAsync(GetUsage(palette)).ConfigureAwait(false);
                return;
            }

            if (options.ListTree || options.ListDependencies || options.ListInputs || options.ListTargets)
            {
                var rootTargets = names.Any() ? names : targets.Select(target => target.Name).OrderBy(name => name).ToList();
                var maxDepth = options.ListTree ? int.MaxValue : options.ListDependencies ? 1 : 0;
                var maxDepthToShowInputs = options.ListTree ? int.MaxValue : 0;
                await console.Out.WriteLineAsync(targets.ToString(rootTargets, maxDepth, maxDepthToShowInputs, options.ListInputs, palette)).ConfigureAwait(false);
                return;
            }

            if (names.Count == 0)
            {
                names.Add("default");
            }

            await targets.RunAsync(names, options.SkipDependencies, options.DryRun, options.Parallel, log).ConfigureAwait(false);
        }

        private static async Task<(Logger, Palette)> InitializeLogger(Options options, IConsole console)
        {
            if (options.Clear)
            {
                console.Clear();
            }

            var operatingSystem = OperatingSystem.Unknown;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                operatingSystem = OperatingSystem.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                operatingSystem = OperatingSystem.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                operatingSystem = OperatingSystem.MacOS;
            }

            if (!options.NoColor && operatingSystem == OperatingSystem.Windows)
            {
                await WindowsConsole.TryEnableVirtualTerminalProcessing(console.Out, options.Verbose).ConfigureAwait(false);
            }

            var isHostDetected = false;
            if (options.Host == Host.Unknown)
            {
                isHostDetected = true;

                if (Environment.GetEnvironmentVariable("APPVEYOR")?.ToUpperInvariant() == "TRUE")
                {
                    options.Host = Host.Appveyor;
                }
                else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRAVIS_OS_NAME")))
                {
                    options.Host = Host.Travis;
                }
                else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEAMCITY_PROJECT_NAME")))
                {
                    options.Host = Host.TeamCity;
                }
            }

            var palette = new Palette(options.NoColor, options.Host, operatingSystem);
            var log = new Logger(console, options.SkipDependencies, options.DryRun, options.Parallel, palette, options.Verbose);

            await log.Version().ConfigureAwait(false);
            await log.Verbose($"Host: {options.Host}{(options.Host != Host.Unknown ? $" ({(isHostDetected ? "detected" : "forced")})" : "")}").ConfigureAwait(false);
            await log.Verbose($"OS: {operatingSystem}").ConfigureAwait(false);

            return (log, palette);
        }

        private static (List<string>, Options) Parse(List<string> args)
        {
            var targetNames = new List<string>();
            var options = new Options();

            var helpOptions = new[] { "--help", "-h", "-?" };

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "-c":
                    case "--clear":
                        options.Clear = true;
                        break;
                    case "-n":
                    case "--dry-run":
                        options.DryRun = true;
                        break;
                    case "-D":
                    case "--list-dependencies":
                        options.ListDependencies = true;
                        break;
                    case "-I":
                    case "--list-inputs":
                        options.ListInputs = true;
                        break;
                    case "-T":
                    case "--list-targets":
                        options.ListTargets = true;
                        break;
                    case "-t":
                    case "--list-tree":
                        options.ListTree = true;
                        break;
                    case "-N":
                    case "--no-color":
                        options.NoColor = true;
                        break;
                    case "-p":
                    case "--parallel":
                        options.Parallel = true;
                        break;
                    case "-s":
                    case "--skip-dependencies":
                        options.SkipDependencies = true;
                        break;
                    case "-v":
                    case "--verbose":
                        options.Verbose = true;
                        break;
                    case "--appveyor":
                        options.Host = Host.Appveyor;
                        break;
                    case "--travis":
                        options.Host = Host.Travis;
                        break;
                    case "--teamcity":
                        options.Host = Host.TeamCity;
                        break;
                    default:
                        if (helpOptions.Contains(arg, StringComparer.OrdinalIgnoreCase))
                        {
                            options.ShowHelp = true;
                        }
                        else if (arg.StartsWith("-"))
                        {
                            options.UnknownOptions.Add(arg);
                        }
                        else
                        {
                            targetNames.Add(arg);
                        }

                        break;
                }
            }

            return (targetNames, options);
        }

        private static string ToString(this TargetCollection targets, List<string> rootTargets, int maxDepth, int maxDepthToShowInputs, bool listInputs, Palette p)
        {
            var value = new StringBuilder();

            var corner = "└─";
            var teeJunction = "├─";
            var line = "│ ";

            foreach (var rootTarget in rootTargets)
            {
                Append(new List<string> { rootTarget }, new Stack<string>(), true, "", 0);
            }

            return value.ToString();

            void Append(List<string> names, Stack<string> seenTargets, bool isRoot, string previousPrefix, int depth)
            {
                if (depth > maxDepth)
                {
                    return;
                }

                foreach (var item in names.Select((name, index) => new { name, index }))
                {
                    var circularDependency = seenTargets.Contains(item.name);

                    seenTargets.Push(item.name);

                    try
                    {
                        var prefix = isRoot
                            ? ""
                            : $"{previousPrefix.Replace(corner, "  ").Replace(teeJunction, line)}{(item.index == names.Count - 1 ? corner : teeJunction)}";

                        var isMissing = !targets.Contains(item.name);

                        value.Append($"{p.Tree}{prefix}{(isRoot ? p.Target : p.Dependency)}{item.name}");

                        if (isMissing)
                        {
                            value.AppendLine($" {p.Failed}(missing){p.Default}");
                            continue;
                        }

                        if (circularDependency)
                        {
                            value.AppendLine($" {p.Failed}(circular dependency){p.Default}");
                            continue;
                        }

                        value.AppendLine(p.Default);

                        var target = targets[item.name];

                        if (listInputs && depth <= maxDepthToShowInputs && target is IHaveInputs hasInputs)
                        {
                            foreach (var inputItem in hasInputs.Inputs.Select((input, index) => new { input, index }))
                            {
                                var inputPrefix = $"{prefix.Replace(corner, "  ").Replace(teeJunction, line)}{(target.Dependencies.Any() && depth + 1 <= maxDepth ? line : "  ")}";

                                value.AppendLine($"{p.Tree}{inputPrefix}{p.Input}{inputItem.input}{p.Default}");
                            }
                        }

                        Append(target.Dependencies, seenTargets, false, prefix, depth + 1);
                    }
                    finally
                    {
                        seenTargets.Pop();
                    }
                }
            }
        }

        public static string GetUsage(Palette p) =>
$@"{p.Label}Usage: {p.CommandLine}<command-line> {p.Option}[<options>] {p.Target}[<targets>]

{p.Label}command-line: {p.Text}The command line which invokes the build targets.
  {p.Label}Examples:
    {p.CommandLine}build.cmd
    {p.CommandLine}build.sh
    {p.CommandLine}dotnet run --project targets --

{p.Label}options:
 {p.Option}-c, --clear                {p.Text}Clear the console before execution
 {p.Option}-n, --dry-run              {p.Text}Do a dry run without executing actions
 {p.Option}-D, --list-dependencies    {p.Text}List all (or specified) targets and dependencies, then exit
 {p.Option}-I, --list-inputs          {p.Text}List all (or specified) targets and inputs, then exit
 {p.Option}-T, --list-targets         {p.Text}List all (or specified) targets, then exit
 {p.Option}-t, --list-tree            {p.Text}List all (or specified) targets and dependency trees, then exit
 {p.Option}-N, --no-color             {p.Text}Disable colored output
 {p.Option}-p, --parallel             {p.Text}Run targets in parallel
 {p.Option}-s, --skip-dependencies    {p.Text}Do not run targets' dependencies
 {p.Option}-v, --verbose              {p.Text}Enable verbose output
 {p.Option}    --appveyor             {p.Text}Force Appveyor mode (normally auto-detected)
 {p.Option}    --teamcity             {p.Text}Force TeamCity mode (normally auto-detected)
 {p.Option}    --travis               {p.Text}Force Travis CI mode (normally auto-detected)
 {p.Option}-h, --help, -?             {p.Text}Show this help, then exit (case insensitive)

{p.Label}targets: {p.Text}A list of targets to run or list.
  If not specified, the {p.Target}""default""{p.Text} target will be run, or all targets will be listed.

{p.Label}Remarks:
  {p.Text}The {p.Option}--list-xxx {p.Text}options can be combined.

{p.Label}Examples:
  {p.CommandLine}build.cmd
  {p.CommandLine}build.cmd {p.Option}-D
  {p.CommandLine}build.sh {p.Option}-t -I {p.Target}default
  {p.CommandLine}build.sh {p.Target}test pack
  {p.CommandLine}dotnet run --project targets -- {p.Option}-n {p.Target}build{p.Default}";
    }
}
