using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Contains the exception occurred while accessing Redis database.
    /// </summary>
    public class RedisExceptionEventArgs : EventArgs
    {
        //Gets the exception occurred while accessing Redis database.
        public Exception Exception { get; }
        //Gets whether this exception is a fatal one which terminated the Redis connection and further jobs.
        public bool IsFatal { get; }

        /// <summary>
        /// Initializes an instance of RedisExceptionEventArgs.
        /// </summary>
        /// <param name="exception">The exception occurred while accessing Redis database.</param>
        /// <param name="isFatal">Whether this exception is a fatal one which terminated the Redis connection and further jobs.</param>
        public RedisExceptionEventArgs(Exception exception, bool isFatal)
        {
            Exception = exception;
            IsFatal = isFatal;
        }
    }
}
