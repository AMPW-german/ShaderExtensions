using KittenExtensions;
using KSA;
using System.Linq;
using System.Xml.Serialization;

namespace ShaderExtensions.PostProcessing.PostImgui
{
    [KxAsset("GlobalPostShader")]
    public class GlobalPostShaderAsset : ShaderEx
    {
        [XmlAttribute]
        public int RenderPassId = 0;

        [XmlAttribute]
        public int SubpassId = 0;

        [XmlAttribute]
        public bool RequiresUniqueRenderpass = false;

        [XmlAttribute]
        public string VertexShaderID = "ScreenspaceVert";

        public static List<GlobalPostShaderAsset> AllShaders = new();
        public static SortedDictionary<int, SortedDictionary<int, List<GlobalPostShaderAsset>>> ShadersByPassAndSubpass = new();

        public ShaderReference VertexShader => ModLibrary.Get<ShaderReference>(VertexShaderID);

        public override void OnDataLoad(Mod mod)
        {
            base.OnDataLoad(mod);
            foreach (PostProcessingInputReference input in XmlBindings.OfType<PostProcessingInputReference>())
                input.Initialize(this);
            foreach (PostProcessingOutputReference output in XmlBindings.OfType<PostProcessingOutputReference>())
                output.Initialize(this);

            AllShaders.Add(this);

            if (!ShadersByPassAndSubpass.TryGetValue(RenderPassId, out SortedDictionary<int, List<GlobalPostShaderAsset>> value1))
            {
                value1 = new();
                ShadersByPassAndSubpass[RenderPassId] = value1;
            }
            if (!value1.TryGetValue(SubpassId, out List<GlobalPostShaderAsset> value))
            {
                value = new();
                value1[SubpassId] = value;
            }

            value.Add(this);
        }
    }
}