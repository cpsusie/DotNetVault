using System;
using DotNetVault.ClortonGame;

namespace ClortonGameDemo
{
    sealed class HpTimeStampProvider : TimeStampProvider
    {
        public static HpTimeStampProvider CreateInstance() 
            => new HpTimeStampProvider();

        public override DateTime Now => HpTimeStamps.TimeStampSource.Now;
        public override void Calibrate() =>
            HpTimeStamps.TimeStampSource.Calibrate();

        private HpTimeStampProvider() { }
    }
}
