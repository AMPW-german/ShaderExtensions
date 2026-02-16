
using System.Xml.Serialization;
using KittenExtensions;
using KSA;

namespace ShaderExtensions.ImGuiShader
{
    [KxAsset("ImGuiShader")]
    public class ImGuiShaderReference : SerializedId, IKeyed
    {
        public static readonly LookupCollection<ImGuiShaderReference> AllShaders = new(nameof(ImGuiShaderReference));

        [XmlElement("Vertex")]
        public ShaderReference Vertex;
        [XmlElement("Fragment")]
        public ShaderEx Fragment;

        public override void OnDataLoad(Mod mod)
        {
            base.OnDataLoad(mod);

            Vertex.OnDataLoad(mod);
            Fragment.OnDataLoad(mod);

            if (IsReferenceable)
                AllShaders.Register(this);
        }

        public override SerializedId Populate() => throw new System.NotImplementedException();
        public override TableString.Row ToRow() => new();
    }
}