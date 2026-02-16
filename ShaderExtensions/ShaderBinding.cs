
using System;
using System.Xml.Serialization;
using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;

namespace ShaderExtensions
{
    public ref struct BindingDescriptorWrites
    {
      public Span<VkDescriptorImageInfo> ImageInfo;
      public Span<VkDescriptorBufferInfo> BufferInfo;
      public Span<VkBufferView> TexelBufferView;
    }

    public interface IShaderBinding : ILibraryData, IKeyed
    {
      public static readonly LookupCollection<IShaderBinding> Bindings = new("shader bindings");

      public VkDescriptorType DescriptorType { get; }
      public int DescriptorCount { get; }
      public IShaderBinding Get();
      public void WriteDescriptors(BindingDescriptorWrites write);
    }

    public class TextureBindingReference : TextureReference, IShaderBinding
    {
      public VkDescriptorType DescriptorType => VkDescriptorType.CombinedImageSampler;
      public int DescriptorCount => 1;
      IShaderBinding IShaderBinding.Get() => IsReference() ? IShaderBinding.Bindings.Get(Hash) : this;

      public void WriteDescriptors(BindingDescriptorWrites write)
      {
        write.ImageInfo[0] = new VkDescriptorImageInfo
        {
          ImageView = ImageView,
          ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
          Sampler = Program.LinearClampedSampler,
        };
      }

      public override void OnDataLoad(Mod mod)
      {
        base.OnDataLoad(mod);
        if (!IsReference())
          IShaderBinding.Bindings.Register(this);
      }
    }

    public class UniformBindingReference<T> : SerializedId, IShaderBinding, IBinder
      where T : unmanaged
    {
      public static readonly LookupCollection<UniformBindingReference<T>> Buffers =
        new($"uniform buffers of {typeof(T)}");

      public static BufferEx GetBuffer(KeyHash key) => Buffers.Get(key).buffer;
      public static MappedMemory GetMappedMemory(KeyHash key) => Buffers.Get(key).mappedMemory;
      public static Span<T> GetSpan(KeyHash key) => Buffers.Get(key).mappedMemory.AsSpan<T>();
      public static unsafe T* GetPtr(KeyHash key) => (T*)Buffers.Get(key).mappedMemory.MapPtr;

      [XmlAttribute("Size")]
      public int Size = 0;

      private BufferEx buffer;
      private MappedMemory mappedMemory;

      public VkDescriptorType DescriptorType => VkDescriptorType.UniformBufferDynamic;
      public int DescriptorCount => 1;
      public IShaderBinding Get() => IsReference() ? IShaderBinding.Bindings.Get(Hash) : this;

      public override bool IsReference() => Size <= 0;

      public override void OnDataLoad(Mod mod)
      {
        base.OnDataLoad(mod);
        if (IsReference())
          return;

        IShaderBinding.Bindings.Register(this);
        Buffers.Register(this);
        ModLibrary.RegisterBinder(this);
      }

      public void Bind(Renderer renderer, StagingPool stagingPool)
      {
        buffer = renderer.Device.CreateBuffer(new BufferEx.CreateInfo
        {
          Name = "UniformBindingReference.UniformBuffer",
          BufferUsage = VkBufferUsageFlags.UniformBufferBit,
          BufferSize = ByteSize.Of<T>() * Size,
          AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit,
        });
        mappedMemory = buffer.Map();
      }

      public void WriteDescriptors(BindingDescriptorWrites write)
      {
        write.BufferInfo[0] = new VkDescriptorBufferInfo
        {
          Buffer = buffer.VkBuffer,
          Offset = ByteSize.Zero,
          Range = buffer.BufferSize,
        };
      }

      public override SerializedId Populate() => throw new NotImplementedException();
      public override TableString.Row ToRow() => new();
    }
}