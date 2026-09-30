namespace Saas.Services;

public sealed record AnexoValidacaoResultado(bool Valido, string? Erro);

public sealed record AnexoSalvo(string CaminhoRelativo, string NomeOriginal);

public static class AnexoStorage
{
    private static readonly string[] ExtensoesPermitidas = [".pdf", ".jpg", ".jpeg", ".png"];
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024;

    public static AnexoValidacaoResultado Validar(IFormFile arquivo)
    {
        if (arquivo.Length == 0)
        {
            return new AnexoValidacaoResultado(false, "O arquivo está vazio.");
        }

        if (arquivo.Length > TamanhoMaximoBytes)
        {
            return new AnexoValidacaoResultado(false, "O arquivo deve ter no máximo 5 MB.");
        }

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            return new AnexoValidacaoResultado(false, "Formato não aceito. Envie um PDF, JPG ou PNG.");
        }

        return new AnexoValidacaoResultado(true, null);
    }

    public static async Task<AnexoSalvo> SalvarAsync(string contentRootPath, int empresaId, string tipoEntidade, int entidadeId, IFormFile arquivo)
    {
        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var nomeArquivo = $"{Guid.NewGuid():N}{extensao}";
        var pastaRelativa = Path.Combine("Anexos", empresaId.ToString(), tipoEntidade, entidadeId.ToString());
        var pastaAbsoluta = Path.Combine(contentRootPath, pastaRelativa);
        Directory.CreateDirectory(pastaAbsoluta);

        var caminhoAbsoluto = Path.Combine(pastaAbsoluta, nomeArquivo);
        await using (var destino = File.Create(caminhoAbsoluto))
        {
            await arquivo.CopyToAsync(destino);
        }

        var caminhoRelativo = Path.Combine(pastaRelativa, nomeArquivo);
        return new AnexoSalvo(caminhoRelativo, Path.GetFileName(arquivo.FileName));
    }

    public static void Excluir(string contentRootPath, string? caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo))
        {
            return;
        }

        var caminhoAbsoluto = Path.Combine(contentRootPath, caminhoRelativo);
        if (File.Exists(caminhoAbsoluto))
        {
            File.Delete(caminhoAbsoluto);
        }
    }

    public static string ContentTypeDe(string nomeArquivo)
    {
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        return extensao switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
