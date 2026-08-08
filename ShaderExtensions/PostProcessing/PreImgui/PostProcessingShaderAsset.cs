using KittenExtensions;
using KSA;
using System.Linq;
using System.Xml.Serialization;

namespace ShaderExtensions.PostProcessing.PreImgui
{
    [KxAsset("PostProcessingShader")]
    public class PostProcessingShaderAsset : ShaderEx, IPostProcessingShader, IPostProcessingLegacyMetadata
    {
        [XmlAttribute]
        public int RenderPassId = 0;

        /// <summary>No longer supported. Kept only so shaders that still declare it can be rejected with a clear message.</summary>
        [XmlAttribute]
        public int SubpassId = -1;

        /// <summary>No longer supported. Kept only so shaders that still declare it can be rejected with a clear message.</summary>
        [XmlAttribute]
        public bool RequiresUniqueRenderpass = false;

        [XmlAttribute]
        public string VertexShaderID = "ScreenspaceVert";

        public static List<PostProcessingShaderAsset> AllShaders = [];

        public ShaderReference VertexShader => ModLibrary.Get<ShaderReference>(VertexShaderID);

        int IPostProcessingShader.PassId => RenderPassId;
        ShaderReference IPostProcessingShader.VertexShaderReference => VertexShader;

        bool IPostProcessingLegacyMetadata.HasUnsupportedMetadata(out string reason)
        {
            if (SubpassId >= 0)
            {
                reason = $"it declares {nameof(SubpassId)}={SubpassId}";
                return true;
            }

            if (RequiresUniqueRenderpass)
            {
                reason = $"it declares {nameof(RequiresUniqueRenderpass)}=true";
                return true;
            }

            reason = null;
            return false;
        }

        public override void OnDataLoad(Mod mod)
        {
            base.OnDataLoad(mod);
            foreach (PostProcessingInputReference input in XmlBindings.OfType<PostProcessingInputReference>())
                input.Initialize(this);
            foreach (PostProcessingOutputReference output in XmlBindings.OfType<PostProcessingOutputReference>())
                output.Initialize(this);

            AllShaders.Add(this);
        }
    }
}