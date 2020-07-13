namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Constants for the Dead Beef Cafe Babe game
    /// </summary>
    public interface IDeadBeefCafeBabeGameConstants
    {
        /// <summary>
        /// The number the arbiter thread writes on the termination condition detection
        /// and the reader threads seek this text
        /// </summary>
        ref readonly UInt256 LookForNumber { get; }
        /// <summary>
        /// The number written by the x writer
        /// </summary>
        ref readonly UInt256 XNumber { get; }
        /// <summary>
        /// The number written 
        /// </summary>
        ref readonly UInt256 ONumber { get; }

    }
}