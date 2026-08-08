using KSA;
using KSA.Rendering;

namespace ShaderExtensions.PostProcessing
{
    internal readonly record struct PostProcessingInputKey(bool PreImgui, int RenderPassId);

    internal static class PostProcessingInputResolver
    {
        private static readonly Dictionary<PostProcessingInputKey, RenderImage> Inputs = [];
        private static readonly Dictionary<PostProcessingInputKey, RenderImage> Outputs = [];

        public static void Clear(bool preImgui)
        {
            foreach (PostProcessingInputKey key in Inputs.Keys.Where(key => key.PreImgui == preImgui).ToArray())
                Inputs.Remove(key);
            foreach (PostProcessingInputKey key in Outputs.Keys.Where(key => key.PreImgui == preImgui).ToArray())
                Outputs.Remove(key);
        }

        public static void RegisterInput(bool preImgui, int renderPassId, RenderImage attachment)
        {
            Inputs[new PostProcessingInputKey(preImgui, renderPassId)] = attachment;
        }

        public static void RegisterOutput(bool preImgui, int renderPassId, RenderImage attachment)
        {
            Outputs[new PostProcessingInputKey(preImgui, renderPassId)] = attachment;
        }

        public static void Resolve(PostProcessingInputReference input)
        {
            PostProcessingInputKey key = new(input.PreImgui, input.RenderPassId);
            if (!Inputs.TryGetValue(key, out RenderImage attachment))
            {
                string stage = input.PreImgui ? "pre-imgui" : "post-imgui";
                throw new InvalidOperationException($"No {stage} post-processing input found for RenderPassId {input.RenderPassId}.");
            }

            input.Resolve(attachment);
        }

        public static void Resolve(PostProcessingOutputReference output)
        {
            PostProcessingInputKey key = new(output.PreImgui, output.RenderPassId);
            if (!Outputs.TryGetValue(key, out RenderImage attachment))
            {
                string stage = output.PreImgui ? "pre-imgui" : "post-imgui";
                throw new InvalidOperationException($"No {stage} post-processing output found for RenderPassId {output.RenderPassId}.");
            }

            output.Resolve(attachment);
        }

        public static void Resolve(ShaderEx shader)
        {
            foreach (PostProcessingInputReference input in shader.XmlBindings.OfType<PostProcessingInputReference>())
                Resolve(input);
            foreach (PostProcessingOutputReference output in shader.XmlBindings.OfType<PostProcessingOutputReference>())
                Resolve(output);
        }
    }
}
