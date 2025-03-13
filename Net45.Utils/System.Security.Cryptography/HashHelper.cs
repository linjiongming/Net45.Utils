using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Security.Cryptography
{
    public static class HashHelper
    {
        public static string HashMD5(this byte[] data, string format = "X2")
        {
            return string.Concat(new MD5CryptoServiceProvider().ComputeHash(data).Select(x => x.ToString(format)));
        }
    }
}