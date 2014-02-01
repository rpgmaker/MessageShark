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

namespace MessageShark
{
	public static partial class CustomBinary
	{
		#region Variables
		const int LSB = 0xf;
		const int MSB = 0xf0;
        const int LENGTH_BUFFER_SIZE = 4;
        const string NullString = "\0";

        static readonly ConcurrentBag<Type> BufferedTypes = new ConcurrentBag<Type>();
        static readonly ConcurrentBag<Type> PrimitiveReadersWithTypes = new ConcurrentBag<Type>();
        static readonly ConcurrentDictionary<Type, bool> NullableTypes = new ConcurrentDictionary<Type, bool>();
        static readonly ConcurrentDictionary<Type, Type> NonNullableTypes = new ConcurrentDictionary<Type, Type>();
        static readonly ConcurrentDictionary<Type, ConstructorInfo> NullableTypeCtors = new ConcurrentDictionary<Type, ConstructorInfo>();
        static readonly ConcurrentDictionary<Type, MethodInfo> NullableMethods = new ConcurrentDictionary<Type, MethodInfo>();
        static readonly ConcurrentDictionary<Type, MethodInfo> NullableHasValueMethods = new ConcurrentDictionary<Type, MethodInfo>();
        static readonly ConcurrentDictionary<Type, MethodInfo> PrimitiveWriterMethods = new ConcurrentDictionary<Type, MethodInfo>();
        static readonly ConcurrentDictionary<Type, MethodInfo> PrimitiveReaderMethods = new ConcurrentDictionary<Type, MethodInfo>();
        static readonly ConcurrentDictionary<string, MethodBuilder> WriterMethodBuilders = new ConcurrentDictionary<string, MethodBuilder>();
        static readonly ConcurrentDictionary<string, MethodBuilder> ReaderMethodBuilders = new ConcurrentDictionary<string, MethodBuilder>();
        static readonly ConcurrentDictionary<Type, IEnumerable<PropertyInfo>> TypeProperties = new ConcurrentDictionary<Type, IEnumerable<PropertyInfo>>();
        static readonly ConcurrentDictionary<Type, object> SerializerTypes = new ConcurrentDictionary<Type, object>();
		static readonly ConcurrentDictionary<Type, Func<object>> DictFuncNew = new ConcurrentDictionary<Type, Func<object>>();
        static readonly ConcurrentDictionary<Type, Dictionary<byte, Type>> TypeIDMapping = new ConcurrentDictionary<Type, Dictionary<byte, Type>>();
        static readonly ConcurrentDictionary<Type, Dictionary<Type, byte>> TypeMapping = new ConcurrentDictionary<Type, Dictionary<Type, byte>>();
        static readonly ConcurrentDictionary<Type, Type> InterfaceTypes = new ConcurrentDictionary<Type, Type>();
		
		const BindingFlags PropertyBinding = BindingFlags.Instance | BindingFlags.Public;

		const BindingFlags MethodBinding = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

		const MethodAttributes MethodAttribute =
			MethodAttributes.Public
			| MethodAttributes.Virtual
			| MethodAttributes.Final
			| MethodAttributes.HideBySig
			| MethodAttributes.NewSlot
			| MethodAttributes.SpecialName;

        static readonly Encoding UTF8 = Encoding.UTF8;
        static readonly byte[] MinTimeSpanBytes = new byte[] { 0 };
        static readonly byte[] MaxTimeSpanBytes = new byte[] { 1 };
        static readonly long[] TimeSpanTicks = new[] { TimeSpan.TicksPerDay,
            TimeSpan.TicksPerHour, TimeSpan.TicksPerMillisecond, TimeSpan.TicksPerMinute, TimeSpan.TicksPerSecond };
        static readonly int TimeSpanTicksLength = TimeSpanTicks.Length;
		static readonly Dictionary<Type, Type> CachedType = new Dictionary<Type, Type>();

        static readonly Type BufferStreamType = typeof(CustomBuffer);
        static readonly ConstructorInfo BufferStreamCtor =
            BufferStreamType.GetConstructor(Type.EmptyTypes);

        static volatile bool IsBuild = false;
        static readonly Label DefaultLabel = default(Label);
        static readonly Type ObjectType = typeof(object);
        static readonly Type TypeType = typeof(Type);
        static readonly Type EnumType = typeof(Enum);
        static readonly Type CustomBinaryType = typeof(CustomBinary);
        static readonly Type IgnoreAttribute = typeof(MessageSharkIgnoreAttribute);
		static readonly Type ByteArrayType = typeof(byte[]);
		static readonly Type SerializerType = typeof(ISerializer<>);
		static MethodInfo BufferStreamToArrayMethod =
			BufferStreamType.GetMethod("ToArray");
        static MethodInfo BufferStreamWriteBytesMethod =
            BufferStreamType.GetMethod("Write", MethodBinding);
		static MethodInfo IDisposableDisposeMethod =
			typeof(IDisposable).GetMethod("Dispose");
        static MethodInfo EncodeLengthMethod =
            CustomBinaryType.GetMethod("EncodeLength", MethodBinding);
        static MethodInfo GetNullableValueMethodMethod =
            CustomBinaryType.GetMethod("GetNullableValue", MethodBinding);
        static MethodInfo WriteCollectionHeaderMethod =
            CustomBinaryType.GetMethod("WriteCollectionHeader", MethodBinding);
        static MethodInfo WriteTypeIDForMethod =
            CustomBinaryType.GetMethod("WriteTypeIDFor", MethodBinding);
        static MethodInfo GetTypeMethod =
            ObjectType.GetMethod("GetType");
        static MethodInfo GetTypeFromHandleMethod =
            TypeType.GetMethod("GetTypeFromHandle", MethodBinding);
        static MethodInfo GetTypeOpEqualityMethod =
            TypeType.GetMethod("op_Equality", MethodBinding);
        static MethodInfo CreateInstanceForConcreteTypeMethod =
            CustomBinaryType.GetMethod("CreateInstanceForConcreteType", MethodBinding);
        static MethodInfo IsNextTagForPropertyTagMethod =
            CustomBinaryType.GetMethod("IsNextTagForPropertyTag", MethodBinding);
        static MethodInfo ConvertBaseToConcreteTypeMethod =
            CustomBinaryType.GetMethod("ConvertBaseToConcreteType", MethodBinding);
        static MethodInfo ReadNextBytesMethod =
            CustomBinaryType.GetMethod("ReadNextBytes", MethodBinding);
        static MethodInfo GetCollectionLengthMethod =
            CustomBinaryType.GetMethod("GetCollectionLength", MethodBinding);
        static MethodInfo MoveToNextBytesMethod =
            CustomBinaryType.GetMethod("MoveToNextBytes", MethodBinding);


        static readonly byte[] TypeIDByteArray = new byte[1];
        static readonly byte[] TrueBooleanBytes = new byte[] { 1 };
        static readonly byte[] FalseBooleanBytes = new byte[] { 0 };
        static readonly Type[] CtorCapacityTypes = new Type[] { typeof(int) };
        static readonly Type ICollectionType = typeof(ICollection);
		static readonly Type DictType = typeof(IDictionary);
		static readonly Type ListType = typeof(IList);
        static readonly Type NullableType = typeof(Nullable<>);
		static readonly Type GenericIListType = typeof(IList<>);
        static readonly Type GenericIEnumeratorType = typeof(IEnumerator<>);
        static readonly Type EnumeratorType = typeof(IEnumerator);
        static readonly Type GenericListType = typeof(List<>);
        static readonly Type VoidType = typeof(void);
		static readonly Type GenericIDictType = typeof(IDictionary<,>);
		static readonly Type GenericDictType = typeof(Dictionary<,>);
		static readonly Type GenericKeyValuePairType = typeof(KeyValuePair<,>);
		static readonly Type StringType = typeof(string);
		static readonly Type DateTimeType = typeof(DateTime);
		static readonly Type DecimalType = typeof(Decimal);
		static readonly Type GuidType = typeof(Guid);
        static readonly Type GenericDictionaryEnumerator =
            Type.GetType("System.Collections.Generic.Dictionary`2+Enumerator");
        static readonly Type GenericListEnumerator =
            Type.GetType("System.Collections.Generic.List`1+Enumerator");
		#endregion
		
        static CustomBinary() {
            RegisterObjectTypeMapping();
            RegisterPrimitiveBufferWriter();
            RegisterPrimitiveBufferReader();
            RegisterBufferedTypes();
		}

        static void RegisterObjectTypeMapping() {
            TypeIDMapping[ObjectType] =
                new Dictionary<byte, Type>()
                {
                    {1, StringType}, {2, typeof(int)},
                    {3, typeof(DateTime)}, {4, typeof(bool)}, {5, typeof(char)},
                    {6, typeof(double)}, {7, typeof(short)}, {8, typeof(long)},
                    {9, typeof(decimal)}, {10, typeof(float)}, {11, typeof(ushort)},
                    {12, typeof(uint)}, {13, typeof(ulong)}, {14, GuidType}, {15, typeof(Enum)},
                    {16, typeof(TimeSpan)}, {17, typeof(TimeSpan?)}, {18, typeof(int?)}, {19, VoidType},
                    {20, typeof(byte)}
                };
            TypeMapping[ObjectType] =
                new Dictionary<Type, byte>()
                {
                    {StringType, 1}, {typeof(int), 2}, {typeof(DateTime), 3},
                    {typeof(bool), 4}, {typeof(char), 5}, {typeof(double), 6},
                    {typeof(short), 7}, {typeof(long), 8}, {typeof(decimal), 9},
                    {typeof(float), 10}, {typeof(ushort), 11}, {typeof(uint), 12},
                    {typeof(ulong), 13}, {GuidType, 14}, {typeof(Enum), 15}, {typeof(TimeSpan), 16},
                    {typeof(TimeSpan?), 17}, {typeof(int?), 18}, {VoidType, 19}, {typeof(byte), 20}
                };
        }

        internal static void Build() {
            if (IsBuild) return;
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in asms) {
                if (asm.FullName.StartsWith("System.")) continue;
                foreach (var type in asm.GetTypes()) {
                    var includes = type.GetCustomAttributes(typeof(MessageSharkIncludeAttribute), false)
                        .Cast<MessageSharkIncludeAttribute>().ToList();
                    if (!includes.Any()) continue;
                    var dict = new Dictionary<byte, Type>();
                    var typeDict = new Dictionary<Type, byte>();
                    byte currentTag = 0;
                    includes.ForEach(include =>
                    {
                        if (include.Tag == 0)
                            throw new InvalidOperationException(String.Format("Zero cannot be used as Tag for {0}", type.Name));
                        if (include.Tag == currentTag)
                            throw new InvalidOperationException(String.Format("Duplicated tag error for {0}. Tag# {1} already exists",
                                type.Name, currentTag));

                        currentTag = include.Tag;
                        dict[currentTag] = include.KnownType;
                        typeDict[include.KnownType] = currentTag;
                    });
                    TypeIDMapping[type] = dict;
                    TypeMapping[type] = typeDict;
                }
            }
            IsBuild = true;
        }

        internal static void RegisterBufferedType<T>() {
            BufferedTypes.Add(typeof(T));
        }

        internal static void RegisterWriter<T>(Action<CustomBuffer, T, int, bool> func) {
            if (!func.Method.IsPublic) throw new InvalidOperationException("func.Method must be a public method!");
            PrimitiveWriterMethods[typeof(T)] = func.Method;
        }

        internal static void RegisterReader<T>(Func<byte[], T> func) {
            if (!func.Method.IsPublic) throw new InvalidOperationException("func.Method must be a public method!");
            PrimitiveReaderMethods[typeof(T)] = func.Method;
        }

        internal static void RegisterReader<T>(Func<byte[], Type, T> func) {
            if (!func.Method.IsPublic) throw new InvalidOperationException("func.Method must be a public method!");
            var type = typeof(T);
            PrimitiveReaderMethods[type] = func.Method;
            PrimitiveReadersWithTypes.Add(type);
        }

        internal static void RegisterTypeFor<TType>(Type type, byte tag) {
            var baseType = typeof(TType);
            if (!TypeMapping.ContainsKey(baseType)) {
                TypeMapping[baseType] = new Dictionary<Type, byte>();
                TypeIDMapping[baseType] = new Dictionary<byte, Type>();
            }
            if (TypeIDMapping[baseType].ContainsKey(tag))
                throw new InvalidOperationException(
                    String.Format("Duplicated tag error for {0}. Tag# {1} already exists",
                    type.Name, tag));
            TypeMapping[baseType][type] = tag;
            TypeIDMapping[baseType][tag] = type;
        }

        internal static bool IsTypeRegisterFor<TType>(Type type) {
            var baseType = typeof(TType);
            return TypeMapping.ContainsKey(baseType) &&
                TypeMapping[baseType].ContainsKey(type);
        }

        static void RegisterBufferedTypes() {
            RegisterBufferedType<string>();
            RegisterBufferedType<Guid>();
            RegisterBufferedType<decimal>();
            RegisterBufferedType<object>();
        }

        static void RegisterPrimitiveBufferWriter() {
            RegisterWriter<int>(WriteInt32ToBuffer);
            RegisterWriter<int?>(WriteNullableInt32ToBuffer);
            RegisterWriter<DateTime>(WriteDateTimeToBuffer);
            RegisterWriter<string>(WriteStringToBuffer);
            RegisterWriter<Guid>(WriteGuidToBuffer);
            RegisterWriter<bool>(WriteBoolToBuffer);
            RegisterWriter<char>(WriteCharToBuffer);
            RegisterWriter<double>(WriteDoubleToBuffer);
            RegisterWriter<short>(WriteInt16ToBuffer);
            RegisterWriter<long>(WriteInt64ToBuffer);
            RegisterWriter<decimal>(WriteDecimalToBuffer);
            RegisterWriter<float>(WriteFloatToBuffer);
            RegisterWriter<ushort>(WriteUInt16ToBuffer);
            RegisterWriter<uint>(WriteUInt32ToBuffer);
            RegisterWriter<ulong>(WriteUInt64ToBuffer);
            RegisterWriter<TimeSpan>(WriteTimeSpanToBuffer);
            RegisterWriter<TimeSpan?>(WriteNullableTimeSpanToBuffer);
            RegisterWriter<Enum>(WriteEnumToBuffer);
            RegisterWriter<byte>(WriteByteToBuffer);
            RegisterWriter<object>(WriteObjectToBuffer);
        }

        static void RegisterPrimitiveBufferReader() {
            RegisterReader(BytesToInt32);
            RegisterReader(BytesToNullableInt32);
            RegisterReader(BytesToDateTime);
            RegisterReader(BytesToString);
            RegisterReader(BytesToGuid);
            RegisterReader(BytesToChar);
            RegisterReader(BytesToDouble);
            RegisterReader(BytesToInt16);
            RegisterReader(BytesToInt64);
            RegisterReader(BytesToDecimal);
            RegisterReader(BytesToFloat);
            RegisterReader(BytesToUInt16);
            RegisterReader(BytesToUInt32);
            RegisterReader(BytesToUInt64);
            RegisterReader(BytesToTimeSpan);
            RegisterReader(BytesToNullableTimeSpan);
            RegisterReader(BytesToEnum);
            RegisterReader(BytesToBool);
            RegisterReader(BytesToByte);
            RegisterReader(BytesToObject);
        }
	}
}
