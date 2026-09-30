using System.Globalization;

namespace Saas.Services;

public static class CsvImportHelper
{
    public static string NormalizarCabecalho(string valor)
    {
        var semAcento = new string(valor.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return new string(semAcento.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static bool TryParseDecimalFlexivel(string valor, out decimal resultado)
    {
        valor = valor.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out resultado)
            || decimal.TryParse(valor, NumberStyles.Any, new CultureInfo("pt-BR"), out resultado);
    }

    public static bool TryParseDataFlexivel(string valor, out DateTime resultado)
    {
        return DateTime.TryParse(valor, new CultureInfo("pt-BR"), DateTimeStyles.None, out resultado)
            || DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out resultado);
    }

    public static List<string[]> ParseCsv(string conteudo)
    {
        var linhas = conteudo.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
            .Where(l => l.Length > 0)
            .ToList();
        if (linhas.Count == 0)
        {
            return [];
        }

        var separador = linhas[0].Count(c => c == ';') >= linhas[0].Count(c => c == ',') ? ';' : ',';
        var resultado = new List<string[]>();

        foreach (var linha in linhas)
        {
            var campos = new List<string>();
            var atual = new System.Text.StringBuilder();
            var dentroDeAspas = false;

            for (var i = 0; i < linha.Length; i++)
            {
                var c = linha[i];
                if (dentroDeAspas)
                {
                    if (c == '"')
                    {
                        if (i + 1 < linha.Length && linha[i + 1] == '"')
                        {
                            atual.Append('"');
                            i++;
                        }
                        else
                        {
                            dentroDeAspas = false;
                        }
                    }
                    else
                    {
                        atual.Append(c);
                    }
                }
                else if (c == '"')
                {
                    dentroDeAspas = true;
                }
                else if (c == separador)
                {
                    campos.Add(atual.ToString());
                    atual.Clear();
                }
                else
                {
                    atual.Append(c);
                }
            }

            campos.Add(atual.ToString());
            resultado.Add(campos.ToArray());
        }

        return resultado;
    }
}
