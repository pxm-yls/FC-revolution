这里保存的是工程内随附的 FFmpeg 原始运行库文件。

构建 `FC-Revolution.UI` 时，会直接把以下目录中的文件复制到应用输出目录：

- `win-x64/native/`
- `osx-x64/native/`

建议仅保留预览播放所需库：

- Windows: `avcodec-*.dll`, `avformat-*.dll`, `avutil-*.dll`, `swscale-*.dll`
- macOS: `libavcodec*.dylib`, `libavformat*.dylib`, `libavutil*.dylib`, `libswscale*.dylib`
