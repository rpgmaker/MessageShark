using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;

namespace MessageShark
{
	public static partial class CustomBinary
	{
		static MethodBuilder GenerateDeserializerClass(TypeBuilder typeBuilder, Type objType, Type ownerType = null) {
			MethodBuilder method;
			var suffix = ownerType == objType ? "Method" : string.Empty;
			var key = objType.FullName + suffix;
			var methodPrefix = objType.Name + suffix;
			if (ReaderMethodBuilders.TryGetValue(key, out method)) return method;
			var methodName = String.Intern("Read") + methodPrefix;
			method = typeBuilder.DefineMethod(methodName, MethodAttribute,
				typeof(void), new[] { objType.IsValueType ? objType.MakeByRefType() : objType, ByteArrayType, typeof(int).MakeByRefType() });

			ReaderMethodBuilders[key] = method;
			var methodIL = method.GetILGenerator();

			WriteDeserializerClass(typeBuilder, methodIL, objType, tag: 0, field: null, callerType: objType);

			methodIL.Emit(OpCodes.Ret);
			return method;
		}

		static void WriteDeserializerClass(TypeBuilder typeBuilder, ILGenerator il, Type type, int tag, FieldInfo field,
			Type callerType = null, int? itemLocalIndex = null) {
			var isDict = type.IsDictionaryType();
			var isList = type.IsListType();
			var isClass = !isDict && !isList;
			if (isClass) {
				WriteDeserializerProperties(typeBuilder, il, type, callerType);
			} else {
				if(isDict)
					WriteDeserializerDictionary(typeBuilder, il, type, tag, field, itemLocalIndex);
				else if (isList) {
					if (type.IsArray)
						WriteDeserializerArray(typeBuilder, il, type, tag, field, itemLocalIndex);
					else
						WriteDeserializerList(typeBuilder, il, type, tag, field, itemLocalIndex);
				}
			}
		}

		static void WriteDeserializerDictionary(TypeBuilder typeBuilder, ILGenerator il, Type type, int tag, FieldInfo field,
			int? itemLocalIndex = null) {
			var arguments = type.GetGenericArguments();
			var keyType = arguments[0];
			var valueType = arguments[1];
			if (GenericIDictType.IsAssignableFrom(type.GetGenericTypeDefinition()))
				type = GenericDictType.MakeGenericType(keyType, valueType);
			
			var lengthLocal = il.DeclareLocal(typeof(int));
			var dictLocal = il.DeclareLocal(type);
			var keyItemLocal = il.DeclareLocal(keyType);
			var valueItemLocal = il.DeclareLocal(valueType);
			var indexLocal = il.DeclareLocal(typeof(int));
			var startLabel = il.DefineLabel();
			var endLabel = il.DefineLabel();

			il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Ldc_I4, tag);
			il.Emit(OpCodes.Call, GetCollectionLengthMethod);
			il.Emit(OpCodes.Stloc, lengthLocal);

			il.Emit(OpCodes.Ldloc, lengthLocal.LocalIndex);
			il.Emit(OpCodes.Newobj, type.GetConstructor(CtorCapacityTypes));
			il.Emit(OpCodes.Stloc, dictLocal.LocalIndex);


			il.Emit(OpCodes.Ldc_I4_0);
			il.Emit(OpCodes.Stloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Br, startLabel);
			il.MarkLabel(endLabel);


			WriteDeserializerReadValue(typeBuilder, il, keyType, 1, keyItemLocal.LocalIndex);
			WriteDeserializerReadValue(typeBuilder, il, valueType, 2, valueItemLocal.LocalIndex);


			il.Emit(OpCodes.Ldloc, dictLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, keyItemLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, valueItemLocal.LocalIndex);
			il.Emit(OpCodes.Callvirt, type.GetMethod("set_Item"));


			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stloc, indexLocal.LocalIndex);
			il.MarkLabel(startLabel);
			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, lengthLocal.LocalIndex);
			il.Emit(OpCodes.Blt, endLabel);

			if (itemLocalIndex == null) {
				if (field.DeclaringType.IsClass)
					il.Emit(OpCodes.Ldarga, 1);
				else
					il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldloc, dictLocal.LocalIndex);
				SetMemberValue(il, field);
			} else {
				il.Emit(OpCodes.Ldloc, dictLocal.LocalIndex);
				il.Emit(OpCodes.Stloc, itemLocalIndex.Value);
			}
		}

		static void WriteDeserializerList(TypeBuilder typeBuilder, ILGenerator il, Type type, int tag, FieldInfo field,
			int? itemLocalIndex = null) {
			var itemType = type.GetGenericArguments()[0];
			if (GenericIListType.IsAssignableFrom(type.GetGenericTypeDefinition()))
				type = GenericListType.MakeGenericType(itemType);


			var lengthLocal = il.DeclareLocal(typeof(int));
			var listLocal = il.DeclareLocal(type);
			var itemLocal = il.DeclareLocal(itemType);
			var indexLocal = il.DeclareLocal(typeof(int));
			var startLabel = il.DefineLabel();
			var endLabel = il.DefineLabel();

			il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Ldc_I4, tag);
			il.Emit(OpCodes.Call, GetCollectionLengthMethod);
			il.Emit(OpCodes.Stloc, lengthLocal);

			il.Emit(OpCodes.Ldloc, lengthLocal.LocalIndex);
			il.Emit(OpCodes.Newobj, type.GetConstructor(CtorCapacityTypes));
			il.Emit(OpCodes.Stloc, listLocal.LocalIndex);


			il.Emit(OpCodes.Ldc_I4_0);
			il.Emit(OpCodes.Stloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Br, startLabel);
			il.MarkLabel(endLabel);

			WriteDeserializerReadValue(typeBuilder, il, itemType, 1, itemLocal.LocalIndex);
			
			il.Emit(OpCodes.Ldloc, listLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, itemLocal.LocalIndex);
			il.Emit(OpCodes.Callvirt, type.GetMethod("Add"));


			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stloc, indexLocal.LocalIndex);
			il.MarkLabel(startLabel);
			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, lengthLocal.LocalIndex);
			il.Emit(OpCodes.Blt, endLabel);

			if (itemLocalIndex == null) {
				if (field.DeclaringType.IsClass)
					il.Emit(OpCodes.Ldarga, 1);
				else
					il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldloc, listLocal.LocalIndex);
				SetMemberValue(il, field);
			} else {
				il.Emit(OpCodes.Ldloc, listLocal.LocalIndex);
				il.Emit(OpCodes.Stloc, itemLocalIndex.Value);
			}
		}

		static void WriteDeserializerArray(TypeBuilder typeBuilder, ILGenerator il, Type type, int tag, FieldInfo field,
			int? itemLocalIndex = null) {
			var itemType = type.GetElementType();

			var lengthLocal = il.DeclareLocal(typeof(int));
			var arrayLocal = il.DeclareLocal(type);
			var itemLocal = il.DeclareLocal(itemType);
			var indexLocal = il.DeclareLocal(typeof(int));
			var startLabel = il.DefineLabel();
			var endLabel = il.DefineLabel();

			il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Ldc_I4, tag);
			il.Emit(OpCodes.Call, GetCollectionLengthMethod);
			il.Emit(OpCodes.Stloc, lengthLocal);
			il.Emit(OpCodes.Ldloc, lengthLocal.LocalIndex);
			il.Emit(OpCodes.Newarr, itemType);
			il.Emit(OpCodes.Stloc, arrayLocal.LocalIndex);


			il.Emit(OpCodes.Ldc_I4_0);
			il.Emit(OpCodes.Stloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Br, startLabel);
			il.MarkLabel(endLabel);

			WriteDeserializerReadValue(typeBuilder, il, itemType, 1, itemLocal.LocalIndex);
			
			il.Emit(OpCodes.Ldloc, arrayLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, itemLocal.LocalIndex);
			il.Emit(OpCodes.Stelem, itemType);
			

			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stloc, indexLocal.LocalIndex);
			il.MarkLabel(startLabel);
			il.Emit(OpCodes.Ldloc, indexLocal.LocalIndex);
			il.Emit(OpCodes.Ldloc, lengthLocal.LocalIndex);
			il.Emit(OpCodes.Blt, endLabel);

			if (itemLocalIndex == null) {
				if (field.DeclaringType.IsClass)
					il.Emit(OpCodes.Ldarga, 1);
				else
					il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldloc, arrayLocal.LocalIndex);
				SetMemberValue(il, field);
			} else {
				il.Emit(OpCodes.Ldloc, arrayLocal.LocalIndex);
				il.Emit(OpCodes.Stloc, itemLocalIndex.Value);
			}
		}

		static void WriteDeserializerReadValue(TypeBuilder typeBuilder, ILGenerator il, Type type, int tag, int itemLocalIndex) {
			var isCollection = type.IsCollectionType();
			var isClass = !isCollection && type.IsComplexType();
			if (isClass) {
				MethodBuilder method;
				var hasTypeMapping = TypeMapping.ContainsKey(type);

				if (hasTypeMapping) {
					var index = 0;
					var typeMapping = TypeMapping[type];
					var count = typeMapping.Count;
					var types = typeMapping.Select(kv => kv.Key);
					var needBranchLabel = count > 1;
					var branchLabel = needBranchLabel ? il.DefineLabel() : DefaultLabel;
					var valueTypeLocal = il.DeclareLocal(TypeType);

					il.Emit(OpCodes.Ldarg_2);
					il.Emit(OpCodes.Ldtoken, type);
					il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
					il.Emit(OpCodes.Ldarg_3);
					il.Emit(OpCodes.Call, ConvertBaseToConcreteTypeMethod);

					il.Emit(OpCodes.Stloc, valueTypeLocal.LocalIndex);

					foreach (var mapType in types) {
						index++;
						var isLastIndex = index == count;
						var isLastCondition = isLastIndex && needBranchLabel;
						var conditionLabel = !isLastCondition ? il.DefineLabel() : DefaultLabel;
						var currentConditionLabel = isLastCondition ? branchLabel : conditionLabel;
						il.Emit(OpCodes.Ldloc, valueTypeLocal.LocalIndex);
						il.Emit(OpCodes.Ldtoken, mapType);
						il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
						il.Emit(OpCodes.Call, GetTypeOpEqualityMethod);
						il.Emit(OpCodes.Brfalse, currentConditionLabel);

						method = GenerateDeserializerClass(typeBuilder, mapType);
						il.Emit(OpCodes.Newobj, mapType.GetConstructor(Type.EmptyTypes));
						il.Emit(OpCodes.Stloc, itemLocalIndex);
						il.Emit(OpCodes.Ldarg_0);
						il.Emit(OpCodes.Ldloc, itemLocalIndex);
						if (mapType.IsClass)
							il.Emit(OpCodes.Castclass, mapType);
						else il.Emit(OpCodes.Unbox_Any, mapType);
						il.Emit(OpCodes.Ldarg_2);
						il.Emit(OpCodes.Ldarg_3);
						il.Emit(OpCodes.Call, method);

						if (!isLastIndex)
							il.Emit(OpCodes.Br, branchLabel);
						il.MarkLabel(currentConditionLabel);
					}
				} else {
					method = GenerateDeserializerClass(typeBuilder, type);
					var isTypeClass = type.IsClass;
					if (isTypeClass) {
						il.Emit(OpCodes.Newobj, type.GetConstructor(Type.EmptyTypes));
						il.Emit(OpCodes.Stloc, itemLocalIndex);
					} else {
						il.Emit(OpCodes.Ldloca, itemLocalIndex);
						il.Emit(OpCodes.Initobj, type);
					}
					il.Emit(OpCodes.Ldarg_0);
					if (isTypeClass)
						il.Emit(OpCodes.Ldloc, itemLocalIndex);
					else il.Emit(OpCodes.Ldloca, itemLocalIndex);
					il.Emit(OpCodes.Ldarg_2);
					il.Emit(OpCodes.Ldarg_3);
					il.Emit(OpCodes.Call, method);
				}
			} else if (isCollection) {
				WriteDeserializerClass(typeBuilder, il, type, tag, null, itemLocalIndex: itemLocalIndex);
			} else {
				var nonNullableType = type.GetNonNullableType();
				var isTypeEnum = type.IsEnum;
				var needTypeForReader = PrimitiveReadersWithTypes.Contains(isTypeEnum ? EnumType : nonNullableType);
				il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Ldarg_3);
				il.Emit(OpCodes.Ldc_I4, 1);
				il.Emit(OpCodes.Ldc_I4, type.IsBufferedTypeInt());
				il.Emit(OpCodes.Call, ReadNextBytesMethod);
				if (needTypeForReader) {
					il.Emit(OpCodes.Ldtoken, type);
					il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
				}
				if (isTypeEnum)
					il.Emit(OpCodes.Call, PrimitiveReaderMethods[EnumType]);
				else {
					il.Emit(OpCodes.Call, PrimitiveReaderMethods[nonNullableType]);
					if (type.IsNullable())
						il.Emit(OpCodes.Newobj, type.GetNullableTypeCtor());
				}
				if (needTypeForReader) il.Emit(OpCodes.Unbox_Any, type);
				il.Emit(OpCodes.Stloc, itemLocalIndex);
			}
		}

		static void WriteDeserializerCallClassMethod(TypeBuilder typeBuilder, ILGenerator il, Type type, int tag, FieldInfo field, Type ownerType = null) {
			MethodBuilder method;
			var local = il.DeclareLocal(type);
			var hasTypeMapping = TypeMapping.ContainsKey(type);
			il.Emit(OpCodes.Ldc_I4, tag);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Call, MoveToNextBytesMethod);

			if (hasTypeMapping) {
				var index = 0;
				var typeMapping = TypeMapping[type];
				var count = typeMapping.Count;
				var types = typeMapping.Select(kv => kv.Key);
				var needBranchLabel = count > 1;
				var branchLabel = needBranchLabel ? il.DefineLabel() : DefaultLabel;
				var valueTypeLocal = il.DeclareLocal(TypeType);

				il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Ldtoken, type);
				il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
				il.Emit(OpCodes.Ldarg_3);
				il.Emit(OpCodes.Call, ConvertBaseToConcreteTypeMethod);

				il.Emit(OpCodes.Stloc, valueTypeLocal.LocalIndex);

				foreach (var mapType in types) {
					index++;
					var isLastIndex = index == count;
					var isLastCondition = isLastIndex && needBranchLabel;
					var conditionLabel = !isLastCondition ? il.DefineLabel() : DefaultLabel;
					var currentConditionLabel = isLastCondition ? branchLabel : conditionLabel;
					il.Emit(OpCodes.Ldloc, valueTypeLocal.LocalIndex);
					il.Emit(OpCodes.Ldtoken, mapType);
					il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
					il.Emit(OpCodes.Call, GetTypeOpEqualityMethod);
					il.Emit(OpCodes.Brfalse, currentConditionLabel);

					method = GenerateDeserializerClass(typeBuilder, mapType, ownerType: ownerType);
					il.Emit(OpCodes.Newobj, mapType.GetConstructor(Type.EmptyTypes));
					il.Emit(OpCodes.Stloc, local.LocalIndex);
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Ldloc, local.LocalIndex);
					if (mapType.IsClass)
						il.Emit(OpCodes.Castclass, mapType);
					else il.Emit(OpCodes.Unbox_Any, mapType);
					il.Emit(OpCodes.Ldarg_2);
					il.Emit(OpCodes.Ldarg_3);
					il.Emit(OpCodes.Call, method);

					if (!isLastIndex)
						il.Emit(OpCodes.Br, branchLabel);
					il.MarkLabel(currentConditionLabel);
				}
			} else {
				method = GenerateDeserializerClass(typeBuilder, type, ownerType: ownerType);
				var isTypeClass = type.IsClass;
				if (isTypeClass) {
					il.Emit(OpCodes.Newobj, type.GetConstructor(Type.EmptyTypes));
					il.Emit(OpCodes.Stloc, local.LocalIndex);
				} else {
					il.Emit(OpCodes.Ldloca, local.LocalIndex);
					il.Emit(OpCodes.Initobj, type);
				}
				il.Emit(OpCodes.Ldarg_0);
				if (isTypeClass)
					il.Emit(OpCodes.Ldloc, local.LocalIndex);
				else il.Emit(OpCodes.Ldloca, local.LocalIndex);
				il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Ldarg_3);
				il.Emit(OpCodes.Call, method);
			}

			if (field.DeclaringType.IsClass)
				il.Emit(OpCodes.Ldarga, 1);
			else
				il.Emit(OpCodes.Ldarg_1);
			
			il.Emit(OpCodes.Ldloc, local.LocalIndex);
			SetMemberValue(il, field);
		}

		static void WriteDeserializerProperties(TypeBuilder typeBuilder, ILGenerator il, Type type, Type callerType) {
			var fields = GetTypeFields(type);
			var tag = 1;
			var isTypeClass = type.IsClass;

			foreach (var field in fields) {
				var flagLabel = il.DefineLabel();
				var fieldType = field.FieldType;

				il.Emit(OpCodes.Ldc_I4, tag);
				il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Ldarg_3);
				il.Emit(OpCodes.Ldind_I4);
				il.Emit(OpCodes.Call, IsNextTagForPropertyTagMethod);
				il.Emit(OpCodes.Brfalse, flagLabel);

				if (fieldType.IsComplexType()) {
					if (fieldType.IsCollectionType())
						WriteDeserializerClass(typeBuilder, il, fieldType, tag, field, callerType: callerType);
					else {
						WriteDeserializerCallClassMethod(typeBuilder, il, fieldType, tag, field, ownerType: type);
					}
				} else {
					var isTypeEnum = fieldType.IsEnum;
					var needTypeForReader = PrimitiveReadersWithTypes.Contains(isTypeEnum ? EnumType : fieldType);

					if (isTypeClass)
						il.Emit(OpCodes.Ldarga, 1);
					else
						il.Emit(OpCodes.Ldarg_1);

					il.Emit(OpCodes.Ldarg_2);
					il.Emit(OpCodes.Ldarg_3);
					il.Emit(OpCodes.Ldc_I4, tag);
					il.Emit(OpCodes.Ldc_I4, fieldType.IsBufferedTypeInt());
					il.Emit(OpCodes.Call, ReadNextBytesMethod);
					if (needTypeForReader) {
						il.Emit(OpCodes.Ldtoken, fieldType);
						il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
					}
					if (isTypeEnum)
						il.Emit(OpCodes.Call, PrimitiveReaderMethods[EnumType]);
					else {
						il.Emit(OpCodes.Call, PrimitiveReaderMethods[fieldType.GetNonNullableType()]);
						if (fieldType.IsNullable()) 
							il.Emit(OpCodes.Newobj, fieldType.GetNullableTypeCtor());
					}
					if (needTypeForReader) il.Emit(OpCodes.Unbox_Any, fieldType);
					SetMemberValue(il, field);
				}
				il.MarkLabel(flagLabel);
				tag++;
			}
		}
	}
}
