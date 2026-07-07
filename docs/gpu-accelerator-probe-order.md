# GPU accelerator probe order

The prebuilt core (`libLiteRt`) walks a hardcoded table and registers the **first**
accelerator plugin it can `dlopen` — the active backend is controlled only by which
dylibs are present on the load path.

## Probe order (Android arm64)

| # | Plugin basename | Backends bundled | Shipped in |
| - | --------------- | ---------------- | ---------- |
| 0 | `libLiteRtGpuAccelerator.so`    | OpenCL + WebGPU/Dawn + Vulkan (multi-backend) | `LiteRT.Gpu.OpenCl` (see caveat) |
| 1 | `libLiteRtClGlAccelerator.so`   | OpenCL + GL interop | — (not shipped) |
| 2 | `libLiteRtOpenClAccelerator.so` | OpenCL only | `LiteRT.Gpu.OpenCl` |
| 3 | `libLiteRtWebGpuAccelerator.so` | WebGPU / Dawn | `LiteRT.Gpu.WebGpu` |
| 4 | `libLiteRtVulkanAccelerator.so` | Vulkan | — (not shipped) |

## Packaging consequence

Ship exactly **one** accelerator dylib per package: if two candidates sit in the same
directory, the earlier table entry wins and the later is never reached.
`LiteRT.Gpu.OpenCl` currently ships both `libLiteRtGpuAccelerator.so` (index 0) and
`libLiteRtOpenClAccelerator.so` (index 2), so the multi-backend one registers and the
OpenCL-only dylib is dead weight — fix by adjusting `classify()` in
`scripts/fetch-natives.sh` to route only the intended dylib.

Determined by reverse-engineering the prebuilt
`src/LiteRT/runtimes/android-arm64/native/libLiteRt.so`: decode the APS2-packed
(`DT_ANDROID_RELA`) relocations of the 5-entry plugin-name table in `.data.rel.ro`
(vaddr `0x4c0000`) and match addends against the `.rodata` basenames.
