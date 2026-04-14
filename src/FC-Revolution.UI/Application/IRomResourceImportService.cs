using System.Collections.Generic;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public interface IRomResourceImportService
{
    ImportedRomResource ImportRom(string sourcePath);

    IReadOnlyList<ImportedRomResource> ImportRomDirectory(
        string directoryPath,
        bool recursive = true,
        IReadOnlyList<string>? supportedFilePatterns = null);

    ImportedRomResource ImportPreviewVideo(string romPath, string sourcePath);

    ImportedRomResource ImportCoverImage(string romPath, string sourcePath);

    ImportedRomResource ImportArtworkImage(string romPath, string sourcePath);
}
