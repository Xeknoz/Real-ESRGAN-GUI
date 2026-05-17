# Third-party notices

The original GUI, launcher, scripts, and repository-specific documentation in this repository are licensed under the MIT License in [`LICENSE`](LICENSE).

This repository also includes or distributes third-party components under their own licenses. Their original notices remain in the source tree and are not replaced by this project's MIT License. Portable and installer distributions include the relevant notice files under `licenses/`.

## Real-ESRGAN upstream source and model provenance

- Source: https://github.com/xinntao/Real-ESRGAN
- Model package: `scripts/build-models.ps1` downloads the official Real-ESRGAN NCNN release archive and extracts only the required `models/*.bin` and `models/*.param` entries
- Generated model payload: `artifacts/models/`, prepared from the official Real-ESRGAN NCNN release archive and copied into release portable builds
- License: BSD 3-Clause
- Copyright (c) 2021, Xintao Wang
- Distributed notice: `licenses/Real-ESRGAN-LICENSE.txt`

## Bundled NCNN/Vulkan backend payload

- `third_party/ncnn_src/` and generated backend executable/DLL payloads under `artifacts/backend/<arch>/engine/`
- License: MIT
- Copyright (c) 2021 Xintao Wang
- Portions based on `realsr-ncnn-vulkan`
- Copyright (c) 2019 nihui
- Distributed notice: `licenses/Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt`

## ncnn

- Path: `third_party/ncnn_src/src/ncnn/`
- License: BSD 3-Clause, with additional third-party notices listed by upstream
- Copyright (C) 2017 THL A29 Limited, a Tencent company
- Distributed notice: `licenses/ncnn-LICENSE.txt`

## libwebp

- Path: `third_party/ncnn_src/src/libwebp/`
- License: BSD 3-Clause
- Copyright (c) 2010 Google Inc.
- Distributed notice: `licenses/libwebp-COPYING.txt`

## pybind11

- Path: `third_party/ncnn_src/src/ncnn/python/pybind11/`
- License: BSD 3-Clause
- Copyright (c) 2016 Wenzel Jakob
- Distributed notice: `licenses/pybind11-LICENSE.txt`

## glslang

- Path: `third_party/ncnn_src/src/ncnn/glslang/`
- License: multiple permissive licenses and Apache-2.0 notices preserved by upstream
- Distributed notice: `licenses/glslang-LICENSE.txt`

## Microsoft .NET runtime and Windows Desktop runtime

- Self-contained WPF publishing copies .NET runtime and Windows Desktop runtime files into `artifacts/portable/<arch>/`
- License: MIT and additional third-party notices as provided by Microsoft
- Distributed notices: `licenses/Microsoft-dotnet-LICENSE.txt` and `licenses/Microsoft-dotnet-ThirdPartyNotices.txt`

## Microsoft Visual C++ OpenMP runtime

- Backend builds link against the Microsoft Visual C++ OpenMP runtime and distribute the release runtime `engine/vcomp140.dll`
- Distribution is limited to the normal Visual Studio redistributable runtime files; debug-only `debug_nonredist` runtime files such as `vcomp140d.dll` must not be shipped
- Distributed notice/reference: `licenses/Microsoft-Visual-Cpp-Redistributable-Redist.txt`

## Additional notices carried by ncnn

The `ncnn` source tree also preserves additional upstream license and notice files for bundled or optional third-party components.

When redistributing source or binaries, keep the relevant upstream license files and notices with the distribution.
