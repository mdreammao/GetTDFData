using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography;
using System.IO;

namespace OrientWebGW.Client
{
    public static class EncryptHelper
    {
        public static string RSAEncrypt(string publicKey, string target)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKey);
            var targetBytes = new UnicodeEncoding().GetBytes(target);
            var midBytes = rsa.Encrypt(targetBytes, false);

            var result = Convert.ToBase64String(midBytes);

            return result;
        }

    }
}
