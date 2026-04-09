namespace MainProject.Web.ViewModels;

public sealed class HelpDocumentViewModel
{
    public List<HelpDocumentBlock> Blocks { get; } = new();
}

public abstract record HelpDocumentBlock;

public sealed record HelpParagraphBlock(string Text) : HelpDocumentBlock;

public sealed record HelpTableBlock(IReadOnlyList<IReadOnlyList<string>> Rows) : HelpDocumentBlock;

public sealed record HelpImageBlock(string DataUri, string AltText) : HelpDocumentBlock;
