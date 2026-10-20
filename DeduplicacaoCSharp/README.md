# Deduplicação local em C# — 2.0.0

Aplicação ASP.NET Core Razor Pages para revisão supervisionada de duplicatas em arquivos RIS. A comparação, decisões, leitura de arquivos, persistência e exportação são implementadas em C#. A interface usa HTML Razor e CSS, sem JavaScript e sem pacotes externos de aplicação.

## Executar no Windows

1. Instale o **SDK do .NET 8** (o SDK, não apenas o runtime): https://dotnet.microsoft.com/download/dotnet/8.0 .
2. Extraia todo o ZIP.
3. Na pasta `DeduplicacaoCSharp`, execute `iniciar.bat` ou abra um terminal e execute:

```powershell
dotnet run --project DeduplicacaoCSharp.csproj
```

4. Abra **http://127.0.0.1:5080** no navegador.
5. Mantenha o terminal aberto enquanto usa a aplicação. Para encerrar, pressione `Ctrl+C`.

Também funciona por `dotnet run` em Linux/macOS com SDK compatível. No Visual Studio, abra o `.csproj`. Não é necessário Node.js ou banco de dados. O projeto não é um HTML que executa sozinho: há um servidor C# local.

## Testar antes de importar a revisão

Importe `exemplo.ris`. São três registros fictícios: dois da mesma publicação e um de outra publicação. Ao confirmar uma cópia como duplicada, os totais esperados são **3 importados, 1 removido, 2 mantidos**. Use Novo projeto antes de importar os dados reais.

## Continuar o trabalho da versão anterior

Na aplicação HTML antiga, use **Salvar projeto JSON**. Na versão C#, abra **Retomar backup ou começar outro projeto**, selecione o JSON e confirme a substituição. Backups versão `1.0.0` são aceitos; após uma alteração, o projeto passa a ser gravado no formato `2.0.0`. A migração é unidirecional: a aplicação antiga não aceita backups novos.

RIS é formato de intercâmbio bibliográfico, não backup de decisões. Para preservar o histórico, migre pelo JSON. O salvamento automático no navegador da versão anterior não é transferido sozinho.

## Fluxo de utilização

1. Importe o arquivo dos **190 registros**, ou as exportações originais por base. Não importe ambos: isso repetiria artificialmente a entrada.
2. Confira o total e os acentos. UTF-8 é padrão; Windows-1252 está disponível.
3. Na aba Pares de duplicatas, examine os metadados. Informe responsável e justificativa antes de confirmar uma remoção ou manter publicações distintas.
4. Decidir depois apenas passa ao próximo par; não registra exclusão.
5. Todos os registros permite buscar título, autor, DOI ou ID e filtrar mantidos/removidos. Ver registro mostra todos os metadados e o RIS original.
6. Use Desfazer para reverter a última decisão ativa. O histórico conserva a reversão.
7. Exporte backup, RIS, inventário e relatório. Com pares pendentes, o RIS é explicitamente parcial e mantém todos os registros não removidos.

## Como ler o código

Todos os arquivos usam quatro espaços de indentação e chaves em linhas separadas. `.editorconfig` preserva o padrão no editor.

| Arquivo | Responsabilidade |
| --- | --- |
| `Program.cs` | Inicia o servidor apenas em `127.0.0.1:5080`. |
| `Models/BibliographicRecord.cs` | Representa os campos RIS, ID e fonte. |
| `Models/ReviewProject.cs` | Representa a sessão e os arquivos importados. |
| `Models/ReviewDecision.cs` | Representa uma decisão e sua justificativa. |
| `Services/RisParser.cs` | Lê os registros RIS e preserva os campos repetidos. |
| `Services/TextNormalizer.cs` | Padroniza DOI e títulos. |
| `Services/SimilarityCalculator.cs` | Calcula Dice de bigramas. |
| `Services/DuplicateDetector.cs` | Compara os pares e gera candidatos. **Comece por `Compare()`.** |
| `Services/ReviewState.cs` | Aplica decisões, encontra representantes e bloqueia conflitos. |
| `Services/ProjectStore.cs` | Valida e salva o projeto local de forma atômica. |
| `Services/ExportService.cs` | Gera RIS, CSV e relatório JSON. |
| `Pages/Index.cshtml.cs` | Recebe ações da interface, usando os serviços. |
| `Pages/Index.cshtml` | Monta a interface com Razor. |
| `wwwroot/css/site.css` | Aparência, espaçamento e responsividade. |

## Critérios implementados

Cada par de registros é comparado uma vez. Para 190 registros, são 17.955 comparações. Um par vira candidato quando:

1. Os DOIs não vazios são iguais após normalização; ou
2. Os títulos não vazios são iguais após normalização; ou
3. Ambos os títulos normalizados têm pelo menos 20 caracteres e a similaridade Dice é pelo menos 0,90.

Os parâmetros ficam em `DuplicateDetector.SimilarityThreshold` e `DuplicateDetector.MinimumTitleLength`. O relatório usa os valores dessas constantes. Se alterá-los, atualize também os textos explicativos da página e esta documentação e registre uma nova versão do método. Não altere critérios no meio de uma revisão sem documentar a mudança.

A normalização aplica NFD, remove diacríticos U+0300 a U+036F, converte para minúsculas e substitui pontuação por espaço. O DOI remove prefixos `doi:`, `https://doi.org/` e variantes `dx.doi.org`. A comparação aproximada conta a multiplicidade dos bigramas.

O limiar é heurístico e não foi calibrado para seu corpus. Autores, ano, tipo e veículo são apresentados para a confirmação manual, não restringem a geração de candidatos. DOIs diferentes são destacados. DOIs vazios não correspondem entre si. Não há consulta externa nem IA.

Publicações distintas de um mesmo estudo, teses e artigos derivados não são automaticamente duplicatas. Títulos traduzidos, truncados ou muito diferentes podem não ser encontrados. Zero candidatos pendentes não certifica que a deduplicação foi exaustiva.

## Dados e rastreabilidade

- Um projeto ativo por instância. Não é um sistema multiusuário.
- Salva automaticamente em `App_Data/projeto.json`, com cópia anterior em `projeto.json.previous`.
- Use também Salvar backup JSON para um arquivo transportável e arquivado por data.
- O arquivo inclui registros originais, textos importados, SHA-256 dos bytes de entrada, decisões ativas e histórico.
- Mutação ocorre numa cópia validada; um erro de importação cancela todo o lote.
- Um arquivo com o mesmo hash não é importado duas vezes na sessão.
- Formulários têm antifalsificação de requisições e controle de revisão para rejeitar ações de abas desatualizadas.
- Remoção significa retirar da exportação, não apagar o original.
- O termo interno `merge` registra a consolidação; **não mescla campos bibliográficos**.
- Fontes consolidadas aparecem no inventário e nos backups. O RIS mantém os campos da cópia escolhida.
- Datas são registradas em UTC. Logs são editáveis, não assinados digitalmente.
- A importação usa o limite padrão de requisição do ASP.NET Core, aproximadamente 30 MB por requisição; adequada a RIS bibliográficos, não PDFs.

O aplicativo não modifica o Mendeley. Reimportar um RIS não elimina as cópias antigas no Mendeley nem preserva automaticamente PDFs, coleções e notas. A seleção por título/resumo ainda é uma etapa externa a esta aplicação.

## Testes

O projeto irmão `DeduplicacaoCSharp.Tests` é um executável de testes sem pacotes NuGet externos. Na pasta que contém ambos os projetos:

```powershell
dotnet run --project DeduplicacaoCSharp.Tests
```

Os testes verificam parsing RIS, normalização, Dice, contagens, cadeia de remoções, exportação, reversão, conflito transitivo, metadados vazios, backups antigos e consistência do RIS original.

## Revisão científica

Preserve os arquivos originais por base. Documente software/versão, critérios, datas, quem confirmou os candidatos e quantas cópias foram removidas. Mantenha essas remoções separadas de exclusões posteriores por título/resumo. O software auxilia a rastreabilidade, mas não certifica aceitação por um periódico.
