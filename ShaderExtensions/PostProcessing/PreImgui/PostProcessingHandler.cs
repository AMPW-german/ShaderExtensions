using Brutal.VulkanApi;
using Core;
using KSA;
using KSA.Rendering;
using System.Reflection;

namespace ShaderExtensions.PostProcessing.PreImgui
{
    /// <summary>
    /// Runs the pre-imgui post processing chain on the game offscreen target before the UI is drawn.
    /// </summary>
    internal static class PostProcessingHandler
    {
        private static readonly PostProcessingChain Chain = new(preImgui: true);

        private static readonly FieldInfo OffscreenTargetField =
            typeof(Program).GetField("_offscreenTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static RenderTarget GameOffscreenTarget =>
            (RenderTarget)OffscreenTargetField.GetValue(Program.Instance);

        public static void Rebuild()
        {
            Program.GetRenderer().Device.WaitIdle();

            RenderTarget offscreenTarget = GameOffscreenTarget;
            if (offscreenTarget is null) return;

            Chain.Build(
                "ShaderExtensions_PreImgui",
                PostProcessingChain.FilterSupported(PostProcessingShaderAsset.AllShaders),
                offscreenTarget.ColorImage);
        }

        /// <summary>
        /// Called before the UI renderpass.
        /// </summary>
        public static void RenderNow(CommandBuffer commandBuffer)
        {
            if (Chain.PassCount == 0) return;

            Chain.Render(commandBuffer);

            // Feed the result back into the image the game composites from.
            PostProcessingChain.CopyTo(commandBuffer, Chain.Output, GameOffscreenTarget.ColorImage);
        }

        public static void Dispose() => Chain.Dispose();
    }
}
