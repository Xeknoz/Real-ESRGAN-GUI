# Third-party notices

The original GUI, launcher, scripts, and repository-specific documentation in this repository are licensed under the MIT License in [`LICENSE`](LICENSE).

This repository also includes or distributes third-party components under their own licenses. Their original notices remain in the source tree and are not replaced by this project's MIT License. Portable and installed application distributions include the runtime notice files under `licenses/`; installer-only notices are kept under `packaging/windows/`.

The entries below identify each component and the downstream notice handling used by this distribution. For runtime components, the full copyright notices, license conditions, and disclaimers are copied into the installed application and displayed in the GUI About window under Licenses.

## Real-ESRGAN upstream source and model provenance

- Source: https://github.com/xinntao/Real-ESRGAN
- Model package: `scripts/build-models.ps1` downloads the official Real-ESRGAN NCNN release archive and extracts only the required `models/*.bin` and `models/*.param` entries.
- Generated model payload: `artifacts/models/`, prepared from the official Real-ESRGAN NCNN release archive and copied into release portable builds.
- License: BSD 3-Clause.
- Copyright notice retained: Copyright (c) 2021, Xintao Wang. All rights reserved.
- Downstream notice handling: BSD 3-Clause requires redistributions to retain the copyright notice, license conditions, and disclaimer, and prohibits endorsement by the copyright holder or contributors without permission. The full required text is distributed as `licenses/Real-ESRGAN-LICENSE.txt`.

## Bundled NCNN/Vulkan backend payload

- Source: https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan
- Build input: `third_party/ncnn_src/`
- Generated backend payload: `artifacts/backend/<arch>/engine/`
- License: MIT.
- Copyright notices retained: Copyright (c) 2021 Xintao Wang; Copyright (c) 2019 nihui for portions based on `realsr-ncnn-vulkan`.
- Downstream notice handling: MIT requires the copyright notice and permission notice to be included in all copies or substantial portions of the software. The full required text is distributed as `licenses/Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt`.

## ncnn

- Source: https://github.com/Tencent/ncnn
- Build input: `third_party/ncnn_src/src/ncnn/`
- License: BSD 3-Clause for ncnn, with additional upstream third-party notices under zlib, BSD 2-Clause, BSD 3-Clause, and other listed terms in the ncnn license file.
- Copyright notice retained: Copyright (C) 2017 THL A29 Limited, a Tencent company. All rights reserved.
- Downstream notice handling: the full ncnn license and its listed third-party notices are distributed as `licenses/ncnn-LICENSE.txt`.

## libwebp

- Source: https://chromium.googlesource.com/webm/libwebp
- Build input: `third_party/ncnn_src/src/libwebp/`
- License: BSD 3-Clause.
- Copyright notice retained: Copyright (c) 2010, Google Inc. All rights reserved.
- Downstream notice handling: BSD 3-Clause requires redistributions to retain the copyright notice, license conditions, and disclaimer, and prohibits endorsement by Google or contributors without permission. The full required text is distributed as `licenses/libwebp-COPYING.txt`.

## pybind11

- Source: https://github.com/pybind/pybind11
- Build input: `third_party/ncnn_src/src/ncnn/python/pybind11/`
- License: BSD 3-Clause.
- Copyright notice retained: Copyright (c) 2016 Wenzel Jakob <wenzel.jakob@epfl.ch>. All rights reserved.
- Downstream notice handling: BSD 3-Clause requires redistributions to retain the copyright notice, license conditions, and disclaimer, and prohibits endorsement by the copyright holder or contributors without permission. The full required text is distributed as `licenses/pybind11-LICENSE.txt`.

## glslang

- Source: https://github.com/KhronosGroup/glslang
- Build input: `third_party/ncnn_src/src/ncnn/glslang/`
- Licenses/notices retained: BSD 3-Clause, BSD 2-Clause, MIT, Apache License 2.0, GPL 3 with the special Bison exception notice, and the NVIDIA preprocessor license notice, as carried by upstream glslang's license file.
- Copyright notices retained include: Copyright (C) 2015-2018 Google, Inc.; Copyright 2020 The Khronos Group Inc.; Copyright (C) 1984, 1989-1990, 2000-2015 Free Software Foundation, Inc.; Copyright (c) 2002, NVIDIA Corporation.
- Downstream notice handling: the complete upstream glslang license file is distributed as `licenses/glslang-LICENSE.txt`.

## Microsoft .NET runtime and Windows Desktop runtime

- Sources: https://github.com/dotnet/runtime and https://github.com/dotnet/wpf
- Self-contained WPF publishing copies .NET runtime and Windows Desktop runtime files into `artifacts/portable/<arch>/`.
- License/terms retained: Microsoft Software License Terms, Microsoft .NET Library, plus Microsoft-provided third-party notices.
- Downstream notice handling: the complete Microsoft .NET license terms and third-party notices are distributed as `licenses/Microsoft-dotnet-LICENSE.txt` and `licenses/Microsoft-dotnet-ThirdPartyNotices.txt`.

## Microsoft Visual C++ OpenMP runtime

- Source/reference: https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist
- Backend builds link against the Microsoft Visual C++ OpenMP runtime and distribute the release runtime `engine/vcomp140.dll`.
- Distribution is limited to normal Visual Studio redistributable runtime files; debug-only `debug_nonredist` runtime files such as `vcomp140d.dll` must not be shipped.
- Downstream notice handling: the Microsoft redistributable notice/reference is distributed as `licenses/Microsoft-Visual-Cpp-Redistributable-Redist.txt`.

## Inno Setup installer runtime

- Source: https://jrsoftware.org/isinfo.php
- Used to build the Windows installer and uninstaller.
- License: Inno Setup License.
- Copyright notices retained: Copyright (C) 1997-2026 Jordan Russell. All rights reserved.; Portions Copyright (C) 2000-2026 Martijn Laan. All rights reserved.
- Downstream notice handling: the Inno Setup License allows use and redistribution, including commercial use, provided redistributions retain copyright notices and web site addresses, do not misrepresent origin, and plainly mark modified versions. The installer notice retains the copyright notices and upstream web site address, reproduces the complete license text in `packaging/windows/THIRD_PARTY_NOTICES.txt`, and keeps the source-tree license text at `packaging/windows/InnoSetup.LICENSE.txt`.

## Inno Setup Chinese Simplified messages

- Source: https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation
- Embedded in the installer to provide Simplified Chinese setup UI text.
- License: MIT.
- Copyright notice retained: Copyright (c) 2019 - 2020 kirakira.
- Downstream notice handling: MIT requires the copyright notice and permission notice to be included in all copies or substantial portions of the software. The installer notice reproduces the complete license text in `packaging/windows/THIRD_PARTY_NOTICES.txt`, and the source-tree license text is kept at `packaging/windows/languages/ChineseSimplified.LICENSE.txt`.

## Additional notices carried by ncnn

The `ncnn` source tree also preserves additional upstream license and notice files for bundled or optional third-party components.

When redistributing source or binaries, keep the relevant upstream license files and notices with the distribution.
