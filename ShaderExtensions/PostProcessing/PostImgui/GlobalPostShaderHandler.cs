using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;

namespace ShaderExtensions.PostProcessing.PostImgui
{
    /// <summary>
    /// Runs the post-imgui post processing chain. The finished swapchain image (game + UI) is copied into a
    /// tracked render image, the chain is executed on it, and the result is copied back to the swapchain image.
    /// </summary>
    internal static class GlobalPostShaderHandler
    {
        private static readonly PostProcessingChain Chain = new(preImgui: false);

        /// <summary>Holds a copy of the finished swapchain image, used as the chain source.</summary>
        internal static RenderTarget SourceTarget;

        public static void Rebuild()
        {
            Renderer renderer = Program.GetRenderer();
            renderer.Device.WaitIdle();

            EnsureSourceTarget(renderer);

            Chain.Build(
                "ShaderExtensions_PostImgui",
                PostProcessingChain.FilterSupported(GlobalPostShaderAsset.AllShaders),
                SourceTarget.ColorImage);
        }

        private static void EnsureSourceTarget(Renderer renderer)
        {
            if (SourceTarget is not null &&
                SourceTarget.Extent.Width == renderer.Extent.Width &&
                SourceTarget.Extent.Height == renderer.Extent.Height)
            {
                return;
            }

            SourceTarget?.Dispose();
            SourceTarget = new RenderTarget(
                renderer,
                "ShaderExtensions_PostImguiSource",
                renderer.Extent,
                renderer.ColorFormat,
                VkFormat.Undefined);
        }

        /// <summary>
        /// Called after the UI renderpass has finished.
        /// </summary>
        public static void RenderNow(CommandBuffer commandBuffer, FrameResources destFrameResources, int dynamicOffset = 0)
        {
            if (Chain.PassCount == 0) return;

            PostProcessingChain.CopyFromRaw(
                commandBuffer,
                destFrameResources.ColorImage,
                VkImageLayout.PresentSrcKHR,
                SourceTarget.ColorImage);

            Chain.Render(commandBuffer);

            PostProcessingChain.CopyToRaw(
                commandBuffer,
                Chain.Output,
                destFrameResources.ColorImage,
                VkImageLayout.PresentSrcKHR);
        }

        public static void Dispose()
        {
            Chain.Dispose();
            SourceTarget?.Dispose();
            SourceTarget = null;
        }
    }
}
