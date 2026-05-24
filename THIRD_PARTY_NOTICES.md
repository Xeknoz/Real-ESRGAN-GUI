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

## dirent

- Source: https://github.com/tronkko/dirent
- Build input: `third_party/ncnn_src/src/win32dirent.h`
- Used for: Windows directory iteration in the local backend.
- License: MIT.
- Copyright notice retained: Copyright (c) 1998-2019 Toni Ronkko.
- Downstream notice handling: MIT requires the copyright notice and permission notice to be included in all copies or substantial portions of the software. The full required text is distributed as `licenses/dirent-MIT-LICENSE.txt`.

## stb image headers

- Source: https://github.com/nothings/stb
- Build input: `third_party/ncnn_src/src/stb_image.h` and `third_party/ncnn_src/src/stb_image_write.h`
- Used for: image loading and image writing in the local backend.
- License: dual MIT License or Public Domain / Unlicense-style dedication.
- Copyright notice retained: Copyright (c) 2017 Sean Barrett.
- Downstream notice handling: the public-domain alternative has no notice condition, but this distribution still carries the upstream dual-license text as `licenses/stb-LICENSE.txt` for traceability.

## ncnn

- Source: https://github.com/Tencent/ncnn
- Build input: `third_party/ncnn_src/src/ncnn/`
- License: BSD 3-Clause for ncnn, with additional upstream third-party notices under zlib, BSD 2-Clause, BSD 3-Clause, and other listed terms in the ncnn license file.
- Copyright notice retained: Copyright (C) 2017 THL A29 Limited, a Tencent company. All rights reserved.
- Downstream notice handling: the full ncnn license and its listed third-party notices are distributed as `licenses/ncnn-LICENSE.txt`.

## libwebp

- Source: https://chromium.googlesource.com/webm/libwebp
- Build input: `third_party/ncnn_src/src/libwebp/`
- License: BSD 3-Clause, with an additional WebM Project patent grant carried in upstream `PATENTS`.
- Copyright notice retained: Copyright (c) 2010, Google Inc. All rights reserved.
- Downstream notice handling: BSD 3-Clause requires redistributions to retain the copyright notice, license conditions, and disclaimer, and prohibits endorsement by Google or contributors without permission. The full required text is distributed as `licenses/libwebp-COPYING.txt`, and the upstream patent grant is distributed as `licenses/libwebp-PATENTS.txt`.

## pybind11

- Source: https://github.com/pybind/pybind11
- Notice source: `third_party/ncnn_src/src/ncnn/python/pybind11/`
- License: BSD 3-Clause.
- Copyright notice retained: Copyright (c) 2016 Wenzel Jakob <wenzel.jakob@epfl.ch>. All rights reserved.
- Downstream notice handling: pybind11 is retained from the upstream ncnn source tree notices and is not an application Python runtime dependency. BSD 3-Clause requires redistributions to retain the copyright notice, license conditions, and disclaimer, and prohibits endorsement by the copyright holder or contributors without permission. The full required text is distributed as `licenses/pybind11-LICENSE.txt`.

## glslang

- Source: https://github.com/KhronosGroup/glslang
- Build input: `third_party/ncnn_src/src/ncnn/glslang/`
- Licenses/notices retained: BSD 3-Clause, BSD 2-Clause, MIT, Apache License 2.0, the historical GPL 3 with special Bison exception text that upstream keeps in its license file while noting Bison was removed long ago, and the NVIDIA preprocessor license notice.
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

## Enigma Virtual Box optional boxed portable runtime

- Source: https://enigmaprotector.com/en/products/evb.html
- Used only for optional single-file portable executables generated by `scripts/build-enigma.ps1`.
- License: Enigma Virtual Box end user license agreement.
- Copyright notice retained: Copyright (C) 2004-2025 The Enigma Protector Developers Team. All Rights Reserved.
- Downstream notice handling: standard portable-folder and installer builds do not embed Enigma Virtual Box. Enigma boxed portable builds copy the license text into `licenses/Enigma-Virtual-Box-LICENSE.txt` so the GUI About window can display it, and a maintainer copy is kept at `packaging/windows/EnigmaVirtualBox.LICENSE.txt`.

## Inno Setup installer runtime

- Source: https://jrsoftware.org/isinfo.php
- Used to build the Windows installer and uninstaller.
- License: Inno Setup License.
- Copyright notices retained: Copyright (C) 1997-2026 Jordan Russell. All rights reserved.; Portions Copyright (C) 2000-2026 Martijn Laan. All rights reserved.
- Downstream notice handling: the Inno Setup License allows use and redistribution, including commercial use, provided redistributions retain copyright notices and web site addresses, do not misrepresent origin, and plainly mark modified versions. The plain-text setup notice at `packaging/windows/THIRD_PARTY_NOTICES.txt` includes the required copyright notices, upstream web site address, and complete Inno Setup License text shown by the installer. A maintainer copy of the same license text is kept at `packaging/windows/InnoSetup.LICENSE.txt`.

## Inno Setup Chinese Simplified messages

- Source: https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation
- Embedded in the installer to provide Simplified Chinese setup UI text.
- License: MIT.
- Copyright notice retained: Copyright (c) 2019 - 2020 kirakira.
- Downstream notice handling: MIT requires the copyright notice and permission notice to be included in all copies or substantial portions of the software. The plain-text setup notice at `packaging/windows/THIRD_PARTY_NOTICES.txt` includes the required copyright notice, upstream source URL, and complete MIT License text shown by the installer. A maintainer copy of the same license text is kept at `packaging/windows/languages/ChineseSimplified.LICENSE.txt`.

## Additional notices carried by ncnn

The `ncnn` source tree also preserves additional upstream license and notice files for bundled or optional third-party components.

When redistributing source or binaries, keep the relevant upstream license files and notices with the distribution.
