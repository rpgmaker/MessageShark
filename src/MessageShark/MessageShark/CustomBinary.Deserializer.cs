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
            var str = System.Text.Encoding.UTF8.GetString(buffer);
            if (str == NullString) str = null;
            return str;
        }

        public static Guid BytesToGuid(byte[] buffer) {
            return new Guid(buffer);
        }

        public static decimal BytesToDecimal(byte[] buffer) {
            //if (buffer.Length == 1) return Decimal.Parse(((char)buffer[0]).ToString());
            if (buffer.Length == 2) {
                if (buffer[1] == 1) return decimal.MinValue;
                else return decimal.MaxValue;
            }
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

        public static int? BytesToNullableInt32(byte[] buffer) {
            return BytesToInt32(buffer);
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
            return buffer[0] == 1;
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
            if (buffer.Length == 2) {
                if (buffer[1] == 1)
                    return double.MinValue;
                else return double.MaxValue;
            }
            if (BitConverter.IsLittleEndian) {
                var temp = buffer[0];
                buffer[0] = buffer[7];
                buffer[7] = temp;

                temp = buffer[1];
                buffer[1] = buffer[6];
                buffer[6] = temp;

                temp = buffer[2];
                buffer[2] = buffer[5];
                buffer[5] = temp;

                temp = buffer[3];
                buffer[3] = buffer[4];
                buffer[4] = temp;
            }
            return BitConverter.ToDouble(buffer, 0);
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

        public static object BytesToObject(byte[] buffer) {
            object obj = null;
            var type = TypeIDMapping[ObjectType][buffer[0]];
            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == NullableType)
                    type = type.GetGenericArguments()[0];
            var buffer1 = new byte[buffer.Length - 1];
            Buffer.BlockCopy(buffer, 1, buffer1, 0, buffer1.Length);
            if (type == typeof(string)) {
                obj = BytesToString(buffer1);
            } else if (type == typeof(int)) {
                obj = BytesToInt32(buffer1);
            } else if (type == typeof(byte)) {
                obj = (byte)BytesToInt32(buffer1);
            } else if (type == typeof(DateTime)) {
                obj = BytesToDateTime(buffer1);
            } else if (type == typeof(bool)) {
                obj = BytesToBool(buffer1);
            } else if (type == typeof(char)) {
                obj = BytesToChar(buffer1);
            } else if (type == typeof(double)) {
                obj = BytesToDouble(buffer1);
            } else if (type == typeof(short)) {
                obj = BytesToInt16(buffer1);
            } else if (type == typeof(long)) {
                obj = BytesToInt64(buffer1);
            } else if (type == typeof(decimal)) {
                obj = BytesToDecimal(buffer1);
            } else if (type == typeof(float)) {
                obj = BytesToFloat(buffer1);
            } else if (type == typeof(ushort)) {
                obj = BytesToUInt16(buffer1);
            } else if (type == typeof(uint)) {
                obj = BytesToUInt32(buffer1);
            } else if (type == typeof(ulong)) {
                obj = BytesToUInt64(buffer1);
            } else if (type == typeof(Guid)) {
                obj = BytesToGuid(buffer1);
            } else if (type.IsEnum) {
                obj = BytesToEnum(buffer1, type);
            } else if (type == typeof(TimeSpan)) {
                obj = BytesToTimeSpan(buffer1);
            } else if (type == typeof(int?)) {
                obj = BytesToNullableInt32(buffer1);
            } else if (type == typeof(TimeSpan?)) {
                obj = BytesToNullableTimeSpan(buffer1);
            }
            return obj;
        }
    }
}
