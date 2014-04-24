using System.Security.Cryptography;
using System.Text;

namespace Rust
{
    public static class Crypto
    {
        public static string GetMd5(string pass)
        {
            byte[] hash;
            using (MD5 md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(pass));
            }
            string md5Pass = ToHex(hash, false);
            return md5Pass;
        }

        private static string ToHex(byte[] bytes, bool upperCase)
        {
            var result = new StringBuilder(bytes.Length*2);

            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(i.ToString(upperCase ? "X2" : "x2"));
            }

            return result.ToString();
        }
    }
}