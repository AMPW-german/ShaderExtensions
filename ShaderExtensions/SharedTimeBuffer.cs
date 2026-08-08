using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ShaderExtensions
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ShaderTimeData
    {
        public uint FrameNumber;
        public float DeltaTime;
        public float RealTimeSinceStart;
        public float TimeSinceStart;
        public float TimeWarpSpeed;
    }

    public sealed class SharedTimeBuffer : IDisposable
    {
        private static SharedTimeBuffer? TimeBuffer;

        public static bool IsInitialized => TimeBuffer is not null;

        public static void Initialize(Renderer renderer)
        {
            TimeBuffer?.Dispose();
            TimeBuffer = new SharedTimeBuffer();
            TimeBuffer.Bind(renderer);
        }

        public static void UpdateInstance(in ShaderTimeData value)
        {
            TimeBuffer?.Update(value);
        }

        public static VkDescriptorBufferInfo GetDescriptorInfoInstance()
        {
            if (TimeBuffer is null)
                throw new InvalidOperationException("Shared time buffer has not been initialized.");

            return TimeBuffer.GetDescriptorInfo();
        }

        public static void DisposeInstance()
        {
            TimeBuffer?.Dispose();
            TimeBuffer = null;
        }

        private BufferEx buffer;
        private MappedMemory mappedMemory;

        public VkBuffer Buffer => buffer.VkBuffer;

        public void Bind(Renderer renderer)
        {
            buffer = renderer.Device.CreateBuffer(new BufferEx.CreateInfo
            {
                Name = nameof(SharedTimeBuffer),
                BufferUsage = VkBufferUsageFlags.UniformBufferBit,
                BufferSize = ByteSize.Of<ShaderTimeData>(),
                AllocRequiredProperties =
                    VkMemoryPropertyFlags.HostVisibleBit |
                    VkMemoryPropertyFlags.HostCoherentBit,
            });

            mappedMemory = buffer.Map();
        }

        public void Update(in ShaderTimeData value)
        {
            mappedMemory.AsSpan<ShaderTimeData>()[0] = value;
        }

        public VkDescriptorBufferInfo GetDescriptorInfo() => new()
        {
            Buffer = buffer.VkBuffer,
            Offset = ByteSize.Zero,
            Range = ByteSize.Of<ShaderTimeData>(),
        };

        public void Dispose()
        {
            mappedMemory.Dispose();
            buffer.Dispose();
        }
    }
}
