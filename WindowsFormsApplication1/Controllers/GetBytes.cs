using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FordDiag.Controllers
{
    internal static class GetBytes
    {
        public static List<byte> Int8(int integer)
        {
            return new List<byte> { (byte)integer };
        }

        public static List<byte> Int16(int integer)
        {
            return new List<byte> { (byte)(integer >> 8),
                                    (byte)integer };
        }

        public static List<byte> Int24(int integer)
        {
            return new List<byte> { (byte)(integer >> 16),
                                    (byte)(integer >> 8),
                                    (byte)integer };
        }

        public static List<byte> Int32(int integer)
        {
            return new List<byte> { (byte)(integer >> 24),
                                    (byte)(integer >> 16),
                                    (byte)(integer >> 8),
                                    (byte)integer };
        }
    }
}
