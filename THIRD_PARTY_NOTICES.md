# Third-party notices

The original GUI, launcher, scripts, and repository-specific documentation in this repository are licensed under the MIT License in [`LICENSE`](LICENSE).

This repository also includes or distributes third-party components under their own licenses. Their original notices remain in the source tree and are not replaced by this project's MIT License.

## Bundled backend and model payload

- `third_party/ncnn_src/` and the runtime backend payload under `runtime/engine/`
- License: MIT
- Copyright (c) 2021 Xintao Wang
- Portions based on `realsr-ncnn-vulkan`
- Copyright (c) 2019 nihui
- Source notice: `third_party/ncnn_src/LICENSE`

## ncnn

- Path: `third_party/ncnn_src/src/ncnn/`
- License: BSD 3-Clause, with additional third-party notices listed by upstream
- Copyright (C) 2017 THL A29 Limited, a Tencent company
- Source notice: `third_party/ncnn_src/src/ncnn/LICENSE.txt`

## libwebp

- Path: `third_party/ncnn_src/src/libwebp/`
- License: BSD 3-Clause
- Copyright (c) 2010 Google Inc.
- Source notice: `third_party/ncnn_src/src/libwebp/COPYING`

## Additional notices carried by ncnn

The `ncnn` source tree also preserves license files for bundled third-party components, including:

- `glslang`: multiple permissive licenses and Apache-2.0 notices in `third_party/ncnn_src/src/ncnn/glslang/LICENSE.txt`
- `pybind11`: BSD 3-Clause in `third_party/ncnn_src/src/ncnn/python/pybind11/LICENSE`

When redistributing source or binaries, keep the relevant upstream license files and notices with the distribution.
