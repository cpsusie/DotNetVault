using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

// ReSharper disable All

namespace DotNetVault.Test.TestCases
{
    //No diagnostic: type is vault-safe
    [VaultSafe]
    public sealed class Cat
    {
        public int Age { get; }

        public string Name { get; }

        public Cat([NotNull] string name, int age)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Every cat deserves a non-empty name that is more than just whitespace.",
                    nameof(name));
            Name = name;
            Age = age > -1
                ? age
                : throw new ArgumentOutOfRangeException(nameof(age), age, @"A negative age is non-sensical.");
        }
    }
}
