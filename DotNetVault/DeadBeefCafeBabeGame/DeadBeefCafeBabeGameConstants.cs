namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Constants used by the dead beef cafe babe game
    /// </summary>
    public readonly struct DeadBeefCafeBabeGameConstants : IDeadBeefCafeBabeGameConstants
    {
        /// <inheritdoc />
        public ref readonly UInt256 LookForNumber => ref TheLookForNumber;

        /// <inheritdoc />
        public ref readonly UInt256 XNumber => ref TheXNumber;

        /// <inheritdoc />
        public ref readonly UInt256 ONumber => ref TheONumber;


        private static readonly UInt256  TheLookForNumber = new UInt256(TheLookForNumberBase, TheLookForNumberBase, TheLookForNumberBase, TheLookForNumberBase);
        private static readonly UInt256 TheXNumber = new UInt256(TheXNumberBase, TheXNumberBase, TheXNumberBase, TheXNumberBase);
        private static readonly UInt256 TheONumber = new UInt256(TheONumberBase, TheONumberBase, TheONumberBase, TheONumberBase);
        private const ulong TheLookForNumberBase = 0xdead_beef_cafe_babe;
        private const ulong TheXNumberBase = 0xface_c0ca_f00d_bad0;
        private const ulong TheONumberBase = 0xc0de_d00d_fea2_b00b;
    }
}