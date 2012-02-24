using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;

namespace MessageShark {
    public static partial class CustomBinary {
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

            var serializeMethod = GenerateSerializerClass(type, objType, isEntryPoint: true);
            var deserializeMethod = GenerateDeserializerClass(type, objType);

            var methodSerialize = type.DefineMethod("ISerializer.Serialize", MethodAttribute,
                ByteArrayType, new[] { objType });

            var methodDeserialize = type.DefineMethod("ISerializer.Deserialize", MethodAttribute,
                objType, new[] { ByteArrayType });

            var methodSerializeIL = methodSerialize.GetILGenerator();
            var methodDeserializeIL = methodDeserialize.GetILGenerator();

            var msLocal = methodSerializeIL.DeclareLocal(BufferStreamType);
            var bufferLocal = methodSerializeIL.DeclareLocal(ByteArrayType);

            var returnLocal = methodDeserializeIL.DeclareLocal(objType);
            var startIndexLocal = methodDeserializeIL.DeclareLocal(typeof(int));
            methodDeserializeIL.Emit(OpCodes.Ldc_I4_0);
            methodDeserializeIL.Emit(OpCodes.Stloc, startIndexLocal.LocalIndex);

            //Serialize
            methodSerializeIL.Emit(OpCodes.Newobj, BufferStreamCtor);
            methodSerializeIL.Emit(OpCodes.Stloc, msLocal.LocalIndex);
            methodSerializeIL.Emit(OpCodes.Ldarg_0);
            methodSerializeIL.Emit(OpCodes.Ldloc, msLocal.LocalIndex);
            methodSerializeIL.Emit(OpCodes.Ldarg_1);
            methodSerializeIL.Emit(OpCodes.Ldc_I4_1);
            methodSerializeIL.Emit(OpCodes.Ldc_I4_0);
            methodSerializeIL.Emit(OpCodes.Callvirt, serializeMethod);

            methodSerializeIL.Emit(OpCodes.Ldloc, msLocal.LocalIndex);
            methodSerializeIL.Emit(OpCodes.Callvirt, BufferStreamToArrayMethod);
            methodSerializeIL.Emit(OpCodes.Stloc_S, bufferLocal.LocalIndex);
            methodSerializeIL.Emit(OpCodes.Ldloc_S, bufferLocal.LocalIndex);
            methodSerializeIL.Emit(OpCodes.Ret);

            //Deserialize
            if (TypeMapping.ContainsKey(objType)) {
                methodDeserializeIL.Emit(OpCodes.Ldarg_1);
                methodDeserializeIL.Emit(OpCodes.Ldtoken, objType);
                methodDeserializeIL.Emit(OpCodes.Call, GetTypeFromHandleMethod);
                methodDeserializeIL.Emit(OpCodes.Ldloca_S, startIndexLocal.LocalIndex);
                methodDeserializeIL.Emit(OpCodes.Call, CreateInstanceForConcreteTypeMethod);
            } else 
                methodDeserializeIL.Emit(OpCodes.Newobj, objType.GetConstructor(Type.EmptyTypes));
            methodDeserializeIL.Emit(OpCodes.Stloc, returnLocal.LocalIndex);
            
            methodDeserializeIL.Emit(OpCodes.Ldarg_0);
            methodDeserializeIL.Emit(OpCodes.Ldloc, returnLocal.LocalIndex);
            methodDeserializeIL.Emit(OpCodes.Ldarg_1);
            methodDeserializeIL.Emit(OpCodes.Ldloca_S, startIndexLocal.LocalIndex);
            methodDeserializeIL.Emit(OpCodes.Callvirt, deserializeMethod);
            
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
