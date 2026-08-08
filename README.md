# ShaderExtensions

Post Processing Shaders and uniform buffers for KSA

Current Features:
- Adds a `<ShaderEx>` asset that allows adding additional texture and uniform buffer bindings to fragment shaders
- Adds push constants to `ShaderEx` shaders for small per-draw values
- Adds a `<ImGuiShader>` asset that allows running a custom shader for a specific window
- Adds `<PostProcessingShader>` and `<GlobalPostShader>` assets to run post processing shaders pre/post imgui

## Installation

- Requires [Starmap](https://github.com/StarMapLoader/StarMap) and [KittenExtensions > v0.3.1](https://github.com/tsholmes/KittenExtensions/releases/latest)
- Download zip from [Releases](https://github.com/AMPW-german/ShaderExtensions/releases/latest) and extract into game `Content` folder
- Add to `manifest.toml` in `%USER%/my games/Kitten Space Agency`
    ```toml
    [[mods]]
    id = "ShaderExtensions"
    enabled = true
    ```

This mod is based on [KittenExtensions](https://github.com/tsholmes/KittenExtensions) with large parts unmodified.

## Post Processing Shaders

There are two types of post processing shaders available, pre imgui shaders and post imgui shaders.\
The only difference between them is the execution point and the asset name, otherwise they are identical which is why all of the examples are for the global (post) post processing shaders.

Both types are based on the `ShaderEx` asset so they support the same attributes and additional bindings. The shaders are ordered with the `RenderPassId` attributes. It defaults to the `ScreenspaceVert` shader but custom vertex shaders can be set with the `VertexShaderID` attribute.\
Normal post processing shaders use the `PostProcessingShader` asset and global post processing shaders use the `GlobalPostShader` asset.

### Limitations:

The shaders only target the main window, any other windows are ignored. Additionally, these shaders can't be disabled and will always run at their designated stage (use custom bindings to conditionally return the original color and achieve the same effect as disabling the shaders).\
Each shader gets its own pass, ordered by `RenderPassId`. If more than one shader shares the same `RenderPassId` the execution order between them is no longer guaranteed.

> **Breaking change:** subpasses are no longer supported. The `SubpassId` and `RequiresUniqueRenderpass` attributes have been removed, and shaders that still declare either of them are skipped with an error in the log. Every shader now samples its input through a `sampler2D`; `subpassInput` / `subpassLoad` are no longer available.

### Shader type

Every post processing shader has a `sampler2D` at set 1 binding 0 as pixel color source, which allows free sampling of the input. Ordering is done exclusively through the `RenderPassId` attribute.

```xml
<GlobalPostShader Id="BlurFrag" Path="Shaders/Blur.frag" RenderPassId="16" />
```

```glsl
#version 450 core

layout(location = 0) out vec4 outColor;
layout(set = 1, binding = 0) uniform sampler2D Source;
layout(location = 0) in vec2 Uv;

void main()
{
    vec4 c = texture(Source, Uv);
    outColor = c;
}
```

## ImGui Post-Processing

To add a post-processing shader to an ImGui window, use the `<ImGuiShader>` asset with a vertex and fragment shader specified. The included `ImGuiVertexPost` vertex shader draws one rect covering the bounding box of the imgui rendering calls, and can be used in most cases. The fragment shader is a `ShaderEx` asset that will have `layout(set=0, binding=0)` bound to the rendered ImGui window, with custom bindings starting at `layout(set=0, binding=1)`.

```xml
<Assets>
  <ImGuiShader Id="MyImGuiShader">
    <Vertex Id="ImGuiVertexPost" />
    <Fragment Path="MyImGuiShader.frag" />
  </ImGuiShader>
</Assets>
```

```glsl
#version 450 core

layout(location = 0) out vec4 outColor;
layout(set=0, binding=0) uniform sampler2D imguiTex; // rendered ImGui Window
layout(location = 0) in struct {
  vec2 Px; // screen pixel coord
  vec2 Uv; // screen uv coord
} In;
layout(location = 4) flat in vec4 PxRect; // bounding pixel rect for window
layout(location = 8) flat in vec4 UvRect; // bounding uv rect for window

void main()
{
  outColor = textureLod(imguiTex, In.Uv, 0);
}
```

Then add this helper class to your assembly[^Sximgui].
```cs
using Brutal;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace ShaderExtensions;

internal static class SxImGui
{
  internal static readonly KeyHash MarkerKey = KeyHash.Make("SxImGuiShader");
  internal static unsafe void CustomShader(KeyHash key)
  {
    var data = new uint2(MarkerKey.Code, key.Code);
    ImGui.GetWindowDrawList().AddCallback(DummyCallback, (nint)(&data), ByteSize.Of<uint2>().Bytes);
  }
  private static unsafe void DummyCallback(ImDrawList* parent_list, ImDrawCmd* cmd) { }
}
```

Then in your ImGui code, call the `SxImGui.CustomShader` utility to set the custom shader for the currently rendering ImGui window (from the most recent `ImGui.Begin` call).

```cs
// matches Id attribute of the <ImGuiShader> element
// save this value so you aren't rehashing every frame
KeyHash myShader = KeyHash.Make("MyImGuiShader");

ImGui.Begin("My Window");
SxImGui.CustomShader(myShader);

// your window contents

ImGui.End();
```

### Limitations

The rendering data from ImGui does not include any window information, only a list of `ImDrawList`, so the shader will only be run on the draw list of the rendering window. This does not include child windows, so child window contents will be overlayed on top of the parent window after the custom shader is run.

## Additional bindings

Additional bindings can only be added to a post processing shaders using one of the tags from ShaderExtensions

```xml
<Assets>
  <ShaderEx Id="MyFragmentShader" Path="MyShader.frag">
    <TextureBinding Path="Texture1.png" />
    <TextureBinding Path="Texture2.png" />
    <MyBuffer Id="MyBuf" Size="1" />
  </ShaderEx>
</Assets>
```

```xml
<Component>
  <FragmentEx Path="MyShader.frag">
    <TextureBinding Path="Texture1.png" />
    <TextureBinding Path="Texture2.png" />
    <MyBuffer Id="MyBuf" Size="1" />
  </FragmentEx>
</Component>
```

The additional bindings will be available in the fragment shader on set 1, starting from binding 1

```glsl
// in MyShader.frag
layout(set = 1, binding = 1) uniform sampler2D texture1;
layout(set = 1, binding = 2) uniform sampler2D texture2;
layout(set = 1, binding = 3) uniform MyBuffer {
  float v1;
  float v2;
};
```

## Uniform Buffers

To use uniform buffers, first add the uniform buffer attributes to your assembly. They must be defined in the `ShaderExtensions` namespace and at least one of these attributes must be defined in the **same** assembly as the uniform buffer struct.
```cs
#pragma warning disable CS9113
using System;
using KSA;
namespace ShaderExtensions
{

  [AttributeUsage(AttributeTargets.Struct)]
  internal class SxUniformBufferAttribute(string xmlElement) : Attribute;

  [AttributeUsage(AttributeTargets.Field)]
  internal class SxUniformBufferLookupAttribute() : Attribute;

  // You can use your own delegate types as long as the signature matches one of these
  public delegate BufferEx SxBufferLookup(KeyHash hash);
  public delegate MappedMemory SxMemoryLookup(KeyHash hash);
  public delegate Span<T> SxSpanLookup<T>(KeyHash hash) where T : unmanaged;
  public unsafe delegate T* SxPtrLookup<T>(KeyHash hash) where T : unmanaged;
}
```

Then make your custom uniform buffer type.
```cs
// <MyBuffer Id="MyBuf" Size="1" />, where Size is the number of sequential MyBufferUbo elements in the buffer
[SxUniformBuffer("MyBuffer")]
[StructLayout(LayoutKind.Sequential, Pack=1)]
public struct MyBufferUbo
{
  public float V1;
  public float V2;

  // lookup delegate fields must be static fields on the buffer element type
  // the names and specific types of these are not relevant, as long as the delegate signature matches
  // these are not all required, but you will need at least one to be able to set the uniform data
  [SxUniformBufferLookup] public static SxBufferLoop LookupBuffer;
  [SxUniformBufferLookup] public static SxMemoryLookup LookupMemory;
  [SxUniformBufferLookup] public static SxSpanLookup<MyBufferUbo> LookupSpan; // gives a Span<T> of length Size
  [SxUniformBufferLookup] public static SxPtrLookup<MyBufferUbo> LookupPtr; // gives T* to first element
}
```

The buffers can then be accessed via a lookup function. `Id` is not required on the buffer xml element, but it is the only way you will be able to access the buffer.
```cs
Span<MyBufferUbo> data = MyBufferUbo.LookupSpan(KeyHash.Make("MyBuf"));
```

Buffers can be shared between shaders by specifying `Id` without `Size`.
```xml
<Assets>
  <ShaderEx Id="MyFragmentShader" Path="MyShader.frag">
    <MyBuffer Id="MyBuf" Size="1" />
  </ShaderEx>
  <ShaderEx Id="MyFragmentShader2" Path="MyShader2.frag">
    <MyBuffer Id="MyBuf" />
  </ShaderEx>
  <MyBuffer Id="MyBuf2" Size="1" />
  <ShaderEx Id="MyFragmentShader3" Path="MyShader.frag">
    <MyBuffer Id="MyBuf2" />
  </ShaderEx>
</Assets>
```

Note: the elements are aligned with std140 which means padding might be necessary for elements (like vec3 or mat3)

## Push Constants

Push constants can be added to `ShaderEx` shaders for small values that are updated without using a descriptor binding. Each `ShaderEx` instance supports one push constant binding, and its lookup returns a `Span<T>` with length 1. The push constant data must be 4-byte aligned and no larger than the guaranteed Vulkan minimum of 128 bytes.

To use push constants, first add the `SxPushConstantAttribute` and `SxPushConstantLookupAttribute` types to your assembly in the `ShaderExtensions` namespace. The `[SxPushConstant]` attribute must be applied to the push constant struct, and the `[SxPushConstantLookup]` attribute must be applied to a static delegate field on that same struct.

```cs
#pragma warning disable CS9113
using System;
using KSA;
namespace ShaderExtensions
{
  [AttributeUsage(AttributeTargets.Struct)]
  internal class SxPushConstantAttribute(string xmlElement) : Attribute;

  [AttributeUsage(AttributeTargets.Field)]
  internal class SxPushConstantLookupAttribute() : Attribute;

  // You can use your own delegate type as long as the signature returns Span<T>
  public delegate Span<T> SxPushConstantLookup<T>(KeyHash hash) where T : unmanaged;
}
```

Then make your custom push constant type. The XML element name comes from the `[SxPushConstant(...)]` attribute. Use a layout compatible with 4-byte alignment and add manual padding when the C# layout would not match the GLSL push constant block.

```cs
using System.Runtime.InteropServices;

// <MyPushConstant Id="MyPush" />
[SxPushConstant("MyPushConstant")]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MyPushConstantData
{
  public float Intensity;
  public float Time;

  // lookup delegate fields must be static fields on the push constant type
  [SxPushConstantLookup] public static SxPushConstantLookup<MyPushConstantData> Lookup;
}
```

Add the push constant element to the shader XML. The optional `Stage` attribute defaults to `FragmentBit`.

```xml
<Assets>
  <ShaderEx Id="MyFragmentShader" Path="MyShader.frag">
    <MyPushConstant Id="MyPush" Stage="FragmentBit" />
  </ShaderEx>
</Assets>
```

The data can then be accessed and updated via the lookup function. `Id` is required if you want to update the push constant from code. The framework assigns lookup delegates during asset load, so only call them after the shader assets have loaded.

```cs
Span<MyPushConstantData> data = MyPushConstantData.Lookup(KeyHash.Make("MyPush"));
data[0].Intensity = 1.0f;
data[0].Time = time;
```

Declare the matching push constant block in the shader.

```glsl
layout(push_constant) uniform MyPushConstant
{
  float Intensity;
  float Time;
} Push;

void main()
{
  float intensity = Push.Intensity;
}
```

[^Sximgui]: The marker key must be the hash of the string `SxImGuiShader`, but this class does not need to exist in this form in order to function, it is just a utility.
