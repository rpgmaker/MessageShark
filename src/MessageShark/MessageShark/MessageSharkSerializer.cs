using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;

namespace MessageShark {
    public static class MessageSharkSerializer {
        /// <summary>
        /// Serialize value into a byte array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] Serialize<T>(T value) {
            if (value is ICollection)
                return CustomBinary.GetSerializer<InternalWrapper<T>>().Serialize(new InternalWrapper<T> { Value = value });
            return CustomBinary.GetSerializer<T>().Serialize(value);
        }

        /// <summary>
        /// Serialize value to specified stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="stream"></param>
        public static void Serialize<T>(T value, Stream stream) {
            if (value is ICollection)
                CustomBinary.GetSerializer<InternalWrapper<T>>().Serialize(new InternalWrapper<T> { Value = value }, stream);
            else
                CustomBinary.GetSerializer<T>().Serialize(value, stream);
        }

        private static bool _generateAssembly = false;

        public static bool GenerateAssembly {
            internal get {
                return _generateAssembly;
            }
            set {
                _generateAssembly = value;
            }
        }


        private static bool IsCollectionAssignable(this Type type) {
            return CustomBinary.AssignableTypes.GetOrAdd(type, key => {
                return CustomBinary.ICollectionType.IsAssignableFrom(key);
            });
        }

        /// <summary>
        /// Precompile the specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void Compile<T>() {
            if (typeof(T).IsCollectionAssignable()) {
                CustomBinary.GetSerializer<InternalWrapper<T>>();
                return;
            }
            CustomBinary.GetSerializer<T>();
        }


        delegate object DeserializeWithTypeDelegate(byte[] value, int _);
        delegate byte[] SerializeWithTypeDelegate(object value);

        delegate void SerializeStreamWithTypeDelegate(object value, Stream stream);

        delegate object DeserializeStreamWithTypeDelegate(Stream stream, int _);

        static ConcurrentDictionary<string, DeserializeWithTypeDelegate> _deserializeWithTypes =
            new ConcurrentDictionary<string, DeserializeWithTypeDelegate>();

        static ConcurrentDictionary<string, DeserializeStreamWithTypeDelegate> _deserializeStreamWithTypes =
            new ConcurrentDictionary<string, DeserializeStreamWithTypeDelegate>();

        static ConcurrentDictionary<Type, SerializeWithTypeDelegate> _serializeWithTypes =
            new ConcurrentDictionary<Type, SerializeWithTypeDelegate>();

        static ConcurrentDictionary<Type, SerializeStreamWithTypeDelegate> _serializeStreamWithTypes =
            new ConcurrentDictionary<Type, SerializeStreamWithTypeDelegate>(); 

        static Type _messageSharkSerializerType = typeof(ISerializer<>);
        static Type _customBinaryType = typeof(CustomBinary);
        static MethodInfo _getSerializerMethod = _customBinaryType.GetMethod("GetSerializer", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly string SerializeStr = "Serialize", DeserializeStr = "Deserialize", StreamStr = "Stream";

        /// <summary>
        /// Serialize value to stream using specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="stream"></param>
        public static void Serialize(Type type, object value, Stream stream) {
            _serializeStreamWithTypes.GetOrAdd(type, _ => {
                var name = String.Concat(SerializeStr, StreamStr, type.FullName);
                var method = new DynamicMethod(name, CustomBinary.VoidType, new[] { CustomBinary.ObjectType, CustomBinary.StreamType }, restrictedSkipVisibility: true);

                var il = method.GetILGenerator();
                var genericMethod = _getSerializerMethod.MakeGenericMethod(type);
                var genericType = _messageSharkSerializerType.MakeGenericType(type);

                var genericSerialize = genericType.GetMethod(SerializeStr, new[] { type, CustomBinary.StreamType });

                il.Emit(OpCodes.Call, genericMethod);

                il.Emit(OpCodes.Ldarg_0);
                if (type.IsClass)
                    il.Emit(OpCodes.Isinst, type);
                else il.Emit(OpCodes.Unbox_Any, type);

                il.Emit(OpCodes.Ldarg_1);

                il.Emit(OpCodes.Callvirt, genericSerialize);

                il.Emit(OpCodes.Ret);

                return method.CreateDelegate(typeof(SerializeStreamWithTypeDelegate)) as SerializeStreamWithTypeDelegate;
            })(value, stream);
        }


        /// <summary>
        /// Serialize value using specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] Serialize(Type type, object value) {
            return _serializeWithTypes.GetOrAdd(type, _ => { 
                var name = String.Concat(SerializeStr, type.FullName);
                var method = new DynamicMethod(name, CustomBinary.ByteArrayType, new[] { CustomBinary.ObjectType }, restrictedSkipVisibility: true);

                var il = method.GetILGenerator();
                var genericMethod = _getSerializerMethod.MakeGenericMethod(type);
                var genericType = _messageSharkSerializerType.MakeGenericType(type);

                var genericSerialize = genericType.GetMethod(SerializeStr, new[] { type });

                il.Emit(OpCodes.Call, genericMethod);

                il.Emit(OpCodes.Ldarg_0);
                if (type.IsClass)
                    il.Emit(OpCodes.Isinst, type);
                else il.Emit(OpCodes.Unbox_Any, type);

                il.Emit(OpCodes.Callvirt, genericSerialize);

                il.Emit(OpCodes.Ret);

                return method.CreateDelegate(typeof(SerializeWithTypeDelegate)) as SerializeWithTypeDelegate;
            })(value);
        }

        /// <summary>
        /// Serialize value using underlying type
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] Serialize(object value) {
            return Serialize(value.GetType(), value);
        }

        /// <summary>
        /// Serialize value to stream using underlying type of value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="stream"></param>
        public static void Serialize(object value, Stream stream) {
            Serialize(value.GetType(), value, stream);
        }

        /// <summary>
        /// Deserialize value using specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object Deserialize(Type type, byte[] value) {
            return _deserializeWithTypes.GetOrAdd(type.FullName, _ => {

                var name = String.Concat(DeserializeStr, type.FullName);
                var method = new DynamicMethod(name, CustomBinary.ObjectType, new[] { CustomBinary.ByteArrayType, CustomBinary.IntType }, restrictedSkipVisibility: true);

                var il = method.GetILGenerator();
                var genericMethod = _getSerializerMethod.MakeGenericMethod(type);
                var genericType = _messageSharkSerializerType.MakeGenericType(type);

                var genericDeserialize = genericType.GetMethod(DeserializeStr, new[] { CustomBinary.ByteArrayType });

                il.Emit(OpCodes.Call, genericMethod);

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, genericDeserialize);

                if (type.IsClass)
                    il.Emit(OpCodes.Isinst, type);
                else {
                    il.Emit(OpCodes.Box, type);
                }

                il.Emit(OpCodes.Ret);

                return method.CreateDelegate(typeof(DeserializeWithTypeDelegate)) as DeserializeWithTypeDelegate;
            })(value, 0);
        }


        /// <summary>
        /// Deserialize to type using specified stream
        /// </summary>
        /// <param name="type"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static object Deserialize(Type type, Stream stream) {
            return _deserializeStreamWithTypes.GetOrAdd(type.FullName, _ => {

                var name = String.Concat(DeserializeStr, StreamStr, type.FullName);
                var method = new DynamicMethod(name, CustomBinary.ObjectType, new[] { CustomBinary.StreamType, CustomBinary.IntType }, restrictedSkipVisibility: true);

                var il = method.GetILGenerator();
                var genericMethod = _getSerializerMethod.MakeGenericMethod(type);
                var genericType = _messageSharkSerializerType.MakeGenericType(type);

                var genericDeserialize = genericType.GetMethod(DeserializeStr, new[] { CustomBinary.StreamType });

                il.Emit(OpCodes.Call, genericMethod);

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, genericDeserialize);

                if (type.IsClass)
                    il.Emit(OpCodes.Isinst, type);
                else {
                    il.Emit(OpCodes.Box, type);
                }

                il.Emit(OpCodes.Ret);

                return method.CreateDelegate(typeof(DeserializeStreamWithTypeDelegate)) as DeserializeStreamWithTypeDelegate;
            })(stream, 0);
        }

        /// <summary>
        /// Deserialize buffer into T type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static T Deserialize<T>(byte[] buffer) {
            if (typeof(T).IsCollectionAssignable()) {
                var obj = CustomBinary.GetSerializer<InternalWrapper<T>>().Deserialize(buffer);
                return obj.Value;
            }
            return CustomBinary.GetSerializer<T>().Deserialize(buffer);
        }

        /// <summary>
        /// Deserialize stream into T type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static T Deserialize<T>(Stream stream) {
            if (typeof(T).IsCollectionAssignable()) {
                var obj = CustomBinary.GetSerializer<InternalWrapper<T>>().Deserialize(stream);
                return obj.Value;
            }
            return CustomBinary.GetSerializer<T>().Deserialize(stream);
        }

        /// <summary>
        /// Generate meta data for serializing interfaces and classes using inheritant
        /// </summary>
        public static void Build() {
            CustomBinary.Build();
        }

        /// <summary>
        /// Register any type that requires length of buffer size when serializing/deserializing
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void RegisterBufferedType<T>() {
            CustomBinary.RegisterBufferedType<T>();
        }

        /// <summary>
        /// Register a method that would be used for serializing custom types to bytes
        /// The method must be a public method
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        public static void RegisterWriter<T>(Action<CustomBuffer, T, int, bool> func) {
            CustomBinary.RegisterWriter(func);
        }

        /// <summary>
        /// Register a new method that would be used to read custom type from bytes
        /// The method must be a public method
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        public static void RegisterReader<T>(Func<byte[], T> func) {
            CustomBinary.RegisterReader(func);
        }

        /// <summary>
        /// Register a new method that would be used to read custom type from bytes that requires
        /// Type information to be passed during deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        public static void RegisterReader<T>(Func<byte[], Type, T> func) {
            CustomBinary.RegisterReader(func);
        }

        /// <summary>
        /// Set type as a subclass for TType. This would be used for registering implementation for interfaces
        /// and classes requires inheritant
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="type"></param>
        /// <param name="tag"></param>
        public static void RegisterTypeFor<TType>(Type type, byte tag) {
            CustomBinary.RegisterTypeFor<TType>(type, tag);
        }

        /// <summary>
        /// Returns true if type is already register for TType
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsTypeRegisterFor<TType>(Type type) {
            return CustomBinary.IsTypeRegisterFor<TType>(type);
        }
    }
}
