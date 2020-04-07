using System;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Event arguments conveying what text was appended to the output helper and when
    /// </summary>
    public sealed class OutputHelperAppendedToEventArgs
    {
        /// <summary>
        /// Time stamp
        /// </summary>
        public DateTime TimeStamp { get; }

        /// <summary>
        /// what was appended
        /// </summary>
        [NotNull] public string Payload { get; }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="payload">string that was appended</param>
        public OutputHelperAppendedToEventArgs(string payload) 
            : this(CgTimeStampSource.Now, payload) { }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="timeStamp">a timestamp</param>
        /// <param name="payload">text that was appended</param>
        public OutputHelperAppendedToEventArgs(DateTime timeStamp, string payload)
        {
            TimeStamp = timeStamp;
            Payload = payload ?? string.Empty;
        }

        /// <inheritdoc />
        public override string ToString() =>
            "At [" + TimeStamp.ToString("O") + 
            "], the following payload was added: [" + Payload + "].";

    }
}