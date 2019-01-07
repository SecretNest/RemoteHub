using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Contains the exception occurred while accessing underlying object.
    /// </summary>
    public class ConnectionExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception occurred while accessing underlying object.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets whether this exception is a fatal one which terminated the connection and further jobs.
        /// </summary>
        public bool IsFatal { get; }

        /// <summary>
        /// Gets whether the operation which raised this exception is retried.
        /// </summary>
        public bool IsRetried { get; }

        /// <summary>
        /// Initializes an instance of ConnectionExceptionEventArgs.
        /// </summary>
        /// <param name="exception">The exception occurred while accessing underlying object.</param>
        /// <param name="isFatal">Whether this exception is a fatal one which terminated the connection and further jobs.</param>
        /// <param name="isRetried">Whether the operation which raised this exception is retried.</param>
        public ConnectionExceptionEventArgs(Exception exception, bool isFatal, bool isRetried)
        {
            Exception = exception;
            IsFatal = isFatal;
            IsRetried = isRetried;
        }
    }
}
