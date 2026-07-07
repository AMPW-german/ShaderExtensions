using Brutal.Logging;
using KSA;
using System.Reflection;

namespace ShaderExtensions
{
    public static class PushConstantEx
    {
        public static void AddPushConstant(Type type, string xmlElement)
        {
            var method = typeof(PushConstantEx).GetMethod(
              nameof(AddPushConstantGeneric),
              BindingFlags.Static | BindingFlags.NonPublic
            ).MakeGenericMethod(type);

            method.CreateDelegate<Action<string>>()(xmlElement);
        }

        private static void AddPushConstantGeneric<T>(string xmlElement) where T : unmanaged
        {
            AssetEx.AddExtension(
                typeof(ShaderEx),
                nameof(ShaderEx.XmlBindings),
                typeof(PushConstantBindingReference<T>),
                xmlElement
            );
            AssetEx.AddExtension(
              typeof(AssetBundle),
              nameof(AssetBundle.Assets),
              typeof(PushConstantBindingReference<T>),
              xmlElement
            );

            var spanMethod = typeof(PushConstantBindingReference<T>).GetMethod(nameof(PushConstantBindingReference<T>.GetSpan));
            var staticFields = typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in staticFields)
            {
                var ftype = field.FieldType;
                foreach (var attr in field.GetCustomAttributesData())
                {
                    if (attr.AttributeType.FullName != typeof(SxPushConstantLookupAttribute).FullName)
                        continue;

                    var rtype = ftype.GetMethod("Invoke")?.ReturnType;
                    if (rtype == null)
                    {
                        DefaultCategory.Log.Warning($"{ftype} {typeof(T)}.{field.Name} is not a delegate type");
                        continue;
                    }

                    if (rtype == typeof(Span<T>))
                        field.SetValue(null, spanMethod?.CreateDelegate(ftype));
                    else
                        DefaultCategory.Log.Warning($"{typeof(T)}.{field.Name} return {rtype} is not a valid push constant lookup type");
                }
            }
        }
    }
}
