using KittenExtensions;
using KSA;
using ShaderExtensions;
using System.Reflection;

#pragma warning disable CS9113

namespace KittenExtensions
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class KxAssetAttribute(string xmlElement) : Attribute;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class KxAssetInjectAttribute(Type parent, string member, string xmlElement) : Attribute;
}

namespace ShaderExtensions
{
    [AttributeUsage(AttributeTargets.Struct)]
    internal class SxUniformBufferAttribute(string xmlElement) : Attribute;

    [AttributeUsage(AttributeTargets.Field)]
    internal class SxUniformBufferLookupAttribute() : Attribute;
}