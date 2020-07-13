using System;
using DotNetVault.ClortonGame;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// A factory for creating clorton games
    /// </summary>
    public interface IDeadBeefCafeGameFactory
    {
        /// <summary>
        /// Create and begin a dead beef cafe babe game.
        /// </summary>
        /// <param name="helper">An output helper (for logging purposes).</param>
        /// <param name="numReader">The number of reader threads (must be >= 2).</param>
        /// <returns>A clorton game that has begun.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> was <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="numReader"/> was less than two.</exception>
        /// <exception cref="InvalidOperationException">There was a problem creating or starting the game.</exception>
        [NotNull] IDeadBeefCafeGame CreateDeadBeefCafeGame([NotNull] IDisposableOutputHelper helper, int numReader);

        /// <summary>
        /// Create and begin a dead beef cafe babe  game.
        /// </summary>
        /// <param name="helper">An output helper (for logging purposes).</param>
        /// <param name="numReader">The number of reader threads (must be >= 2).</param>
        /// <param name="doneHandler">(optional) event handler for when the game finished</param>
        /// <returns>A clorton game that has begun.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> was <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="numReader"/> was less than two.</exception>
        /// <exception cref="InvalidOperationException">There was a problem creating or starting the game.</exception>
        [NotNull] IDeadBeefCafeGame CreateDeadBeefCafeGame([NotNull] IDisposableOutputHelper helper, int numReader, [CanBeNull] EventHandler<DeadBeefCafeGameEndedEventArgs> doneHandler);

    }
}
