using System;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public struct BvIllegalRefExprTestCase1
    {
        
        public DateTime TimeStamp { get; set; }

        [NotNull]
        public string Name
        {
            get => _name ??= string.Empty;
            set => _name = value ?? throw new ArgumentNullException(nameof(value));
        }

        public BvIllegalRefExprTestCase1([NotNull] string name) 
            : this(DateTime.Now, name) { }

        public BvIllegalRefExprTestCase1(DateTime timestamp, [NotNull] string name)
        {
            TimeStamp = timestamp;
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public override string ToString() =>
            $"[{typeof(BvIllegalRefExprTestCase1).Name}] -- Timestamp: [{TimeStamp:O}], Name: [{Name}]";

        private string _name;
    }

    public static class TestRoutine
    {
        public static void DoTest()
        {
            BasicVault<BvIllegalRefExprTestCase1> vault = new BasicVault<BvIllegalRefExprTestCase1>(new BvIllegalRefExprTestCase1("Foobar"));
            {
                using var lck = vault.SpinLock();
                ref var toodles = ref lck.Value;
                Console.WriteLine(toodles.ToString());
            }
        }
    }
}
