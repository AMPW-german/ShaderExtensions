using Brutal.VulkanApi;
using Core;
using KSA;
using ShaderExtensions.PostProcessing.PostImgui;
using ShaderExtensions.PostProcessing.PreImgui;
using System.Xml.Serialization;

namespace ShaderExtensions.PostProcessing
{
    /// <summary>
    /// Adds a texture binding to the post-processing shader, allowing it to access the target image from another post-processing shader.
    /// </summary>
    public class PostProcessingOutputReference : SerializedId, IShaderBinding
    {
        private Framebuffer.FramebufferAttachment attachment;
        private bool resolved;

        internal Framebuffer.FramebufferAttachment Attachment => resolved
            ? attachment
            : throw new InvalidOperationException($"{nameof(PostProcessingOutputReference)} for RenderPassId {RenderPassId}, SubpassId {SubpassId} has not been resolved.");

        [XmlAttribute]
        public int RenderPassId { get; set; }
        [XmlIgnore]
        public bool RenderPassIdSpecified { get; set; }
        [XmlAttribute]
        public int SubpassId { get; set; }
        [XmlIgnore]
        public bool SubpassIdSpecified { get; set; }
        [XmlAttribute]
        public bool PreImgui { get; set; } = false; // Allows post imgui shaders to access pre imgui shader outputs
        [XmlIgnore]
        public bool PreImguiSpecified { get; set; }

        public int LookupIndex { get; set; }

        public VkDescriptorType DescriptorType => VkDescriptorType.CombinedImageSampler;
        public int DescriptorCount => 1;

        public IShaderBinding Get() => this;

        public override bool IsReference() => false;

        public PostProcessingOutputReference()
        {
        }

        public void Initialize(PostProcessingShaderAsset parent)
        {
            if (!SubpassIdSpecified)
            {
                throw new InvalidOperationException($"{nameof(SubpassId)} is required.");
            }

            if (!RenderPassIdSpecified)
            {
                RenderPassId = parent.RenderPassId;
            }

            PreImgui = true; // pre imgui shaders can only use pre imgui outputs
        }

        public void Initialize(GlobalPostShaderAsset parent)
        {
            if (!SubpassIdSpecified)
            {
                throw new InvalidOperationException($"{nameof(SubpassId)} is required.");
            }

            if (!RenderPassIdSpecified)
            {
                RenderPassId = parent.RenderPassId;
            }

            if (!PreImguiSpecified)
            {
                PreImgui = false;
            }
        }

        public void Resolve(Framebuffer.FramebufferAttachment attachment)
        {
            this.attachment = attachment;
            resolved = true;
        }

        public void WriteDescriptors(BindingDescriptorWrites write)
        {
            if (!resolved)
            {
                throw new InvalidOperationException($"{nameof(PostProcessingOutputReference)} for RenderPassId {RenderPassId}, SubpassId {SubpassId} has not been resolved.");
            }

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
