
using System.Xml.Serialization;
using KittenExtensions;
using KSA;

namespace ShaderExtensions.ImGuiShader
{
    [KxAsset("ImGuiShader")]
    public class ImGuiShaderReference : SerializedId, ILookupItem
    {
        public static readonly LookupCollection<ImGuiShaderReference> AllShaders = new(nameof(ImGuiShaderReference));

        [XmlElement("Vertex")]
        public ShaderReference Vertex;
        [XmlElement("Fragment")]
        public ShaderEx Fragment;

        public int LookupIndex { get; set; }

        public override void OnDataLoad(Mod mod)
        {
            base.OnDataLoad(mod);

            Vertex.OnDataLoad(mod);
            Fragment.OnDataLoad(mod);

            if (IsReferenceable)
                AllShaders.Register(this);
        }

        public override TableString.Row ToRow() => new();
    }
}