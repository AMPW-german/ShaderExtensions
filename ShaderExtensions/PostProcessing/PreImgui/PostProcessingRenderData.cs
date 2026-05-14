using Brutal;
using Brutal.Collections;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using static KSA.Framebuffer;

namespace ShaderExtensions.PostProcessing.PreImgui
{
    internal class PostProcessingRenderData : IComparable<PostProcessingRenderData>, IDisposable
    {
        public int SubPassCount { get; private set; }
        public bool UniqueRenderPass { get; private set; }
        public int RenderPassIndex { get; private set; }
        public RenderPassState RenderPass { get; private set; }
        public FramebufferAttachment[] Attachments { get; private set; }
        public FramebufferAttachment SourceAttachment { get; private set; }
        public FramebufferAttachment TargetAttachment => Attachments.LastOrDefault();
        public VkFramebuffer RenderFramebuffer { get; private set; }
        public SortedDictionary<int, PostProcessingRenderer> PostProcessShaders { get; private set; }
        public string Name { get; private set; }

        public void Dispose()
        {
            Renderer renderer = Program.GetRenderer();
            renderer.Device.DestroyRenderPass(RenderPass.Pass, null);
            renderer.Device.DestroyFramebuffer(RenderFramebuffer, null);
            FramebufferAttachment[] attachments = Attachments;
            foreach (FramebufferAttachment attachment in attachments)
            {
                renderer.Device.DestroyImageView(attachment.ImageView, null);
                attachment.AllocatedImage.Dispose();
            }
            foreach (var shader in PostProcessShaders.Values)
            {
                shader.Dispose();
            }
        }


        public unsafe void Render(CommandBuffer commandBuffer)
        {
            Renderer renderer = Program.GetRenderer();
            VkExtent2D extent = renderer.Extent;

            commandBuffer.PipelineBarrier(
                srcStageMask: VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstStageMask: VkPipelineStageFlags.ColorAttachmentOutputBit,
                dependencyFlags: VkDependencyFlags.None,
                pMemoryBarriers: ReadOnlySpan<VkMemoryBarrier>.Empty,
                pBufferMemoryBarriers: ReadOnlySpan<VkBufferMemoryBarrier>.Empty,
                pImageMemoryBarriers: new VkImageMemoryBarrier[]
                {
                        new VkImageMemoryBarrier
                        {
                            SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
                            DstAccessMask = VkAccessFlags.ShaderReadBit,
                            OldLayout = VkImageLayout.ColorAttachmentOptimal,
                            NewLayout = VkImageLayout.ShaderReadOnlyOptimal,
                            SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                            DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                            Image = SourceAttachment.Image,
                            SubresourceRange = SourceAttachment.SubresourceRange
                        }
                }
            );

            if (UniqueRenderPass)
            {
                PostProcessShaders[0].RenderSinglePass(commandBuffer, RenderPass, RenderFramebuffer);
            }
            else
            {
                commandBuffer.BeginRenderPass(new VkRenderPassBeginInfo
                {
                    RenderPass = RenderPass.Pass,
                    Framebuffer = RenderFramebuffer,
                    RenderArea = new VkRect2D(extent),
                }, VkSubpassContents.Inline);

                for (int i = 0; i < SubPassCount; i++)
                {
                    PostProcessShaders[i].RenderSubpass(commandBuffer);

                    if (i < SubPassCount - 1) commandBuffer.NextSubpass(VkSubpassContents.Inline);
                }

                commandBuffer.EndRenderPass();
            }
        }

        public int CompareTo(PostProcessingRenderData other)
        {
            return RenderPassIndex.CompareTo(other.RenderPassIndex);
        }

        public unsafe PostProcessingRenderData(List<PostProcessingShaderAsset> shaders, FramebufferAttachment source, VkFormat colorFormat)
        {
            int uniqueRenderPassCount = shaders.Count(x => x.RequiresUniqueRenderpass);
            if (uniqueRenderPassCount > 1 || uniqueRenderPassCount == 1 && shaders.Count > 1)
            {
                throw new Exception("Multiple unique renderpass shaders in the same pass are not supported.");
            }

            Name = $"ShaderExtensions_PostProcessingRenderData_Pass{shaders[0].RenderPassId}{(uniqueRenderPassCount > 0 ? "_Unique" : "")}";

            Renderer renderer = Program.GetRenderer();
            RenderPassIndex = shaders[0].RenderPassId;
            SubPassCount = shaders.Count;
            SourceAttachment = source;

            VkExtent3D extent = new VkExtent3D
            {
                Width = renderer.Extent.Width,
                Height = renderer.Extent.Height,
                Depth = 1
            };

            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange();
            {
                subresourceRange.AspectMask = VkImageAspectFlags.ColorBit;
                subresourceRange.LevelCount = 1;
                subresourceRange.LayerCount = 1;
                subresourceRange.BaseMipLevel = 0;
                subresourceRange.BaseArrayLayer = 0;
            }

            VkImageUsageFlags imageUsageFlags = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.InputAttachmentBit | VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.SampledBit;

            if (SubPassCount == 1 && shaders[0].RequiresUniqueRenderpass)
            {
                UniqueRenderPass = true;

                Attachments = new FramebufferAttachment[1]; // Sampler2D as input, ColorAttachment as output

                FramebufferAttachment attachment = new FramebufferAttachment();
                attachment.Format = colorFormat;
                attachment.AllocatedImage = renderer.Allocator.CreateImage(new ImageEx.CreateInfo
                {
                    Name = $"{Name}_Attachment0",
                    ImageType = VkImageType._2D,
                    ImageFormat = colorFormat,
                    ImageExtent = extent,
                    ImageMipLevels = 1,
                    ImageArrayLayers = 1,
                    ImageSamples = VkSampleCountFlags._1Bit,
                    ImageTiling = VkImageTiling.Optimal,
                    ImageUsage = imageUsageFlags,
                    ImageInitialLayout = VkImageLayout.Undefined,
                    AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
                });
                attachment.SubresourceRange = subresourceRange;
                attachment.ImageView = renderer.Device.CreateImageView(new VkImageViewCreateInfo
                {
                    Image = attachment.Image,
                    ViewType = VkImageViewType._2D,
                    Format = colorFormat,
                    SubresourceRange = subresourceRange
                }, null);
                VkImageViewCreateInfo imageViewCreateInfo = new VkImageViewCreateInfo
                {
                    Image = attachment.Image,
                    ViewType = VkImageViewType._2D,
                    Format = colorFormat,
                    SubresourceRange = subresourceRange
                };
                attachment.ImageView = renderer.Device.CreateImageView(imageViewCreateInfo, null);

                Attachments[0] = attachment;

                RenderPass = PostProcessingRenderer.CreateSingleRenderPass(renderer, colorFormat);

                VkImageView* views = stackalloc VkImageView[1];
                views[0] = attachment.ImageView;
                VkFramebufferCreateInfo fbInfo = new VkFramebufferCreateInfo
                {
                    RenderPass = RenderPass.Pass,
                    AttachmentCount = 1,
                    Attachments = views,
                    Width = renderer.Extent.Width,
                    Height = renderer.Extent.Height,
                    Layers = 1
                };
                RenderFramebuffer = renderer.Device.CreateFramebuffer(fbInfo, null);

                PostProcessShaders = new SortedDictionary<int, PostProcessingRenderer> {
                    {
                        0, new PostProcessingRenderer(renderer, source, RenderPass, shaders[0], uniqueRenderpass: true)
                    }
                };
            }
            else
            {
                Attachments = new FramebufferAttachment[SubPassCount + 1];
                Attachments[0] = source;

                for (int i = 1; i <= SubPassCount; i++)
                {
                    FramebufferAttachment attachment = new FramebufferAttachment();
                    attachment.Format = colorFormat;
                    attachment.AllocatedImage = renderer.Allocator.CreateImage(new ImageEx.CreateInfo
                    {
                        Name = $"{Name}_Attachment{i}",
                        ImageType = VkImageType._2D,
                        ImageFormat = colorFormat,
                        ImageExtent = extent,
                        ImageMipLevels = 1,
                        ImageArrayLayers = 1,
                        ImageSamples = VkSampleCountFlags._1Bit,
                        ImageTiling = VkImageTiling.Optimal,
                        ImageUsage = imageUsageFlags,
                        ImageInitialLayout = VkImageLayout.Undefined,
                        AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
                    });
                    attachment.SubresourceRange = subresourceRange;
                    attachment.ImageView = renderer.Device.CreateImageView(new VkImageViewCreateInfo
                    {
                        Image = attachment.Image,
                        ViewType = VkImageViewType._2D,
                        Format = colorFormat,
                        SubresourceRange = subresourceRange
                    }, null);
                    VkImageViewCreateInfo imageViewCreateInfo = new VkImageViewCreateInfo
                    {
                        Image = attachment.Image,
                        ViewType = VkImageViewType._2D,
                        Format = colorFormat,
                        SubresourceRange = subresourceRange
                    };
                    attachment.ImageView = renderer.Device.CreateImageView(imageViewCreateInfo, null);
                    Attachments[i] = attachment;
                }

                RenderPass = PostProcessingRenderer.CreateMultiRenderPass(renderer, SubPassCount, colorFormat);

                VkImageView* views = stackalloc VkImageView[SubPassCount + 1];
                for (int i = 0; i <= SubPassCount; i++) views[i] = Attachments[i].ImageView;

                VkFramebufferCreateInfo fbInfo = new VkFramebufferCreateInfo
                {
                    RenderPass = RenderPass.Pass,
                    AttachmentCount = SubPassCount + 1,
                    Attachments = views,
                    Width = renderer.Extent.Width,
                    Height = renderer.Extent.Height,
                    Layers = 1
                };
                RenderFramebuffer = renderer.Device.CreateFramebuffer(fbInfo, null);

                shaders.Sort((a, b) => a.SubpassId.CompareTo(b.SubpassId));
                PostProcessShaders = new SortedDictionary<int, PostProcessingRenderer>();
                for (int i = 0; i < shaders.Count; i++)
                {
                    PostProcessingShaderAsset shader = shaders[i];
                    FramebufferAttachment input = Attachments[i];
                    PostProcessingRenderer finalPostRenderer = new PostProcessingRenderer(renderer, input, RenderPass, shader, subPass: i);
                    PostProcessShaders.Add(i, finalPostRenderer);
                }
            }
        }
    }
}