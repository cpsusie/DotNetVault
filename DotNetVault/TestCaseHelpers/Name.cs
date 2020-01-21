using System;
using DotNetVault.Attributes;
using DotNetVault.ToggleFlags;
using JetBrains.Annotations;
#pragma warning disable 1591 //-- THIS ITEM IS USED SOLELY FOR UNIT TESTING

namespace DotNetVault.TestCaseHelpers
{
    /// <summary>
    /// SOLELY FOR UNIT TESTING
    /// </summary>
    public readonly ref struct Name
    {
        [return: UsingMandatory]
        public static Name CreateName([NotNull] string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(@"Name may not be whitespace or empty.", nameof(name));
            return new Name(name);
        }

        [NotNull] public string Text => _s ?? string.Empty;
        public bool IsDisposed => _tf?.IsSet != false;

        [NoDirectInvoke]
        public void Dispose() =>
            _tf?.SetFlag();
        
        private Name([NotNull] string name)
        {
            _s = name ?? throw new ArgumentNullException(nameof(name));
            _tf = new ToggleFlag(false);
        }

        private readonly ToggleFlag _tf; 
        private readonly string _s;
    }
}
