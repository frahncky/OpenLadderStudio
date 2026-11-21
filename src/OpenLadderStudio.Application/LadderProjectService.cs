using System.Text;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Application;

/// <summary>
/// Criar, abrir e salvar .pladder. Todo o formato fica em LadderProjectCodec (núcleo);
/// este serviço só converte entre o modelo público e o documento do codec.
/// </summary>
public sealed class LadderProjectService
{
    public const string FileExtension = ".pladder";

    // Mesma codificação do editor legado (UTF-8 com BOM): arquivos trocam entre as duas trilhas.
    private static readonly Encoding FileEncoding = Encoding.UTF8;

    public LadderProjectModel CreateNew()
    {
        var model = new LadderProjectModel();
        model.Rungs.Add(new LadderRungModel());
        return model;
    }

    public LadderProjectModel Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Deserialize(File.ReadAllText(path, FileEncoding));
    }

    public void Save(LadderProjectModel model, string path)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(path);
        string text = Serialize(model);
        string temp = path + ".tmp";
        File.WriteAllText(temp, text, FileEncoding);
        File.Move(temp, path, overwrite: true);
    }

    public string Serialize(LadderProjectModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return LadderProjectCodec.Serialize(ToDocument(model));
    }

    public LadderProjectModel Deserialize(string text)
    {
        return FromDocument(LadderProjectCodec.Deserialize(text));
    }

    public static string NormalizeComment(string? text) => LadderProjectCodec.NormalizeComment(text);

    internal static LadderProjectDocument ToDocument(LadderProjectModel model)
    {
        var document = new LadderProjectDocument();
        foreach (LadderRungModel rung in model.Rungs)
        {
            var target = new LadderProjectRung { Comment = rung.Comment };
            for (int column = 0; column < LadderRungModel.ColumnCount; column++)
            {
                Copy(rung.Series[column], target.Series[column]);
                Copy(rung.Parallel[column], target.Parallel[column]);
            }
            document.Rungs.Add(target);
        }
        return document;
    }

    private static LadderProjectModel FromDocument(LadderProjectDocument document)
    {
        var model = new LadderProjectModel();
        foreach (LadderProjectRung rung in document.Rungs)
        {
            var target = new LadderRungModel { Comment = rung.Comment };
            for (int column = 0; column < LadderRungModel.ColumnCount; column++)
            {
                Copy(rung.Series[column], target.Series[column]);
                Copy(rung.Parallel[column], target.Parallel[column]);
            }
            model.Rungs.Add(target);
        }
        return model;
    }

    private static void Copy(LadderCell source, LadderProjectElement target)
    {
        target.Kind = (LadderProjectElementKind)(int)source.Kind;
        target.Address = source.Address ?? string.Empty;
        target.Parameter = source.Parameter ?? string.Empty;
        target.Mode = source.Mode ?? string.Empty;
    }

    private static void Copy(LadderProjectElement source, LadderCell target)
    {
        target.Kind = (LadderElementKind)(int)source.Kind;
        target.Address = source.Address ?? string.Empty;
        target.Parameter = source.Parameter ?? string.Empty;
        target.Mode = source.Mode ?? string.Empty;
    }
}
