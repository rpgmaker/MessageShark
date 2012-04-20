using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace MessageShark {
    public static partial class CustomBinary {

        public static byte[] EncodeLength(int length, int tag) {
            return tag <= 15 ? new byte[] { (byte)((tag << 4) | length) } : new byte[] { (byte)length, (byte)tag };
        }
       
        public static Func<object> ExpressionNew(Type type) {
            Func<object> func;
            if (DictFuncNew.TryGetValue(type, out func)) return func;
            var method = Expression.Lambda<Func<object>>(Expression.New(type.GetConstructor(Type.EmptyTypes))).Compile();
            DictFuncNew[type] = method;
            return method;
        }

        static bool IsListType(this Type type) {
            return ListType.IsAssignableFrom(type) || type.Name == "IList`1";
        }

        static bool IsDictionaryType(this Type type) {
            return DictType.IsAssignableFrom(type) || type.Name == "IDictionary`2";
        }

        static bool IsClassType(this Type type) {
            return !type.IsCollectionType();
        }

        static bool IsCollectionType(this Type type) {
            return type.IsListType() || type.IsDictionaryType();
        }

        public static object CreateInstance(Type type) {
            return ExpressionNew(type)();
        }

        static int IsBufferedTypeInt(this Type type) {
            if (type.IsEnum) type = EnumType;
            return BufferedTypes.Contains(type) || !PrimitiveWriterMethods.ContainsKey(type) ? 1 : 0;
        }

        static bool IsComplexType(this Type type) {
            if (type.IsEnum) type = EnumType;
            return
                !PrimitiveWriterMethods.ContainsKey(type) ||
                DictType.IsAssignableFrom(type)
                || ListType.IsAssignableFrom(type);
        }



        static IEnumerable<PropertyInfo> GetTypeProperties(Type type) {
            IEnumerable<PropertyInfo> props;
            if (!TypeProperties.TryGetValue(type, out props))
                TypeProperties[type] = props =
                    type.GetProperties(PropertyBinding)
                    .Where(p => p.GetCustomAttributes(IgnoreAttribute, true).Length < 1)
                    .OrderBy(p => p.Name);
            return props;
        }

        internal static ISerializer<T> GetSerializer<T>() where T : class {
            var type = typeof(T);
            var serializer = default(object);
            if (!SerializerTypes.TryGetValue(type, out serializer)) {
                var serializerType = GenerateSerializer(type);
                serializer = SerializerTypes[type] = Activator.CreateInstance(serializerType);
            }
            return (serializer as ISerializer<T>);
        }
        
        public static bool IsNextTagForPropertyTag(int tag, byte[] buffer, int startIndex) {
            if (startIndex > buffer.Length - 1) return false;
            var readByte = buffer[startIndex];
            var isNextTag = (tag <= 15 && tag == ((readByte & MSB) >> 4)) || (tag > 15 && tag == buffer[startIndex + 1]);
            return isNextTag;
        }
        
        public static void MoveToNextBytes(int tag, ref int startIndex) {
            if (tag <= 15) startIndex++;
            else startIndex += 2;
        }

        public static object CreateInstanceForConcreteType(byte[] buffer, Type type, ref int startIndex) {
            return CreateInstance(TypeIDMapping[type][buffer[startIndex++]]);
        }

        public static Type ConvertBaseToConcreteType(byte[] buffer, Type type, ref int startIndex) {
            if (TypeMapping.ContainsKey(type))
                return TypeIDMapping[type][buffer[startIndex++]];
            return type;
        }

        public static int GetCollectionLength(byte[] buffer, ref int startIndex, int tag) {
            return BytesToInt32(ReadNextBytes(buffer, ref startIndex, tag, false));
        }

        public static int GetNextLength(byte[] buffer, ref int startIndex, int tag, bool hasBufferLength) {
            var length = 0;
            var size = 0;
            if (tag <= 15)
                length = size = buffer[startIndex++] & LSB;
            else {
                length = size = buffer[startIndex++];
                startIndex++;
            }
            if (hasBufferLength) {
                var bufferIndex = 0;
                var readSize = startIndex + size;
                var lengthBuffer = new byte[LENGTH_BUFFER_SIZE];

                for (var i = startIndex; i < readSize; i++)
                    lengthBuffer[bufferIndex++] = buffer[i];

                startIndex += size;
                length = BitConverter.ToInt32(lengthBuffer, 0);
            }
            return length;
        }

        static void ReverseEx(this byte[] buffer) {
            var size = buffer.Length;
            var midpoint = size / 2;
            for (int i = 0; i < midpoint; i++) {
                var tmp = buffer[i];
                buffer[i] = buffer[size - i - 1];
                buffer[size - i - 1] = tmp;
            }
        }

        public static byte[] ReadNextBytes(byte[] buffer, ref int startIndex, int tag, bool hasBufferLength) {
            var length = GetNextLength(buffer, ref startIndex, tag, hasBufferLength);
            var index = 0;
            var bufferLength = buffer.Length;
            var nextBufferLength = startIndex + length;
            var nextBuffer = new byte[length];
            for (var i = startIndex; i < nextBufferLength; i++) {
                if (i > bufferLength - 1) break;
                nextBuffer[index++] = buffer[i];
            }
            startIndex += length;
            return nextBuffer;
        }
    }
}
