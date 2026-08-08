using Brutal.VulkanApi;
using Core;
using KSA;
using KSA.Rendering;
using ShaderExtensions.PostProcessing.PostImgui;
using ShaderExtensions.PostProcessing.PreImgui;
using System.Xml.Serialization;

namespace ShaderExtensions.PostProcessing
{
    /// <summary>
    /// Adds a texture binding to the post-processing shader, allowing it to access the input image from a previous pass
    /// </summary>
    public class PostProcessingInputReference : SerializedId, IShaderBinding
    {
        private RenderImage attachment;
        private bool resolved;

        internal RenderImage Attachment => resolved
            ? attachment
            : throw new InvalidOperationException($"{nameof(PostProcessingInputReference)} for RenderPassId {RenderPassId} has not been resolved.");

        [XmlAttribute]
        public int RenderPassId { get; set; }
        [XmlIgnore]
        public bool RenderPassIdSpecified { get; set; }
        [XmlAttribute]
        public bool PreImgui { get; set; } = false; // Allows post imgui shaders to access pre imgui shader inputs
        [XmlIgnore]
        public bool PreImguiSpecified { get; set; }

        public int LookupIndex { get; set; }

        public VkDescriptorType DescriptorType => VkDescriptorType.CombinedImageSampler;
        public int DescriptorCount => 1;

        public IShaderBinding Get() => this;

        public override bool IsReference() => false;


        public PostProcessingInputReference()
        {
        }

        public void Initialize(PostProcessingShaderAsset parent)
        {
            if (!RenderPassIdSpecified) throw new MissingFieldException($"{nameof(PostProcessingInputReference)} for PostProcessingShaderAsset {parent.Id} must specify a RenderPassId.");

            PreImgui = true; // pre imgui shaders can only use pre imgui inputs
        }

        public void Initialize(GlobalPostShaderAsset parent)
        {
            if (!RenderPassIdSpecified) throw new MissingFieldException($"{nameof(PostProcessingInputReference)} for GlobalPostShaderAsset {parent.Id} must specify a RenderPassId.");

            if (!PreImguiSpecified) PreImgui = false;
        }

        public void Resolve(RenderImage attachment)
        {
            this.attachment = attachment;
            resolved = true;
        }

        public void WriteDescriptors(BindingDescriptorWrites write)
        {
            if (!resolved) throw new InvalidOperationException($"{nameof(PostProcessingInputReference)} for RenderPassId {RenderPassId} has not been resolved.");

            write.ImageInfo[0] = new VkDescriptorImageInfo
            {
                ImageView = attachment.ImageView,
                ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                Sampler = Program.LinearClampedSampler,
            };
        }

        public override SerializedId Populate() => throw new NotImplementedException();
        public override TableString.Row ToRow() => new();
    }
}
