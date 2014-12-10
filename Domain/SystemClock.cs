﻿using System;
using System.Diagnostics;

namespace Microsoft.Its.Domain
{
    /// <summary>
    /// Provides access to system time.
    /// </summary>
    [DebuggerStepThrough]
    public class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        private SystemClock()
        {
        }

        /// <summary>
        /// Gets the current time via <see cref="DateTimeOffset.Now" />.
        /// </summary>
        public DateTimeOffset Now()
        {
            return DateTimeOffset.Now;
        }

        public override string ToString()
        {
            return GetType() + ": " + Now().ToString("O");
        }
    }
}