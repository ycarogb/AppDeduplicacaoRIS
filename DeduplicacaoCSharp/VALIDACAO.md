# Validação realizada

- Código C# compilado com Roslyn do SDK .NET 8.0.408.
- Páginas Razor compiladas com o gerador Razor do mesmo SDK.
- 20 verificações de regras de negócio executadas com sucesso.
- Servidor iniciado e fluxo HTTP verificado: página inicial, importação do RIS de exemplo, listagem, confirmação de remoção, exportação com 2 registros, filtro de removidos, desfazer e backup com histórico.

Neste ambiente, o comando MSBuild padrão não pôde executar por limitação na consulta de processos. A validação usou o compilador e o gerador Razor diretamente. O pacote mantém a estrutura padrão de projeto .NET para execução local com dotnet run. Não foi realizada inspeção visual em navegador.
