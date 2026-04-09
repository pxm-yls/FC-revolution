Bundled FFmpeg assets live here.

- `runtimes/` contains the native libraries copied into the app output and loaded by `FFmpeg.AutoGen`.
- `tools/` contains vendored FFmpeg command-line binaries copied into the repository for maintenance use only.

The application must run against the bundled files copied into its own output directory. It must not depend on the workspace root `ffmpeg` files or any system-installed FFmpeg.
