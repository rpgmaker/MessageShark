using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark {
    public static class MessageSharkSerializer {
        /// <summary>
        /// Serialize value into a byte array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] Serialize<T>(T value) where T : class {
            return CustomBinary.GetSerializer<T>().Serialize(value);
        }


        /// <summary>
        /// Deserialize buffer into T type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static T Deserialize<T>(byte[] buffer) where T : class {
            return CustomBinary.GetSerializer<T>().Deserialize(buffer);
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

    }
}
