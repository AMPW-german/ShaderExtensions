using Brutal.Logging;
using Brutal.VulkanApi.Abstractions;
using KSA;
using System.Reflection;

namespace ShaderExtensions
{
    public static class UniformBufferEx
    {
        public static void AddUniformBuffer(Type type, string xmlElement)
        {
            var method = typeof(UniformBufferEx).GetMethod(
              nameof(AddUniformBufferGeneric),
              BindingFlags.Static | BindingFlags.NonPublic
            ).MakeGenericMethod(type);

            method.CreateDelegate<Action<string>>()(xmlElement);
        }

        private static void AddUniformBufferGeneric<T>(string xmlElement) where T : unmanaged
        {
            //KittenPiper.AddExtension().Invoke(null, new object[] {
            //    typeof(ShaderEx),
            //    nameof(ShaderEx.XmlBindings),
            //    typeof(UniformBindingReference<T>),
            //    xmlElement
            //});
            //KittenPiper.AddExtension().Invoke(null, new object[] {
            //  typeof(AssetBundle),
            //  nameof(AssetBundle.Assets),
            //  typeof(UniformBindingReference<T>),
            //  xmlElement
            //});

            AssetEx.AddExtension(
                typeof(ShaderEx),
                nameof(ShaderEx.XmlBindings),
                typeof(UniformBindingReference<T>),
                xmlElement
            );
            AssetEx.AddExtension(
              typeof(AssetBundle),
              nameof(AssetBundle.Assets),
              typeof(UniformBindingReference<T>),
              xmlElement
            );

            var bufMethod = typeof(UniformBindingReference<T>).GetMethod(nameof(UniformBindingReference<>.GetBuffer));
            var memMethod = typeof(UniformBindingReference<T>).GetMethod(nameof(UniformBindingReference<>.GetMappedMemory));
            var spanMethod = typeof(UniformBindingReference<T>).GetMethod(nameof(UniformBindingReference<>.GetSpan));
            var ptrMethod = typeof(UniformBindingReference<T>).GetMethod(nameof(UniformBindingReference<>.GetPtr));

            var staticFields = typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in staticFields)
            {
                var ftype = field.FieldType;
                foreach (var attr in field.GetCustomAttributesData())
                {
                    if (attr.AttributeType.FullName != typeof(SxUniformBufferLookupAttribute).FullName)
                        continue;

                    var rtype = ftype.GetMethod("Invoke")?.ReturnType;
                    if (rtype == null)
                    {
                        DefaultCategory.Log.Warning($"{ftype} {typeof(T)}.{field.Name} is not a delegate type");
                        continue;
                    }

                    MethodInfo lookup = null;

                    if (rtype == typeof(BufferEx))
                        lookup = bufMethod;
                    else if (rtype == typeof(MappedMemory))
                        lookup = memMethod;
                    else if (rtype == typeof(Span<T>))
                        lookup = spanMethod;
                    else if (rtype == typeof(T*))
                        lookup = ptrMethod;

                    if (lookup != null)
                        field.SetValue(null, lookup.CreateDelegate(ftype));
                    else
                        DefaultCategory.Log.Warning($"{typeof(T)}.{field.Name} return {rtype} is not a valid lookup type");
                }
            }
        }
    }
}
