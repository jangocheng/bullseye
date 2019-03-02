namespace Bullseye.Internal
{
    using System;

#pragma warning disable CA1032 // Implement standard exception constructors
    public class TargetFailedException : Exception
#pragma warning restore CA1032 // Implement standard exception constructors
    {
        public TargetFailedException(TimeSpan duration, Exception innerException) : base(default, innerException) =>
            this.Duration = duration;

        public TimeSpan Duration { get; }
    }
}
