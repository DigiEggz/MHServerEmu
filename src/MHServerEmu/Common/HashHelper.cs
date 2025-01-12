﻿using System.Text;

namespace MHServerEmu.Common
{
    public static class HashHelper
    {
        public static uint Adler32(string str)
        {
            const int mod = 65521;
            uint a = 1, b = 0;
            foreach (char c in str)
            {
                a = (a + c) % mod;
                b = (b + a) % mod;
            }
            return (b << 16) | a;
        }

        public static uint Crc32(byte[] bytes)
        {
            byte[] hash = System.IO.Hashing.Crc32.Hash(bytes);
            return BitConverter.ToUInt32(hash);
        }

        public static uint Crc32(string str) => Crc32(Encoding.UTF8.GetBytes(str));

        public static uint Djb2(string str)
        {
            uint hash = 5381;
            for (int i = 0; i < str.Length; i++)
                hash = (hash << 5) + hash + ((byte)str[i]);
            return hash;
        }

        /// <summary>
        /// Hashes a path with Adler32 and Crc32 to make a ulong value that can be used as a DataRef.
        /// </summary>
        /// <param name="path">Path to hash.</param>
        /// <returns>Hashed path.</returns>
        public static ulong HashPath(string path)
        {
            // Hashes generated by this method are used as dataRefs for prototypes.
            // Calligraphy and resource prototypes prepare paths for hashing differently:
            //      Calligraphy:    1) Replace '.' with '?' 2) Replace '/' with '.'
            //      Resource:       Insert '&' as the first char
            // This may be done to avoid hash collisions.

            path = path.ToLower();
            ulong adler = Adler32(path);
            ulong crc = Crc32(path);
            return (adler | (crc << 32)) - 1;
        }
    }
}
