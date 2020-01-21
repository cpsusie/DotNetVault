using System;
using JetBrains.Annotations;

namespace DotNetVault.TestCaseHelpers
{
    /// <summary>
    /// Facilitates unit testing
    /// </summary>
    public abstract class WouldBeVaultSafeIfSealed
    {
        /// <summary>
        /// Age
        /// </summary>
        public int Age => _age;

        /// <summary>
        /// Name, nut null
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="name"></param>
        /// <param name="age"></param>
        protected WouldBeVaultSafeIfSealed([NotNull] string name, int age)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _age = age;
        }

        /// <summary>
        /// Make a new object ... same values new name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>a new object</returns>
        /// <exception cref="ArgumentNullException"></exception>
        protected abstract WouldBeVaultSafeIfSealed WithNewName([NotNull] string name);

        /// <summary>
        /// Make a new object ... same values new age
        /// </summary>
        /// <param name="age"></param>
        /// <returns>a new object</returns>
        protected abstract WouldBeVaultSafeIfSealed WithNewAge(int age);


        private readonly int _age;
        [NotNull] private readonly string _name;
    }
}
