using Brutal.Logging;
using Brutal.VulkanApi;
using Core;
using KSA;
using KSA.Rendering;

namespace ShaderExtensions.PostProcessing
{
    /// <summary>
    /// A flat, RenderPassId ordered chain of fullscreen post processing passes.
    /// Every shader renders into its own <see cref="RenderTarget"/> and samples the output of the previous pass.
    /// </summary>
    internal sealed class PostProcessingChain : IDisposable
    {
        private readonly List<RenderTarget> targets = [];
        private readonly List<PostProcessingPassRenderer> passes = [];
        private readonly List<RenderImage> sampledReferences = [];
        private readonly bool preImgui;
        private bool disposed;

        /// <summary>The image the game rendered into, before any of our passes ran.</summary>
        public RenderImage Source { get; private set; }

        /// <summary>The final image of the chain, or <see cref="Source"/> when the chain is empty.</summary>
        public RenderImage Output => passes.Count > 0 ? passes[^1].Output : Source;

        public int PassCount => passes.Count;

        public PostProcessingChain(bool preImgui) => this.preImgui = preImgui;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            foreach (var pass in passes) pass.Dispose();
            passes.Clear();

            foreach (var target in targets) target.Dispose();
            targets.Clear();

            sampledReferences.Clear();
        }

        /// <summary>
        /// Rebuilds the chain from the given shaders. Shaders are executed in ascending RenderPassId order,
        /// and in load order within the same RenderPassId.
        /// </summary>
        public void Build<T>(string name, IEnumerable<T> shaders, RenderImage source)
            where T : ShaderEx, IPostProcessingShader
        {
            Renderer renderer = Program.GetRenderer();

            foreach (var pass in passes) pass.Dispose();
            passes.Clear();
            foreach (var target in targets) target.Dispose();
            targets.Clear();
            sampledReferences.Clear();

            PostProcessingInputResolver.Clear(preImgui);

            Source = source;

            RenderImage current = source;
            int index = 0;

            foreach (T shader in shaders.OrderBy(s => s.PassId))
            {
                RenderTarget target = new(
                    renderer,
                    $"{name}_Pass{shader.PassId}_{index}",
                    renderer.Extent,
                    source.Format,
                    VkFormat.Undefined);
                targets.Add(target);

                // The input of this shader is whatever the previous stage produced.
                PostProcessingInputResolver.RegisterInput(preImgui, shader.PassId, current);
                PostProcessingInputResolver.Resolve(shader);
                CollectSampledReferences(shader);

                PostProcessingPassRenderer pass = new(
                    renderer, current, target, shader, shader.VertexShaderReference);
                passes.Add(pass);

                PostProcessingInputResolver.RegisterOutput(preImgui, shader.PassId, target.ColorImage);

                current = target.ColorImage;
                index++;
            }
        }

        /// <summary>
        /// Records the whole chain. Does nothing when the chain is empty.
        /// </summary>
        public void Render(CommandBuffer commandBuffer)
        {
            if (passes.Count == 0) return;

            // Make sure the game rendered image and every cross referenced image can be sampled.
            commandBuffer.PipelineBarrier2(Source, ImageBarrierInfo.Presets.SampledReadF);
            foreach (RenderImage reference in sampledReferences)
                commandBuffer.PipelineBarrier2(reference, ImageBarrierInfo.Presets.SampledReadF);

            foreach (var pass in passes)
                pass.Render(commandBuffer);
        }

        /// <summary>
        /// Copies the chain result into another tracked render image.
        /// </summary>
        public static void CopyTo(CommandBuffer commandBuffer, RenderImage source, RenderImage destination)
        {
            if (source.Image.Equals(destination.Image)) return;

            commandBuffer.PipelineBarrier2(source, ImageBarrierInfo.Presets.TransferSrc);
            commandBuffer.PipelineBarrier2(destination, ImageBarrierInfo.Presets.TransferDst);

            commandBuffer.CopyImage(
                srcImage: source.Image,
                srcImageLayout: VkImageLayout.TransferSrcOptimal,
                dstImage: destination.Image,
                dstImageLayout: VkImageLayout.TransferDstOptimal,
                pRegions: [MakeRegion(source)]);
        }

        /// <summary>
        /// Copies the chain result into an untracked image (for example a swapchain image).
        /// The destination is transitioned to transfer-dst and back to the given layout.
        /// </summary>
        public static unsafe void CopyToRaw(
            CommandBuffer commandBuffer,
            RenderImage source,
            VkImage destination,
            VkImageLayout destinationLayout)
        {
            commandBuffer.PipelineBarrier2(source, ImageBarrierInfo.Presets.TransferSrc);

            VkImageSubresourceRange range = new()
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            };

            Transition(commandBuffer, destination, range, destinationLayout, VkImageLayout.TransferDstOptimal);

            commandBuffer.CopyImage(
                srcImage: source.Image,
                srcImageLayout: VkImageLayout.TransferSrcOptimal,
                dstImage: destination,
                dstImageLayout: VkImageLayout.TransferDstOptimal,
                pRegions: [MakeRegion(source)]);

            Transition(commandBuffer, destination, range, VkImageLayout.TransferDstOptimal, destinationLayout);
        }

        /// <summary>
        /// Copies an untracked image (for example a swapchain image) into a tracked render image.
        /// </summary>
        public static void CopyFromRaw(
            CommandBuffer commandBuffer,
            VkImage source,
            VkImageLayout sourceLayout,
            RenderImage destination)
        {
            VkImageSubresourceRange range = new()
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            };

            Transition(commandBuffer, source, range, sourceLayout, VkImageLayout.TransferSrcOptimal);
            commandBuffer.PipelineBarrier2(destination, ImageBarrierInfo.Presets.TransferDst);

            commandBuffer.CopyImage(
                srcImage: source,
                srcImageLayout: VkImageLayout.TransferSrcOptimal,
                dstImage: destination.Image,
                dstImageLayout: VkImageLayout.TransferDstOptimal,
                pRegions: [MakeRegion(destination)]);

            Transition(commandBuffer, source, range, VkImageLayout.TransferSrcOptimal, sourceLayout);
        }

        private static VkImageCopy MakeRegion(RenderImage image) => new()
        {
            SrcSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcOffset = new VkOffset3D(0, 0, 0),
            DstSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            DstOffset = new VkOffset3D(0, 0, 0),
            Extent = new VkExtent3D
            {
                Width = image.Width,
                Height = image.Height,
                Depth = 1
            }
        };

        private static void Transition(
            CommandBuffer commandBuffer,
            VkImage image,
            VkImageSubresourceRange range,
            VkImageLayout oldLayout,
            VkImageLayout newLayout)
        {
            if (oldLayout == newLayout) return;

            VkImageMemoryBarrier2 barrier = new()
            {
                SrcStageMask = VkPipelineStageFlags2.AllCommandsBit,
                SrcAccessMask = VkAccessFlags2.MemoryWriteBit,
                DstStageMask = VkPipelineStageFlags2.AllCommandsBit,
                DstAccessMask = VkAccessFlags2.MemoryReadBit | VkAccessFlags2.MemoryWriteBit,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                Image = image,
                SubresourceRange = range,
            };

            commandBuffer.PipelineBarrier2(
                ReadOnlySpan<VkMemoryBarrier2>.Empty,
                ReadOnlySpan<VkBufferMemoryBarrier2>.Empty,
                [barrier]);
        }

        private void CollectSampledReferences(ShaderEx shader)
        {
            foreach (PostProcessingInputReference input in shader.XmlBindings.OfType<PostProcessingInputReference>())
                AddUnique(input.Attachment);
            foreach (PostProcessingOutputReference output in shader.XmlBindings.OfType<PostProcessingOutputReference>())
                AddUnique(output.Attachment);
        }

        private void AddUnique(RenderImage image)
        {
            if (image is null) return;
            if (sampledReferences.Any(existing => existing.Image.Equals(image.Image))) return;
            sampledReferences.Add(image);
        }

        /// <summary>
        /// Filters out shaders that use metadata which is no longer supported and logs an error for each of them.
        /// </summary>
        public static List<T> FilterSupported<T>(IEnumerable<T> shaders) where T : ShaderEx, IPostProcessingShader
        {
            List<T> supported = [];

            foreach (T shader in shaders)
            {
                if (shader is IPostProcessingLegacyMetadata legacy && legacy.HasUnsupportedMetadata(out string reason))
                {
                    DefaultCategory.Log.Error(
                        $"[ShaderExtensions] Skipping post processing shader '{shader.Id}': {reason}. " +
                        "Subpasses and unique renderpasses are no longer supported, every shader now renders into its own target. " +
                        "Remove the attribute and order the shader with RenderPassId instead.");
                    continue;
                }

                supported.Add(shader);
            }

            return supported;
        }
    }

    /// <summary>
    /// Implemented by shader assets that still accept removed metadata attributes so they can be rejected with a clear message.
    /// </summary>
    internal interface IPostProcessingLegacyMetadata
    {
        bool HasUnsupportedMetadata(out string reason);
    }
}
