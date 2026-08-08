using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using RenderCore;

namespace ShaderExtensions.PostProcessing
{
    /// <summary>
    /// Renders a single post processing shader as a fullscreen pass into its own render target.
    /// The previous image in the chain is bound as a combined image sampler (set 1, binding 0).
    /// </summary>
    public class PostProcessingPassRenderer : RenderTechnique
    {
        private readonly DescriptorSetLayoutEx bindingLayout;
        private readonly VkDescriptorSet bindingSet;
        private readonly ShaderEx shader;
        private readonly RenderTarget target;

        /// <summary>The image this pass samples from.</summary>
        public RenderImage Source { get; }

        /// <summary>The image this pass renders into.</summary>
        public RenderImage Output => target.ColorImage;

        public unsafe PostProcessingPassRenderer(
            Renderer renderer,
            RenderImage source,
            RenderTarget target,
            ShaderEx shader,
            ShaderReference vertexShader)
            : base(nameof(PostProcessingPassRenderer), renderer, target, [vertexShader, shader])
        {
            this.shader = shader;
            this.target = target;
            Source = source;

            var device = renderer.Device;

            DescriptorPool = shader.CreateDescriptorPool(device, VkDescriptorType.CombinedImageSampler);
            bindingLayout = shader.CreateDescriptorSetLayout(
                device,
                new VkDescriptorSetLayoutBinding
                {
                    Binding = 0,
                    DescriptorType = VkDescriptorType.CombinedImageSampler,
                    DescriptorCount = 1,
                    StageFlags = VkShaderStageFlags.FragmentBit,
                });
            bindingSet = device.AllocateDescriptorSet(DescriptorPool, bindingLayout);

            VkDescriptorImageInfo* inputInfo = stackalloc VkDescriptorImageInfo[1];
            inputInfo[0] = new VkDescriptorImageInfo
            {
                ImageView = source.ImageView,
                ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                Sampler = Program.LinearClampedSampler,
            };

            shader.UpdateDescriptorSets(device, new VkWriteDescriptorSet
            {
                DstBinding = 0,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                DstSet = bindingSet,
                ImageInfo = inputInfo,
            });

            var pushConstRanges = shader.CreatePushConstantRanges();
            PipelineLayout = device.CreatePipelineLayout(
                [GlobalShaderBindings.DescriptorSetLayout, bindingLayout], pushConstRanges, null);

            RebuildFrameResources();
        }

        protected override VertexInput MakeVertexInput() => null;

        protected override void OnRebuildFrameResources() => CreatePipeline(
            VkPrimitiveTopology.TriangleStrip,
            VkCullModeFlags.BackBit,
            VkFrontFace.CounterClockwise,
            VkPolygonMode.Fill,
            RenderingPresets.ReverseZDepthStencil.NoDepthTest,
            Presets.BlendState.BlendNone,
            out Pipeline);

        /// <summary>
        /// Records the fullscreen pass. The source image must already be in a sampled-read state.
        /// </summary>
        public void Render(CommandBuffer commandBuffer)
        {
            var extent = target.Extent;
            var rect = new VkRect2D(extent);

            target.BeginRendering(
                commandBuffer,
                default,
                default,
                VkAttachmentLoadOp.DontCare,
                VkAttachmentLoadOp.DontCare);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, Pipeline);

            commandBuffer.SetViewport(0, [new VkViewport
            {
                Width = extent.Width,
                Height = extent.Height,
                MinDepth = 0f,
                MaxDepth = 1f,
            }]);
            commandBuffer.SetScissor(0, [rect]);

            shader.PushConstants(commandBuffer, PipelineLayout);

            int bindingCount = Math.Max(1, bindingLayout.Descriptors.Sum(kvp => kvp.Value) - 1);
            Span<Brutal.ByteSize32> dynamicOffsets = stackalloc Brutal.ByteSize32[bindingCount];
            dynamicOffsets[0] = GlobalShaderBindings.DynamicOffset(0);
            dynamicOffsets[1..].Fill(UniformBufferEx.minUniformBufferOffsetAlignment);

            commandBuffer.BindDescriptorSets(
                VkPipelineBindPoint.Graphics, PipelineLayout, 0,
                [GlobalShaderBindings.DescriptorSet, bindingSet],
                dynamicOffsets);

            commandBuffer.Draw(4, 1, 0, 0);

            target.EndRendering(commandBuffer, ImageBarrierInfo.Presets.SampledReadF, default);
        }
    }
}
