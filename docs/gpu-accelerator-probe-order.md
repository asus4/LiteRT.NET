# GPU accelerator probe order

How the prebuilt LiteRT core (`libLiteRt`) decides which GPU accelerator plugin to
register, and what that means for how we package the per-backend native dylibs.

## Background

GPU support is optional. Each backend ships as a standalone accelerator plugin
(`libLiteRt<Backend>Accelerator.{so,dylib}`) that the core `dlopen`s at runtime and that
exports a single registration symbol, `LiteRtAcceleratorImpl` (versioned `VERS_1.0`). The
core decides which plugin to load from a **hardcoded, ordered list baked into the prebuilt
library** — not from anything we configure. We control the active backend only by which
dylibs are present on the load path.

## The probe order (Android arm64)

The core carries a fixed table of candidate plugin basenames. The registry walks it
top-to-bottom and registers the **first** plugin it can `dlopen` on the load path:

| # | Plugin basename | Backends bundled | Shipped in |
| - | --------------- | ---------------- | ---------- |
| 0 | `libLiteRtGpuAccelerator.so`    | OpenCL + WebGPU/Dawn + Vulkan (multi-backend) | `LiteRT.Gpu.OpenCl` (see caveat) |
| 1 | `libLiteRtClGlAccelerator.so`   | OpenCL + GL interop | — (not shipped) |
| 2 | `libLiteRtOpenClAccelerator.so` | OpenCL only | `LiteRT.Gpu.OpenCl` |
| 3 | `libLiteRtWebGpuAccelerator.so` | WebGPU / Dawn | `LiteRT.Gpu.WebGpu` |
| 4 | `libLiteRtVulkanAccelerator.so` | Vulkan | — (not shipped) |

Because the list is ordered, **if two candidate dylibs sit in the same directory, the one
earlier in the table wins and the later one is never reached.**

### `libLiteRtGpuAccelerator.so` vs `libLiteRtOpenClAccelerator.so`

Both are built from the same upstream tree
(`third_party/odml/litert/litert/runtime/accelerators/gpu/`), export the same
`LiteRtAcceleratorImpl` symbol, use the ML Drift (`ml_drift`) compute library, and
`dlopen` the same vendor drivers (`libOpenCL.so`, `libOpenCL-pixel.so`,
`libOpenCL-car.so`, `libGLES_mali.so`, `libGLESv3.so`). They differ only in how many
compute backends are statically linked in:

| | `libLiteRtOpenClAccelerator.so` | `libLiteRtGpuAccelerator.so` |
| - | - | - |
| Size (android-arm64) | ~2.7 MB | ~8.3 MB |
| OpenCL backend | yes | yes |
| WebGPU/Dawn backend | no | yes (`dawn_native`, `wgpu*`) |
| Vulkan backend | no | yes (`vkCreate*`, links `libvulkan`) |

## Packaging caveat

`LiteRT.Gpu.OpenCl/runtimes/android-*/native/` currently ships **both**
`libLiteRtGpuAccelerator.so` (table index 0) and `libLiteRtOpenClAccelerator.so`
(table index 2). Since index 0 wins, the multi-backend `GpuAccelerator` is the one
actually registered and the OpenCL-only dylib is dead weight.

`scripts/fetch-natives.sh` `classify()` routes both `*OpenClAccelerator.*` and
`*GpuAccelerator.*` into the `gpu-opencl` package, which is what lands both files in the
same directory. To ship exactly one accelerator, change `classify()` so only the intended
dylib reaches this package (e.g. skip `*GpuAccelerator.*`, or route it to its own package).

## How this was determined

Reverse-engineered from the prebuilt `src/LiteRT/runtimes/android-arm64/native/libLiteRt.so`:

1. `strings`/`nm -D` confirmed the five candidate basenames and the shared
   `LiteRtAcceleratorImpl@@VERS_1.0` export.
2. The basenames live in `.rodata` but have no inline `adrp`/`add` references — the
   pointers to them are stored in a table in `.data.rel.ro` (vaddr `0x4c0000`, 16-byte
   entries, name pointer at `+8`).
3. On Android those pointers are **packed relocations** (`DT_ANDROID_RELA`, APS2 format),
   so they are zero in-file; decoding the APS2 stream recovers each slot's addend (the
   string vaddr), giving the table order above.
4. The loader function (the `dlopen@plt` call cluster that begins with `mov w1, #0x5`,
   matching the 5-entry table length) walks this table.

To reproduce, decode the APS2 relocations at the `DT_ANDROID_RELA` offset and match
addends against the `.rodata` offsets of the accelerator basenames.
