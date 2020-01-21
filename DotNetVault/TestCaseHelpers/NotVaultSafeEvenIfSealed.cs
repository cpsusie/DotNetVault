using System;
using System.Text;
using JetBrains.Annotations;

namespace DotNetVault.TestCaseHelpers
{
    /// <summary>
    /// Facilitates unit testing
    /// </summary>
    public class NotVaultSafeEvenIfSealed
    {
        /// <summary>
        /// Age
        /// </summary>
        public int Age => _age;
        /// <summary>
        /// Name 
        /// </summary>
        [NotNull]
        public string Name => _sb.ToString(); 

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="name">name</param>
        /// <param name="age">age </param>
        /// <exception cref="ArgumentNullException"></exception>
        public NotVaultSafeEvenIfSealed([NotNull] string name, int age)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            _sb = new StringBuilder(name);
            _age = age;
        }

        private int _age;
        [NotNull] private readonly StringBuilder _sb;
    }
}
