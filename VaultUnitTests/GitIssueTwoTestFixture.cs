using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Vaults;

namespace VaultUnitTests
{
    public sealed class GitIssueTwoTestFixture
    {
            public void AppendToVault(string appendMe)
            {
                using var lck = StringVault.Lock();
                lck.Append(appendMe ?? string.Empty);
            }

            public int GetCount()
            {
                using var lck = StringVault.RoLock();
                return lck.Length;
            }

            public (bool Extracted, string Contents) ExtractContentsAndClearIfCountGreaterThanThen(int minExtractSize)
            {
                bool extracted;
                string contents = string.Empty;
                using var lck = StringVault.UpgradableRoLock();
                //using var lck = StringVault.UpgradableRoLockBlockUntilAcquired();
                extracted = (lck.Length >= minExtractSize);
                if (extracted)
                {
                    using var writeLck = lck.LockBlockUntilAcquired();
                    //using var writeLck = lck.Lock();
                    contents = writeLck.ToString();
                    writeLck.Clear();
                }

                return (extracted, contents);
            }
            public readonly ReadWriteStringBufferVault StringVault = new ReadWriteStringBufferVault(TimeSpan.FromMilliseconds(100), () => new StringBuilder());
    }

}
