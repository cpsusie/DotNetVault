using System;
using System.Collections.Immutable;
using DotNetVault.LockedResources;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// Indicates the reason for early disposal of a locked resource object
    /// </summary>
    public enum EarlyReleaseReason
    {
        /// <summary>
        /// This value signifies that the reason for the early release
        /// is because a method returning a locked resource object is throwing an
        /// exception but the return object must be disposed before the exception is thrown.
        /// </summary>
        DisposingOnError,
        /// <summary>
        /// This value signifies that the reason for the dispose is that a Custom LockedResourceObject's
        /// Dispose method is disposing the <see cref="LockedVaultMutableResource{TVault,TResource}"/>
        /// wrapped by the Custom LockedResourceObject
        /// </summary>
        CustomWrapperDispose,
    }

    /// <summary>
    /// If a method calls a method annotated with the <see cref="EarlyReleaseAttribute"/>
    /// the calling method must be annotated with this attribute and indicate the reason for
    /// the call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EarlyReleaseJustificationAttribute : Attribute
    {
        /// <summary>
        /// The reason for the early release
        /// </summary>
        public EarlyReleaseReason Justification { get; }

        internal static readonly string AttributeAssemblyLocation = typeof(Attribute).Assembly.Location;

        internal const string ShortenedName = "EarlyReleaseJustification";
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="reason">The reason for early release</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a defined value
        /// of the <see cref="EarlyReleaseReason"/> enum.</exception>
        public EarlyReleaseJustificationAttribute(EarlyReleaseReason reason)
        {
            switch (reason)
            {
                case EarlyReleaseReason.CustomWrapperDispose:
                case EarlyReleaseReason.DisposingOnError:
                    Justification = reason;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason,
                        @"The supplied value is not a defined value of the " +
                        $@"{typeof(EarlyReleaseReason).Name} enumeration type.");
            }
        }
        /// <inheritdoc/>
        public override string ToString() =>
            $"[{typeof(EarlyReleaseReason).Name}] -- Justification: [{Justification.ToString()}]";

    }

    /// <summary>
    /// This attribute is used to annotate the early dispose methods of LockedResourceObjects
    /// <see cref="LockedVaultMutableResource{TVault,TResource}.ErrorCaseReleaseOrCustomWrapperDispose"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EarlyReleaseAttribute : Attribute
    {
        internal const string ShortenedName = "EarlyRelease";
        /// <inheritdoc />
        public override string ToString() =>
            $"[{typeof(EarlyReleaseAttribute).Name}] -- this attribute indicates that the annotated method is a special use-case early release method for LockedResourceObjects.";

    }

    /// <summary>
    /// Defines extension methods for the <see cref="EarlyReleaseReason"/> enumeration,
    /// mostly to check whether a given value is a defined value of that type.
    /// </summary>
    public static class EarlyReleaseExtensionMethods
    {
        /// <summary>
        /// Check if a value is a defined value in the <see cref="EarlyReleaseReason"/>
        /// enum.
        /// </summary>
        /// <param name="reason">the reason to check</param>
        /// <returns>True only if <paramref name="reason"/> is a defined value of the <see cref="EarlyReleaseReason"/>
        /// enumeration.</returns>
        public static bool IsDefined(this EarlyReleaseReason reason) => TheDefinedReasons.Contains(reason);

        /// <summary>
        /// Check if a value is a defined value in the <see cref="EarlyReleaseReason"/>
        /// enum.
        /// </summary>
        /// <param name="reason">the reason to check</param>
        /// <returns>True only if <paramref name="reason"/> is not null and
        /// is a defined value of the <see cref="EarlyReleaseReason"/>
        /// enumeration.</returns>
        public static bool IsDefined(this EarlyReleaseReason? reason) => reason.HasValue && reason.Value.IsDefined();

        /// <summary>
        /// Return the value of <paramref name="reason"/> if it is not null
        /// AND is a defined value of the  <see cref="EarlyReleaseReason"/> enum.
        /// </summary>
        /// <param name="reason">the value to check</param>
        /// <returns>null if <paramref name="reason"/> is null or otherwise is not a
        /// defined value of the <see cref="EarlyReleaseReason"/>
        /// enum; otherwise returns the value of <paramref name="reason"/>.</returns>
        public static EarlyReleaseReason? ValueOrDefaultIfNDef(this EarlyReleaseReason? reason) =>
            reason.IsDefined() ? reason : null;
        /// <summary>
        /// Return the value of <paramref name="reason"/> if it is a defined value of the
        /// <see cref="EarlyReleaseReason"/> enum; otherwise return null
        /// </summary>
        /// <param name="reason"></param>
        /// <returns> the value of <paramref name="reason"/> if it is a defined value of the
        /// <see cref="EarlyReleaseReason"/> enum; otherwise return null</returns>
        public static EarlyReleaseReason? ValueOrDefaultIfNDef(this EarlyReleaseReason reason) =>
            TheDefinedReasons.Contains(reason) ? (EarlyReleaseReason?) reason : null;

        /// <summary>
        /// Check if the value of <paramref name="reason"/> is a defined value of the
        /// <see cref="EarlyReleaseReason"/> enum.
        /// </summary>
        /// <param name="reason">the value to check</param>
        /// <returns>the value</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="reason"/> is
        /// not a defined value of the <see cref="EarlyReleaseReason"/> enum.</exception>
        public static EarlyReleaseReason ValueOrThrowIfNDef(this EarlyReleaseReason reason) =>
            TheDefinedReasons.Contains(reason)
                ? reason
                : throw new ArgumentOutOfRangeException(nameof(reason), reason,
                    @$"The supplied reason {reason.ToString()} is not " +
                    @$"a defined value in the {typeof(EarlyReleaseReason).Name} enum.");

        internal static EarlyReleaseReason? ConvertIntToEnumIfDefinedElseNull(int val) => ((EarlyReleaseReason)val).ValueOrDefaultIfNDef();

        internal static EarlyReleaseReason? ConvertIntToEnumIfDefinedElseNull(int? val) =>
            val.HasValue ? ConvertIntToEnumIfDefinedElseNull(val.Value) : null;

        private static ImmutableArray<EarlyReleaseReason> InitDefinedReasons()
        {
            var arr = (EarlyReleaseReason[])Enum.GetValues(typeof(EarlyReleaseReason));
            return arr.ToImmutableArray();
        }

        private static readonly ImmutableArray<EarlyReleaseReason> TheDefinedReasons = InitDefinedReasons();

        
    }
}
