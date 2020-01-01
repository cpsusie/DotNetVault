using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TResult>(in TInput input);

    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
        in TInput input, in TAncillary ancillary);

    public interface IDoStuff<[VaultSafeTypeParam] T>
    {
        string PrintMe();
    }

  

    class TestDynamicNeverConsideredVaultSafe<[VaultSafeTypeParam] T>
    {
        public string PrintMe() => _value.ToString();

        public TestDynamicNeverConsideredVaultSafe([NotNull] T value) =>
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            _value = value != null ? value : throw new ArgumentNullException(nameof(value));

        [NotNull] private readonly T _value;
    }

    [VaultSafe]
    sealed class TestDynamicNeverVsTwo
    {
        public string PrintMe() => _val.ToString();

        public TestDynamicNeverVsTwo([NotNull] dynamic obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            _val = obj;
        }

        [NotNull] private readonly dynamic _val;
    }

    public static class Tests
    {
        public static void ShouldNotBeAbleToDoThis()
        {
            dynamic value = DateTime.Now;
            TestDynamicNeverConsideredVaultSafe<dynamic> testMe = new TestDynamicNeverConsideredVaultSafe<dynamic>(value);
            Func<dynamic> dateTimeFactory = () => (dynamic) DateTime.Now;
            dynamic awesome = CreateObject(dateTimeFactory);
            Console.WriteLine(testMe.PrintMe());
            ShouldNotBeAbleToDoThisEither();
        }

        public static T CreateObject<[VaultSafeTypeParam] T>([NotNull] Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return factory();
        }

        public static void ShouldNotBeAbleToDoThisEither()
        {
            LocalQuery<StringBuilder, dynamic, string> vq = (in StringBuilder res, in dynamic d) =>
                {
                    res.AppendLine(d.ToString());
                    return res.ToString();
                };
            StringBuilder sb = new StringBuilder();
            dynamic timeStamp = DateTime.Now;

            string result = vq(in sb, in timeStamp);
            Console.WriteLine(result);
        }

        public static void LetsSneakAnArrayTestInHereJustForFun()
        {
            LocalQuery<StringBuilder, int[], string> vq = (in StringBuilder res, in int[] arr) =>
                {
                    foreach (var val in arr)
                    {
                        res.AppendLine(val.ToString());
                    }
                    return res.ToString();
                };

            int[] a = {1, 2, 3};
            StringBuilder sb = new StringBuilder("SFGKSKG S");
            Console.WriteLine(vq(in sb, in a));
        }
    }
}
