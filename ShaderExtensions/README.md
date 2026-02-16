# ShaderExtensions

Post Processing Shaders and uniform buffers for KSA

Current Features:
- Adds a `<ShaderEx>` asset that allows adding additional texture and uniform buffer bindings to fragment shaders
- Adds a `<GaugeCanvasEx>` asset that allows adding a post-processing shader to the rendered gauge
- Adds a `<ImGuiShader>` asset that allows running a custom shader for a specific window
- Adds `<PostProcessingShader>` and `<GlobalPostShader>` assets to run post processing shaders pre/post imgui

## Installation

- Required [Starmap](https://github.com/StarMapLoader/StarMap) and [KittenExtensions](https://github.com/tsholmes/KittenExtensions/releases/latest)
- Download zip from [Releases](https://github.com/AMPW-german/ShaderExtensions/releases/latest) and extract into game `Content` folder
- Add to `manifest.toml` in `%USER%/my games/Kitten Space Agency`
    ```toml
    [[mods]]
    id = "ShaderExtensions"
    enabled = true
    ```

## Post Processing Shaders

There are two types of post processing shaders available, pre imgui shaders and post imgui shaders.\
The only difference between them is the execution point and the asset name, otherwise they are identical which is why all of the examples are for the global (post) post processing shaders.

Both types are based on the `ShaderEx` asset so they support the same attributes and additional bindings. The shaders are ordered with the `RenderPassId` attributes. It defaults to the `ScreenspaceVert` shader but custom vertex shaders can be set with the `VertexShaderID` attribute.\
Normal post processing shaders use the `PostProcessingShader` asset and global post processing shaders use the `GlobalPostShader` asset.

### Limitations:

The shaders only target the main window, any other windows are ignored. Additionally, these shaders can't be disabled and will always run at their designated stage (use custom bindings to conditionally return the original color and achieve the same effect as disabling the shaders).\
This is only designed for unique Renderpass/Subpass combinations. While it won't crash it there is more than shader per Pass the execution order is no longer guaranteed. Sampler2D shaders in the **same** renderpass will always run before subpass shaders.

### Shader types

#### Subpass shaders

Normal GlobalPostShaders have a uniform `subpassInput` at set 1 binding 0 as pixel color source. They use the `SubpassId` for ordering the subpasses.\
It is recommended to use subpasses with the same renderpass if free input sampling is not neccesary.

```xml
<GlobalPostShader Id="GEffectFrag" Path="Shaders/GEffectShader.frag" RenderPassId="32" SubpassId="64"/>
```

```glsl
#version 450 core

layout(location = 0) out vec4 outColor;
layout(set = 1, binding = 0) uniform subpassInput Source;

void main()
{
    vec4 c = subpassLoad(Source);
    outColor = c;
}
```

#### Sampler2D shaders

Shaders that need a sampler2D as input (this allows free sampling of the input) need their own dedicated renderpass which can be done by setting the `RequiresUniqueRenderpass` attribute to `true`. They are not using the SubpassId attribute.

```xml
<GlobalPostShader Id="BlurFrag" Path="Shaders/Blur.frag" RenderPassId="16" RequiresUniqueRenderpass="true" />
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

## GaugeCanvas Post-Processing

To add a post-processing shader to a Gauge, use the `<GaugeCanvasEx>` element and add a vertex and fragment shader to it. The included `GaugeVertexPost` vertex shader draws one rect covering the entire gauge, and can be used in most cases. The fragment shader is a `ShaderEx` asset that will have `layout(set=1, binding=0)` bound to the rendered gauge canvas, with custom bindings starting at `layout(set=1, binding=1)`.

```xml
<Assets>
  <GaugeCanvasEx>
    <PostVertex Id="GaugeVertexPost" />
    <PostFragment Path="MyPost.frag" />
  </GaugeCanvasEx>
</Assets>
```

```glsl
#version 450

layout(location = 0) in vec2 inUv;
layout(location = 0) out vec4 outColor;
layout(set = 1, binding = 0) uniform sampler2D gaugeCanvas;

void main()
{
  outColor = textureLod(gaugeCanvas, inUv, 0);
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

Additional bindings can be added to a shader by using the `ShaderEx` top-level tag, or the `FragmentEx` tag in a gauge component. Top-level defined shaders will still only have the additional bindings injected when used as a post processing or gauge component fragment shader

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

The additional bindings will be available in the fragment shader on set 1, starting from binding 1 (binding 0 will be the existing gauge font atlas)

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


[^Sximgui]: The marker key must be the hash of the string `SxImGuiShader`, but this class does not need to exist in this form in order to function, it is just a utility.
