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

        static void SetMemberValue(ILGenerator il, FieldInfo field) {
            if (field.IsPrivate) {

                var fieldType = field.FieldType;
                var type = field.DeclaringType;

                if (fieldType.IsValueType)
                    il.Emit(OpCodes.Box, fieldType);

                il.Emit(OpCodes.Ldtoken, field);
                il.Emit(OpCodes.Call,
                    typeof(FieldInfo).GetMethod("GetFieldFromHandle",
                    new Type[] { typeof(RuntimeFieldHandle) }));

                il.Emit(OpCodes.Call, SetDynamicMemberValueMethod.MakeGenericMethod(type));
            } else
                il.Emit(OpCodes.Stfld, field);

        }

        delegate void SetDynamicMemberDelegate<T>(ref T instance, object value, FieldInfo field);

        public static void SetDynamicMemberValue<T>(ref T instance, object value, FieldInfo field) {
            
            (SetMemberValues.GetOrAdd(field, key =>
            {
                var fieldType = key.FieldType;

                var type = key.DeclaringType;
               
                var meth = new DynamicMethod(key.Name + "_setValue", VoidType, new[] { type.MakeByRefType(), ObjectType, FieldInfoType }, true);

                var il = meth.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);

                if (type.IsClass) 
                    il.Emit(OpCodes.Ldind_Ref);

                il.Emit(OpCodes.Ldarg_1);

                if (fieldType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, fieldType);
                else
                    il.Emit(OpCodes.Castclass, fieldType);

                il.Emit(OpCodes.Stfld, key);

                il.Emit(OpCodes.Ret);

                return meth.CreateDelegate(typeof(SetDynamicMemberDelegate<T>));
            }) as SetDynamicMemberDelegate<T>)(ref instance, value, field);
        }

        static void LoadMemberValue(ILGenerator il, MemberInfo member) {
            var method = member.MemberType == MemberTypes.Method ? member as MethodInfo : null;
            var field = member.MemberType == MemberTypes.Field ? member as FieldInfo : null;
            var isPrivate = method != null ? method.IsPrivate : field.IsPrivate;
            var fieldType = method != null ? method.ReturnType : field.FieldType;
            var type = member.DeclaringType;

            if (isPrivate && type.IsValueType) 
                    il.Emit(OpCodes.Box, type);

            if (method != null) {
                if (isPrivate) {
                    il.Emit(OpCodes.Ldtoken, method);
                    il.Emit(OpCodes.Call,
                        typeof(MethodBase).GetMethod("GetMethodFromHandle",
                        new Type[] { typeof(RuntimeMethodHandle) }));

                    il.Emit(OpCodes.Call, GetDynamicMemberValueMethod);
                } else
                    il.Emit(OpCodes.Callvirt, method);
            } else {
                if (isPrivate) {
                    il.Emit(OpCodes.Ldtoken, field);
                    il.Emit(OpCodes.Call,
                        typeof(FieldInfo).GetMethod("GetFieldFromHandle",
                        new Type[] { typeof(RuntimeFieldHandle) }));

                    il.Emit(OpCodes.Call, GetDynamicMemberValueMethod);
                } else
                    il.Emit(OpCodes.Ldfld, field);
            }
            if (isPrivate) {
                if (fieldType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, fieldType);
                else
                    il.Emit(OpCodes.Castclass, fieldType);
            }
        }

        public static object GetDynamicMemberValue(object instance, MemberInfo member) {
            return GetMemberValues.GetOrAdd(member, key =>
            {
                var method = key.MemberType == MemberTypes.Method ? member as MethodInfo : null;
                var field = key.MemberType == MemberTypes.Field ? member as FieldInfo : null;
                var memberType = method != null ? method.ReturnType : field.FieldType;
                var type = member.DeclaringType;

                var meth = new DynamicMethod(key.Name + "_getValue", ObjectType, new[] { ObjectType, MemberInfoType }, true);

                var il = meth.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);

                if (type.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, type);
                 else 
                    il.Emit(OpCodes.Isinst, type);
                
                if (method != null) {
                    il.Emit(OpCodes.Callvirt, method);
                } else {
                    il.Emit(OpCodes.Ldfld, field);
                }
                if (memberType.IsValueType)
                    il.Emit(OpCodes.Box, memberType);

                il.Emit(OpCodes.Ret);

                return meth.CreateDelegate(typeof(Func<object, MemberInfo, object>))
                    as Func<object, MemberInfo, object>;
            })(instance, member);
        }

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
                );
        }

        static bool IsNullable(this Type type) {
            return NullableTypes.GetOrAdd(type, key => 
                key.IsGenericType && key.GetGenericTypeDefinition() == NullableType);
        }

        static IEnumerable<FieldInfo> GetTypeFields(Type type) {
            IEnumerable<FieldInfo> fields;

            if (!TypeFields.TryGetValue(type, out fields)) {

                var baseTypes = new List<Type>();
                Type currentBase = type.BaseType;
                if (currentBase != null) baseTypes.Add(currentBase);

                while (currentBase != null && (currentBase = currentBase.BaseType) != null) 
                    baseTypes.Add(currentBase);
                
                TypeFields[type] = fields =
                    type.GetFields(FieldBinding).Union(baseTypes.SelectMany(x => x.GetFields(FieldBinding)))
                    .Where(f => f.GetCustomAttributes(IgnoreAttribute, true).Length < 1)
                    .Where(f => !f.IsInitOnly)
                    .OrderBy(f => f.MetadataToken);
            }
            return fields;
        }

        internal static ISerializer<T> GetSerializer<T>() {
            var type = typeof(T);
            var serializer = default(object);
            if (!SerializerTypes.TryGetValue(type, out serializer)) {
                lock (_lockObject) {
                    var serializerType = GenerateSerializer(type);
                    serializer = SerializerTypes[type] = Activator.CreateInstance(serializerType);
                }
            }
            return ((ISerializer<T>)serializer);
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
