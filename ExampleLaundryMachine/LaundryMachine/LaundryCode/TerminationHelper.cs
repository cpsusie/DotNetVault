using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    internal class TerminationHelper
    {
        [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
        [DoesNotReturn]
        internal static void TerminateApplication([JetBrains.Annotations.NotNull] string information, int exitCode = -1,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLineNumber = 0) => TerminateApplication<Exception>(information, null, exitCode,
            callerName, callerFile, callerLineNumber);
        [DoesNotReturn]
        internal static void TerminateApplication<TException>([JetBrains.Annotations.NotNull] string information, [CanBeNull] TException ex, int exitCode = -1,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0 ) where TException : Exception
        {
            string ts = TimeStampSource.Now.ToString("O");
            StackTrace st;
            try
            {
                st = ex != null ? new StackTrace(ex, true) : new StackTrace(true);
                string stackTrace = st.ToString();
                string sentenceOne =
                    $"At [{ts}], the caller {callerName} in file {callerFile} at line number {callerLineNumber} encountered a fatal error.  ";
                string exceptionInfo = ex != null
                    ? $"An exception of type {typeof(TException).Name} caused the error.  Message: [{ex.Message}].  "
                    : string.Empty;
                string extraInfo = information;
                string finalMessage = sentenceOne + exceptionInfo + extraInfo + "  Stack trace: " + stackTrace +
                                      Environment.NewLine;
                Console.Error.WriteLine(finalMessage);
            }
            finally 
            {
                Environment.Exit(exitCode);
            }
        }
    }
}
