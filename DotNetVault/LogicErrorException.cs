using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace DotNetVault
{
    internal sealed class LogicErrorException : Exception
    {
        public LogicErrorException() : this("An internal logic error was detected.  This is a bug that should be reported.")
        {
        }

        public LogicErrorException([NotNull] SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public LogicErrorException(string message) : base(message)
        {
        }

        public LogicErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}