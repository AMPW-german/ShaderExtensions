# Deprecated because no working test case was available or known:
- `<GaugeCanvasEx>` asset that allows adding a post-processing shader to the rendered gauge

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