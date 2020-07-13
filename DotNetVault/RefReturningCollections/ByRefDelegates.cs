using DotNetVault.Attributes;

namespace DotNetVault.RefReturningCollections
{

    /// <summary>
    /// Perform a potentially mutating action in place on the value specified by T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="firstParam">the value.</param>
    [NoNonVsCapture]
    public delegate void MutatingRefAction<[VaultSafeTypeParam] T>(ref T firstParam);

    /// <summary>
    /// An action that accepts parameter by constant reference
    /// </summary>
    /// <typeparam name="T">The type of the input param.</typeparam>
    /// <param name="firstParam">The first param.</param>
    [NoNonVsCapture]
    public delegate void RefAction<[VaultSafeTypeParam] T>(in T firstParam);
    /// <summary>
    /// An action that accepts parameter by constant reference
    /// </summary>
    /// <typeparam name="T1">The type of the first input param.</typeparam>
    /// <typeparam name="T2">The type of the second input param.</typeparam>
    /// <param name="firstParam">The first param.</param>
    /// <param name="secondParam">The second param.</param>
    [NoNonVsCapture]
    public delegate void RefAction<[VaultSafeTypeParam] T1, [VaultSafeTypeParam] T2>(in T1 firstParam, in T2 secondParam);
    /// <summary>
    /// An action that accepts parameter by constant reference
    /// </summary>
    /// <typeparam name="T1">The type of the first input param.</typeparam>
    /// <typeparam name="T2">The type of the second input param.</typeparam>
    /// <typeparam name="T3">The type of the third input param.</typeparam>
    /// <param name="firstParam">The first param.</param>
    /// <param name="secondParam">The second param.</param>
    /// <param name="thirdParam">The third param.</param>
    [NoNonVsCapture]
    public delegate void RefAction<[VaultSafeTypeParam] T1, [VaultSafeTypeParam] T2, [VaultSafeTypeParam] T3>(in T1 firstParam, in T2 secondParam, in T3 thirdParam);
    /// <summary>
    /// An action that accepts parameter by constant reference
    /// </summary>
    /// <typeparam name="T1">The type of the first input param.</typeparam>
    /// <typeparam name="T2">The type of the second input param.</typeparam>
    /// <typeparam name="T3">The type of the third input param.</typeparam>
    /// <typeparam name="T4">The type of the fourth input param.</typeparam>
    /// <param name="firstParam">The first param.</param>
    /// <param name="secondParam">The second param.</param>
    /// <param name="thirdParam">The third param.</param>
    /// <param name="fourthParam">The fourth param.</param>
    [NoNonVsCapture]
    public delegate void RefAction<T1, [VaultSafeTypeParam] T2, [VaultSafeTypeParam] T3, [VaultSafeTypeParam] T4>(in T1 firstParam, in T2 secondParam, in T3 thirdParam, in T4 fourthParam);

    /// <summary>
    /// An action that accepts parameter by constant reference
    /// </summary>
    /// <typeparam name="T1">The type of the first input param.</typeparam>
    /// <typeparam name="T2">The type of the second input param.</typeparam>
    /// <typeparam name="T3">The type of the third input param.</typeparam>
    /// <typeparam name="T4">The type of the fourth input param.</typeparam>
    /// <typeparam name="T5">The type of the fifth input param</typeparam>
    /// <param name="firstParam">The first param.</param>
    /// <param name="secondParam">The second param.</param>
    /// <param name="thirdParam">The third param.</param>
    /// <param name="fourthParam">The fourth param.</param>
    /// <param name="fifthParam">The fifth param.</param>
    [NoNonVsCapture]
    public delegate void RefAction<T1, [VaultSafeTypeParam] T2, [VaultSafeTypeParam] T3, [VaultSafeTypeParam] T4, [VaultSafeTypeParam] T5>(in T1 firstParam, in T2 secondParam, in T3 thirdParam, in T4 fourthParam, in T5 fifthParam);

    /// <summary>
    /// A function that accepts its input parameter by constant reference
    /// </summary>
    /// <typeparam name="TIn">The input param type.</typeparam>
    /// <typeparam name="TOut">The return type</typeparam>
    /// <param name="firstParam">the input param.</param>
    /// <returns>A value of <typeparamref name="TOut"/> as performed by the delegate accepting
    /// a <typeparamref name="TIn"/> as an input param.</returns>
    [NoNonVsCapture]
    public delegate TOut RefFunc<[VaultSafeTypeParam] TIn, [VaultSafeTypeParam] TOut>(in TIn firstParam);
    /// <summary>
    /// A predicate that received the input value by constant reference
    /// </summary>
    /// <typeparam name="TIn">The type evaluated by the predicate</typeparam>
    /// <param name="paramToEvaluate">the value to apply the predicate true</param>
    /// <returns>True if the predicate is true of <paramref name="paramToEvaluate"/>, false otherwise.</returns>
    [NoNonVsCapture]
    public delegate bool RefPredicate<[VaultSafeTypeParam] TIn>(in TIn paramToEvaluate);

    /// <summary>
    /// A comparison that received its parameters by constant reference
    /// </summary>
    /// <typeparam name="T">the type on which a by reference comparison should be performed</typeparam>
    /// <param name="lhs">the left hand comparand</param>
    /// <param name="rhs">the right hand comparand</param>
    /// <returns>Negative number if <paramref name="lhs"/> is less than <paramref name="rhs"/>, 0 if they are equal, a positive value
    /// if <paramref name="lhs"/> is greater than <paramref name="rhs"/>.</returns>
    [NoNonVsCapture]
    public delegate int RefComparison<[VaultSafeTypeParam] T>(in T lhs, in T rhs);
}
