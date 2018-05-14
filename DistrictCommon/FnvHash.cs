using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistrictCommon
{
    public sealed class FnvHash
    {
        private const ulong FnvPrime = 1099511628211;
        private const ulong FnvOffsetBasis = 14695981039346656037;

        public static long Hash(string value)
        {
            return Hash(Encoding.UTF8.GetBytes(value));
        }

        public static long Hash(byte[] value)
        {
            ulong hash = FnvOffsetBasis;
            for (int i = 0; i < value.Length; ++i)
            {
                hash ^= value[i];
                hash *= FnvPrime;
            }

            return (long)hash;
        }
    }
}
