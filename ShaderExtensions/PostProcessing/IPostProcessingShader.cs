using KSA;

namespace ShaderExtensions.PostProcessing
{
    /// <summary>
    /// Common metadata contract for pre- and post-imgui post processing shader assets.
    /// </summary>
    public interface IPostProcessingShader
    {
        /// <summary>
        /// Ordering key of the shader inside the post processing chain.
        /// </summary>
        int PassId { get; }

        /// <summary>
        /// Screenspace vertex shader used to draw the fullscreen quad.
        /// </summary>
        ShaderReference VertexShaderReference { get; }
    }
}
