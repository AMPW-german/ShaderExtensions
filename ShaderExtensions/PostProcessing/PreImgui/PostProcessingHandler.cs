using Brutal.VulkanApi;
using Core;
using KSA;
using KSA.Rendering;

namespace ShaderExtensions.PostProcessing.PreImgui
{
    /// <summary>
    /// Runs the pre-imgui post processing chain on the game offscreen target before the UI is drawn.
    /// </summary>
    internal static class PostProcessingHandler
    {
        private static readonly PostProcessingChain Chain = new(preImgui: true);

        /// <summary>
        /// The scene image the game composites into the final frame. With HDR active the offscreen
        /// target is composited into the HDR present pass scene target before the UI pass runs, so
        /// the chain has to process that image instead.
        /// </summary>
        internal static RenderTarget SceneTarget =>
            Render.HdrOutput.Active ? Program.HdrPresent.GetSceneTarget() : Program.OffscreenTarget;

        public static void Rebuild()
        {
            Program.GetRenderer().Device.WaitIdle();

            RenderTarget sceneTarget = SceneTarget;
            if (sceneTarget is null) return;

            Chain.Build(
                "ShaderExtensions_PreImgui",
                PostProcessingChain.FilterSupported(PostProcessingShaderAsset.AllShaders),
                sceneTarget.ColorImage);
        }

        /// <summary>
        /// Called before the UI renderpass.
        /// </summary>
        public static void RenderNow(CommandBuffer commandBuffer)
        {
            if (Chain.PassCount == 0) return;

            Chain.Render(commandBuffer);

            // Feed the result back into the image the game composites from.
            PostProcessingChain.CopyTo(commandBuffer, Chain.Output, SceneTarget.ColorImage);
            commandBuffer.PipelineBarrier2(SceneTarget.ColorImage, ImageBarrierInfo.Presets.SampledReadVfc);
        }

        public static void Dispose() => Chain.Dispose();
    }
}
