using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;

namespace MessageShark {
    public static partial class CustomBinary {
        static void GenerateSerializerCallClassMethod(TypeBuilder typeBuilder, ILGenerator il, Type type, int bufferLocalIndex) {
            MethodBuilder method;
            if (TypeMapping.ContainsKey(type)) {
                var isTypeClass = !type.IsValueType;
                var index = 0;
                var typeMapping = TypeMapping[type];
                var count = typeMapping.Count;
                var types = typeMapping.Select(kv => kv.Key);
                var needBranchLabel = count > 1;
                var branchLabel = needBranchLabel ? il.DefineLabel() : DefaultLabel;
                var valueTypeLocal = il.DeclareLocal(TypeType);
                if (isTypeClass)
                    il.Emit(OpCodes.Ldarg_1);
                else il.Emit(OpCodes.Ldarga, 1);
                il.Emit(OpCodes.Callvirt, GetTypeMethod);
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

                    method = GenerateSerializerClass(typeBuilder, mapType, isEntryPoint: true, baseType: type);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldloc, bufferLocalIndex);
                    il.Emit(OpCodes.Ldarg_1);
                    if (mapType.IsClass)
                        il.Emit(OpCodes.Castclass, mapType);
                    else il.Emit(OpCodes.Unbox_Any, mapType);
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Callvirt, method);

                    if (!isLastIndex)
                        il.Emit(OpCodes.Br, branchLabel);
                    il.MarkLabel(currentConditionLabel);
                }
                return;
            }
            method = GenerateSerializerClass(typeBuilder, type, isEntryPoint: true);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, bufferLocalIndex);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, method);
        }

        static void GenerateDeserializerCallClassMethod(TypeBuilder typeBuilder, ILGenerator il, Type type, int valueLocalIndex, int startIndexLocalIndex) {
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

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldtoken, type);
                il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
                il.Emit(OpCodes.Ldloca_S, startIndexLocalIndex);
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
                    il.Emit(OpCodes.Stloc, valueLocalIndex);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldloc, valueLocalIndex);
                    if (mapType.IsClass)
                        il.Emit(OpCodes.Castclass, mapType);
                    else il.Emit(OpCodes.Unbox_Any, mapType);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldloca_S, startIndexLocalIndex);
                    il.Emit(OpCodes.Callvirt, method);

                    if (!isLastIndex)
                        il.Emit(OpCodes.Br, branchLabel);
                    il.MarkLabel(currentConditionLabel);
                }
            } else {
                method = GenerateDeserializerClass(typeBuilder, type);
                var isTypeClass = !type.IsValueType;
                if (isTypeClass) {
                    il.Emit(OpCodes.Newobj, type.GetConstructor(Type.EmptyTypes));
                    il.Emit(OpCodes.Stloc, valueLocalIndex);
                } else {
                    il.Emit(OpCodes.Ldloca, valueLocalIndex);
                    il.Emit(OpCodes.Initobj, type);
                }
                il.Emit(OpCodes.Ldarg_0);
                if (isTypeClass)
                    il.Emit(OpCodes.Ldloc, valueLocalIndex);
                else il.Emit(OpCodes.Ldloca, valueLocalIndex);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldloca_S, startIndexLocalIndex);
                il.Emit(OpCodes.Callvirt, method);
            }
        }

        static Type GenerateSerializer(Type objType) {
            Type returnType;
            if (CachedType.TryGetValue(objType, out returnType)) return returnType;
            var genericType = SerializerType.MakeGenericType(objType);
            var newTypeName = objType.Name + "Serializer";
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName(newTypeName + "Class")
                {
                    Version = new Version(1, 0, 0, 0)
                }, AssemblyBuilderAccess.RunAndSave);
            var module = assembly.DefineDynamicModule(newTypeName + ".dll");

            var type = module.DefineType(newTypeName,
                TypeAttributes.Public | TypeAttributes.Serializable | TypeAttributes.Sealed,
                typeof(Object), new[] { genericType });


            
            var methodSerialize = type.DefineMethod("ISerializer.Serialize", MethodAttribute,
                ByteArrayType, new[] { objType });

            var methodDeserialize = type.DefineMethod("ISerializer.Deserialize", MethodAttribute,
                objType, new[] { ByteArrayType });

            var methodSerializeIL = methodSerialize.GetILGenerator();
            var methodDeserializeIL = methodDeserialize.GetILGenerator();

            var bufferLocal = methodSerializeIL.DeclareLocal(BufferStreamType);

            var returnLocal = methodDeserializeIL.DeclareLocal(objType);
            var startIndexLocal = methodDeserializeIL.DeclareLocal(typeof(int));
            methodDeserializeIL.Emit(OpCodes.Ldc_I4_0);
            methodDeserializeIL.Emit(OpCodes.Stloc, startIndexLocal.LocalIndex);

            //Serialize
            methodSerializeIL.Emit(OpCodes.Newobj, BufferStreamCtor);
            methodSerializeIL.Emit(OpCodes.Stloc, bufferLocal.LocalIndex);

            GenerateSerializerCallClassMethod(type, methodSerializeIL, objType, bufferLocal.LocalIndex);
            
            methodSerializeIL.Emit(OpCodes.Ldloc, bufferLocal.LocalIndex);
            methodSerializeIL.Emit(OpCodes.Callvirt, BufferStreamToArrayMethod);
            methodSerializeIL.Emit(OpCodes.Ret);

            //Deserialize
            GenerateDeserializerCallClassMethod(type, methodDeserializeIL, objType, returnLocal.LocalIndex,
                startIndexLocal.LocalIndex);
            
            methodDeserializeIL.Emit(OpCodes.Ldloc_S, returnLocal.LocalIndex);
            methodDeserializeIL.Emit(OpCodes.Ret);

            //Override interface implementation
            type.DefineMethodOverride(methodSerialize,
                genericType.GetMethod("Serialize", new[] { objType }));
            type.DefineMethodOverride(methodDeserialize,
                genericType.GetMethod("Deserialize", new[] { ByteArrayType }));


            returnType = type.CreateType();
            CachedType[objType] = returnType;
            //assembly.Save(newTypeName + ".dll");
            return returnType;
        }
    }
}
