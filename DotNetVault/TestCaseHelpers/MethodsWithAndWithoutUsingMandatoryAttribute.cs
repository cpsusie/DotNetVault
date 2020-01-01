using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using JetBrains.Annotations;
#pragma warning disable 1591 //NEED TO BE PUBLIC FOR UNIT TESTING PURPOSES (NOT SURE WHY)

namespace DotNetVault.TestCaseHelpers
{
    [UsedImplicitly]
    public static class MethodsWithAndWithoutUsingMandatoryAttribute
    {
        [return: UsingMandatory]
        public static RefStructsRoxor CreateDisposableRefStruct()
        {
            return RefStructsRoxor.CreateRefStruct();
        }

        public static void DoStuff()
        {
            var dt = Generate(() => DateTime.Now);
            if (dt < DateTime.Now) throw new Exception();

            DoStuff<StringBuilder, DateTime>(() => DateTime.Now);
        }

        public static void DoStuff<TFirst, TSecond>( Func<TSecond> make) where TFirst : new()
        {
            TSecond gen = Generate(make);  
            TFirst f = new TFirst();
            string x = "Hi mom!";
            if (ReferenceEquals(f, x)) throw new Exception();
            Console.WriteLine(gen);
        }

        public static T Generate<[VaultSafeTypeParam] T>(Func<T> value)
        {
            return value();
        }

        public static bool DoStuffWithStruct()
        {
            bool wasValid;
            using (var rsrxr = CreateDisposableRefStruct())
            {
                wasValid = rsrxr.IsValid;
                Console.WriteLine($@"RSRXR is valid: {wasValid}.");
            }
            return wasValid;
        }

        public static bool DoBadStuffWithStruct()
        {
            RefStructsRoxor badNaughty = CreateDisposableRefStruct();
            bool wasValid = badNaughty.IsValid;
            bool wasDisposedAtFirst = badNaughty.IsValid;
            Console.WriteLine($@"badNaughty was valid at first: {wasValid}.");
            Console.WriteLine($@"badNaughty was disposed at first: {wasDisposedAtFirst}.");
            badNaughty.Dispose();
            bool isValidAfterDispose = badNaughty.IsValid;
            bool isDisposedAfterDispose = badNaughty.IsDisposed;

            Console.WriteLine($@"badNaughty was valid after Dispose: {isValidAfterDispose}.");
            Console.WriteLine($@"badNaughty was disposed after Dispose: {isDisposedAfterDispose}.");

            return wasValid && !wasDisposedAtFirst && !isValidAfterDispose && isDisposedAfterDispose;
        }

        public static T NeedsToReturnVaultSafeParams<[VaultSafeTypeParam] T>(Func<T> genFunc)
        {
            return genFunc();
        }
    }

    public sealed class NeedsAVaultSafeParam<[VaultSafeTypeParam] T>
    {
        public string FullTypeName { get; }

        public NeedsAVaultSafeParam()
        {
            FullTypeName = typeof(T).FullName;
        }
    }


    public readonly ref struct RefStructsRoxor
    {
        internal static RefStructsRoxor CreateRefStruct()
        {
            RefStructsRoxor rsrxr = new RefStructsRoxor(false);
            if (!rsrxr.IsValid)
            {
                throw new InvalidOperationException();
            }
            return rsrxr;
        }

        public bool IsValid => _flag?.IsSet == false;

        public bool IsDisposed => _flag?.IsSet == true;
        
        // ReSharper disable once UnusedParameter.Local
        private RefStructsRoxor(bool _)
        {
            _flag = new SetOnceFlag();
        }

        public void Dispose()
        {
            _flag.TrySet();
        }

        private readonly SetOnceFlag _flag;
    }

    public sealed class SetOnceFlag
    {

        public bool IsSet => _set != NotSet;

        public bool TrySet()
        {
            int oldValue = Interlocked.CompareExchange(ref _set, Set, NotSet);
            bool ret = oldValue == NotSet;
            Debug.Assert(_set == Set);
            return ret;
        }

        private const int Set = 1;
        private const int NotSet = 0;
        private int _set = NotSet;
    }

}
