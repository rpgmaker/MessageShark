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

        private static object _lockObject = new object();

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
            type = type.GetNonNullableType();
            if (type.IsEnum) type = EnumType;
            return BufferedTypes.Contains(type) || !PrimitiveWriterMethods.ContainsKey(type) ? 1 : 0;
        }

        static bool IsComplexType(this Type type) {
            type = type.GetNonNullableType();
            if (type.IsEnum) type = EnumType;
            return
                !PrimitiveWriterMethods.ContainsKey(type) ||
                DictType.IsAssignableFrom(type)
                || ListType.IsAssignableFrom(type);
        }

        static Type GetNonNullableType(this Type type) {
            return NonNullableTypes.GetOrAdd(type, key =>
            {
                if (key.IsGenericType &&
                        key.GetGenericTypeDefinition() == NullableType)
                    return key.GetGenericArguments()[0];
                return key;
            });
        }

        public static T GetNullableValue<T>(this Nullable<T> nullable) where T : struct {
            if (nullable.HasValue) return nullable.Value;
            return default(T);
        }

        static ConstructorInfo GetNullableTypeCtor(this Type type) {
            return NullableTypeCtors.GetOrAdd(type, key => key.GetConstructors()[0]);
        }

        public static MethodInfo GetNullableHasValueMethod(this Type type) {
            type = type.GetNonNullableType();
            return NullableHasValueMethods.GetOrAdd(type, key =>
                NullableType.MakeGenericType(key).GetProperty("HasValue").GetGetMethod());
        }

        static MethodInfo GetNullableValueMethod(this Type type) {
            type = type.GetNonNullableType();
            return NullableMethods.GetOrAdd(type, key =>
                GetNullableValueMethodMethod.MakeGenericMethod(key)
                //NullableType.MakeGenericType(key).GetProperty("Value").GetGetMethod()
                );
        }

        static bool IsNullable(this Type type) {
            return NullableTypes.GetOrAdd(type, key => 
                key.IsGenericType && key.GetGenericTypeDefinition() == NullableType);
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

        private static class MessageSharkCachedSerializer<T> {
            public static readonly ISerializer<T> Serializer = (ISerializer<T>)Activator.CreateInstance(GenerateSerializer(typeof(T)));
        }

        internal static ISerializer<T> GetSerializer<T>() {
            return MessageSharkCachedSerializer<T>.Serializer;
        }

        public static byte[] ReadFully(Stream input) {
            var m = input as MemoryStream;
            if (m != null)
                return m.ToArray();
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream()) {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
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
