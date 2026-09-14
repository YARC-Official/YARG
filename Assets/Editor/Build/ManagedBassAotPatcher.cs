#if UNITY_IOS
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor.Build
{
    /// <summary>
    ///     IL2CPP rejects managed methods marshalled as native callbacks unless
    ///     they carry a MonoPInvokeCallback attribute (matched by type name).
    ///     The precompiled ManagedBass NuGet assembly lacks one on
    ///     ChannelReferences.Callback — its internal channel-free sync — which
    ///     breaks every CreateStream/RecordStart with callbacks at runtime.
    ///     This preprocessor injects a self-contained attribute into the dll.
    ///     NuGetForUnity restores overwrite the dll, so the patch re-applies on
    ///     every iOS build (it is idempotent).
    /// </summary>
    public class ManagedBassAotPatcher : IPreprocessBuildWithReport
    {
        private const string ASSEMBLY_PATH = "Assets/Packages/ManagedBass.3.1.1/lib/net45/ManagedBass.dll";

        private static readonly (string Method, string DelegateType)[] TARGETS =
        {
            ("ManagedBass.ChannelReferences.Callback", "ManagedBass.SyncProcedure"),
        };

        public int callbackOrder => -10;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != UnityEditor.BuildTarget.iOS)
            {
                return;
            }

            using var assembly = AssemblyDefinition.ReadAssembly(ASSEMBLY_PATH,
                new ReaderParameters { ReadWrite = true });
            var module = assembly.MainModule;

            var attrCtor = GetOrCreateAttribute(module);

            int patched = 0;
            foreach (var (methodPath, delegateName) in TARGETS)
            {
                int split = methodPath.LastIndexOf('.');
                var type = module.GetType(methodPath[..split]);
                var delegateType = module.GetType(delegateName);
                if (type == null || delegateType == null)
                {
                    Debug.LogError($"ManagedBass AOT patch: cannot resolve {methodPath} / {delegateName}");
                    continue;
                }

                foreach (var method in type.Methods.Where(
                    m => m.Name == methodPath[(split + 1)..] && m.IsStatic))
                {
                    if (method.CustomAttributes.Any(
                        a => a.AttributeType.Name == "MonoPInvokeCallbackAttribute"))
                    {
                        continue;
                    }

                    var attr = new CustomAttribute(attrCtor);
                    attr.ConstructorArguments.Add(new CustomAttributeArgument(
                        module.ImportReference(typeof(Type)), delegateType));
                    method.CustomAttributes.Add(attr);
                    patched++;
                }
            }

            if (patched > 0)
            {
                assembly.Write();
                Debug.Log($"ManagedBass AOT patch: {patched} callback(s) patched for IL2CPP");
            }
        }

        private static MethodDefinition GetOrCreateAttribute(ModuleDefinition module)
        {
            var existing = module.GetType("ManagedBass.Aot.MonoPInvokeCallbackAttribute");
            if (existing != null)
            {
                return existing.Methods.First(m => m.IsConstructor && !m.IsStatic);
            }

            var attrType = new TypeDefinition("ManagedBass.Aot", "MonoPInvokeCallbackAttribute",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
                module.ImportReference(typeof(Attribute)));
            var attrCtor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            attrCtor.Parameters.Add(new ParameterDefinition("type", ParameterAttributes.None,
                module.ImportReference(typeof(Type))));
            var il = attrCtor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, module.ImportReference(
                typeof(Attribute).GetConstructor(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null, Type.EmptyTypes, null)));
            il.Emit(OpCodes.Ret);
            attrType.Methods.Add(attrCtor);
            module.Types.Add(attrType);
            return attrCtor;
        }
    }
}
#endif
