using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    public static class Test
    {
        public static void TryStuff()
        {
            ReadOnlyWorkTestRoView viewMom = 
                new ReadOnlyWorkTestRoView(new ReadonlyWorkTest("Hi mom!"));
            ReadOnlyWorkTestRoView viewDad = new ReadOnlyWorkTestRoView(new ReadonlyWorkTest("Hi Dad!"));
            Console.WriteLine(viewMom.Test.TimeStamp);
            Console.WriteLine(viewDad.Test.Text);
            
            //viewDad.Test.TimeStamp = DateTime.Now;
            //viewDad.Test.Text = "eat me!";
        }
    }
    public sealed class ReadOnlyWorkTestRoView
    {
        public ref readonly ReadonlyWorkTest Test => ref _test;

        public ReadOnlyWorkTestRoView(in ReadonlyWorkTest test) => _test = test;

        private readonly ReadonlyWorkTest _test;
    }

    [VaultSafe]
    public struct ReadonlyWorkTest : IEquatable<ReadonlyWorkTest>
    {
        public readonly DateTime TimeStamp => _ts;

        public string Text
        {
            get => _text ?? string.Empty;
            set => _text = value ?? throw new ArgumentNullException(nameof(value));
        }

        public ReadonlyWorkTest([NotNull] string text)
        {
            _ts = DateTime.Now;
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public void MutateTimeStamp(DateTime newTs) => _ts = newTs;

        public override readonly string ToString() => $"{nameof(ReadonlyWorkTest)}- Stamp: [{_ts:O}], Text: [{_text ?? string.Empty}]";
        public readonly bool Equals(ReadonlyWorkTest other) => this == other;
        public static bool operator ==(in ReadonlyWorkTest lhs, in ReadonlyWorkTest rhs) =>
            (lhs._text ?? string.Empty) == (rhs._text ?? string.Empty) && lhs._ts == rhs._ts;
        public static bool operator !=(ReadonlyWorkTest lhs, ReadonlyWorkTest rhs) => !(lhs == rhs);
        public override readonly bool Equals(object obj) => obj is ReadonlyWorkTest rowt && rowt == this;
        public override readonly int GetHashCode() => _ts.GetHashCode();

        private string _text;
        private DateTime _ts;
    }
}
