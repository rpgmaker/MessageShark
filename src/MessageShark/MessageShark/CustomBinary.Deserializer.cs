using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MessageShark {
    public static partial class CustomBinary {
        public static DateTime BytesToDateTime(byte[] buffer) {
            return new DateTime(BitConverter.ToInt64(buffer, 0));
        }

        public static string BytesToString(byte[] buffer) {
            return System.Text.Encoding.UTF8.GetString(buffer);
        }

        public static Guid BytesToGuid(byte[] buffer) {
            return new Guid(buffer);
        }

        public static decimal BytesToDecimal(byte[] buffer) {
            if (buffer.Length == 1) return Decimal.Parse(((char)buffer[0]).ToString());
            using (var ms = new MemoryStream(buffer)) {
                using (var br = new BinaryReader(ms)) {
                    return br.ReadDecimal(); ;
                }
            }
        }

        public static Enum BytesToEnum(byte[] buffer, Type type) {
            return Enum.ToObject(type, BytesToInt32(buffer)) as Enum;
        }

        public static byte BytesToByte(byte[] buffer) {
            return buffer[0];
        }

        public static TimeSpan? BytesToNullableTimeSpan(byte[] buffer) {
            return BytesToTimeSpan(buffer);
        }

        public static TimeSpan BytesToTimeSpan(byte[] buffer) {
            if (buffer.Length == 1) {
                if (buffer[0] == 0) return TimeSpan.MinValue;
                else return TimeSpan.MaxValue;
            } else if (buffer.Length == 2) {
                return TimeSpan.FromTicks(TimeSpanTicks[buffer[0]] * buffer[1]);
            } 
            var buffer2 = new byte[buffer.Length - 1];
            Buffer.BlockCopy(buffer, 1, buffer2, 0, buffer2.Length);
            var ticks = BytesToInt64(buffer2);
            return TimeSpan.FromTicks(buffer[0] == TimeSpanTicksLength ? ticks : ticks * TimeSpanTicks[buffer[0]]);
        }

        public static char BytesToChar(byte[] buffer) {
            return (char)BytesToInt16(buffer);
        }

        public static bool BytesToBool(byte[] buffer) {
            return true;
        }

        public static byte[] BytesToIntegerBytes(byte[] buffer, int size) {
            var buffer2 = new byte[size];
            if (buffer.Length == size)
                buffer2 = buffer;
            else
                for (var i = 0; i < buffer.Length; i++)
                    buffer2[i] = buffer[i];
            return buffer2;
        }

        public static double BytesToDouble(byte[] buffer) {
            return BitConverter.ToDouble(BytesToIntegerBytes(buffer, 8), 0);
        }

        public static float BytesToFloat(byte[] buffer) {
            return BitConverter.ToSingle(BytesToIntegerBytes(buffer, 4), 0);
        }

        public static short BytesToInt16(byte[] buffer) {
            return BitConverter.ToInt16(BytesToIntegerBytes(buffer, 2), 0);
        }

        public static int BytesToInt32(byte[] buffer) {
            return BitConverter.ToInt32(BytesToIntegerBytes(buffer, 4), 0);
        }

        public static long BytesToInt64(byte[] buffer) {
            return BitConverter.ToInt64(BytesToIntegerBytes(buffer, 8), 0);
        }

        public static ushort BytesToUInt16(byte[] buffer) {
            return BitConverter.ToUInt16(BytesToIntegerBytes(buffer, 2), 0);
        }

        public static uint BytesToUInt32(byte[] buffer) {
            return BitConverter.ToUInt32(BytesToIntegerBytes(buffer, 4), 0);
        }

        public static ulong BytesToUInt64(byte[] buffer) {
            return BitConverter.ToUInt64(BytesToIntegerBytes(buffer, 8), 0);
        }
    }
}
