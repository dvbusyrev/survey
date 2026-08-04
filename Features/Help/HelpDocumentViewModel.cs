namespace MainProject.Web.ViewModels;

public sealed class HelpDocumentViewModel
{
    public List<HelpDocumentBlock> Blocks { get; } = new();
}

public abstract record HelpDocumentBlock;

public sealed record HelpParagraphBlock(string Text) : HelpDocumentBlock;

public sealed record HelpTableBlock(IReadOnlyList<IReadOnlyList<string>> Rows) : HelpDocumentBlock;

public sealed record HelpImageBlock(string DataUri, string AltText) : HelpDocumentBlock;

public sealed class HelpPageViewModel
{
    public required HelpInstructionInfoViewModel AdminInstruction { get; init; }

    public required HelpInstructionInfoViewModel ClientInstruction { get; init; }
}

public sealed class HelpInstructionInfoViewModel
{
    public required string Type { get; init; }

    public required string UploadRole { get; init; }

    public required string Title { get; init; }

    public required string FileName { get; init; }

    public required string UploadedAtText { get; init; }

    public required string DownloadUrl { get; init; }

    public bool HasFile { get; init; }

    public string DisplayText => HasFile
        ? $"{FileName} {UploadedAtText}"
        : "Файл не загружен";
}
