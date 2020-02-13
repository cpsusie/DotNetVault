using System;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public struct BvIllegalRefExprTestCase2
    {
        public DateTime TimeStamp { get; set; }

        [NotNull]
        public string Name
        {
            get => _name ??= string.Empty;
            set => _name = value ?? throw new ArgumentNullException(nameof(value));
        }

        public BvIllegalRefExprTestCase2([NotNull] string name)
            : this(DateTime.Now, name) { }

        public BvIllegalRefExprTestCase2(DateTime timestamp, [NotNull] string name)
        {
            TimeStamp = timestamp;
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public override string ToString() =>
            $"[{typeof(BvIllegalRefExprTestCase2).Name}] -- Timestamp: [{TimeStamp:O}], Name: [{Name}]";

        private string _name;
    }

    public ref struct TestLrObj
    {
        [return: UsingMandatory]
        public static TestLrObj Create([NotNull] string name)
            => new TestLrObj(name ?? throw new ArgumentNullException(nameof(name)));
        
        
        [BasicVaultProtectedResource]
        public ref BvIllegalRefExprTestCase2 Value => ref _box.Value;
        [NoDirectInvoke]
        public void Dispose() => _box?.Dispose();

        private TestLrObj(string name)
        {
            _box = Vault<BvIllegalRefExprTestCase2>.Box.CreateBox();
            _box.Value = new BvIllegalRefExprTestCase2(name);
        }
        private readonly Vault<BvIllegalRefExprTestCase2>.Box _box;
         
    }

    public static class TestRoutine
    {
        public static void Test()
        {
            using var lck = TestLrObj.Create("fizzbuzz");
            ref var alias = ref lck.Value;
            alias.Name += " bazzdum!";
            Console.WriteLine(alias.ToString());
        }
    }
}
